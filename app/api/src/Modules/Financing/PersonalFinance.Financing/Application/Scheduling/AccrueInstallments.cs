using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalFinance.Financing.Contracts.IntegrationEvents;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Infrastructure.Scheduling;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Financing.Application.Scheduling;

/// <summary>
/// Once a billing cycle has closed, each of its un-accrued installments is posted to the ledger and moved onto a monthly
/// statement. A plain plan posts Dr card expense / Cr card liability; a split plan splits the expense debit into the
/// holder's slice plus one receivable debit per participating party (D1/D11), tagged with the split reference.
/// </summary>
internal sealed class AccrueInstallments(IServiceScopeFactory scopeFactory, ILogger<AccrueInstallments> logger) : SchedulerBase(logger) {
    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);
    protected override bool RunOnStartup => true;

    protected override async Task TickAsync(CancellationToken cancellationToken) {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinancingDbContext>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var parties = scope.ServiceProvider.GetRequiredService<IPartiesApi>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();
        var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<AccrueInstallments>>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var pending = await (
            from installment in context.Set<Installment>()
            where installment.AccruedOnUtc == null
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            join card in context.CreditCards on plan.CardId equals card.Id
            select new { installment, card, plan }
        ).ToListAsync(cancellationToken);
        foreach(var row in pending) {
            var installment = row.installment;
            var card = row.card;
            var plan = row.plan;
            if(installment.AccruedOnUtc is not null) {
                continue;
            }
            if(!installment.Cycle.IsClosedAsOf(today, card.CutoffDay)) {
                continue;
            }
            var statement = await context.MonthlyStatements.FirstOrDefaultAsync(
                candidate => candidate.CardId == card.Id
                    && candidate.CycleYear == installment.CycleYear
                    && candidate.CycleMonth == installment.CycleMonth,
                cancellationToken
            );
            if(statement is null) {
                statement = MonthlyStatement.Open(card.Id, installment.Cycle);
                context.MonthlyStatements.Add(statement);
            }
            var splitParticipants = plan.SplitReferenceId is null
                ? []
                : await context.Set<PaymentPlanSplitParticipant>()
                    .Where(participant => participant.PaymentPlanId == plan.Id)
                    .ToListAsync(cancellationToken);
            var useSplit = plan.SplitReferenceId is not null && splitParticipants.Count > 0;
            var (lines, partyPortionMinorUnits) = useSplit
                ? buildSplitLines(installment, card, splitParticipants)
                : (new List<PostTransactionLine> {
                    new(card.ExpenseAccountId, DebitOrCredit.Debit, installment.Amount),
                    new(card.LiabilityAccountId, DebitOrCredit.Credit, installment.Amount)
                }, 0L);
            var posting = await ledger.PostTransactionAsync(
                new PostTransactionCommand(
                    lines,
                    now,
                    InstallmentReferenceId: installment.Id,
                    SplitReferenceId: plan.SplitReferenceId
                ),
                cancellationToken
            );
            if(posting.IsFailure) {
                scopedLogger.LogWarning(
                    "Skipping accrual of installment {InstallmentId}: ledger post failed ({ErrorCode}).",
                    installment.Id, posting.Error.Code
                );
                continue;
            }
            var accrual = statement.Accrue(installment);
            if(accrual.IsFailure) {
                scopedLogger.LogWarning(
                    "Skipping accrual of installment {InstallmentId}: {ErrorCode}.",
                    installment.Id, accrual.Error.Code
                );
                continue;
            }
            var marked = installment.MarkAccrued(now, statement);
            if(marked.IsFailure) {
                scopedLogger.LogWarning(
                    "Skipping accrual of installment {InstallmentId}: {ErrorCode}.",
                    installment.Id, marked.Error.Code
                );
                continue;
            }
            await context.SaveChangesAsync(cancellationToken);
            if(useSplit && partyPortionMinorUnits > 0) {
                var splitAccrual = await parties.RecordSplitAccrualAsync(
                    new RecordSplitAccrualCommand(plan.SplitReferenceId!.Value, partyPortionMinorUnits),
                    cancellationToken
                );
                if(splitAccrual.IsFailure) {
                    scopedLogger.LogWarning(
                        "Split accrual metadata not recorded for installment {InstallmentId} (split {SplitReferenceId}): {ErrorCode}.",
                        installment.Id, plan.SplitReferenceId, splitAccrual.Error.Code
                    );
                }
            }
            await dispatcher.DispatchAsync(
                new InstallmentAccruedIntegrationEvent(
                    Guid.CreateVersion7(),
                    DateTimeOffset.UtcNow,
                    installment.Id,
                    card.Id,
                    statement.Id,
                    installment.Amount.MinorUnits
                ),
                cancellationToken
            );
        }
    }

    private static (List<PostTransactionLine> Lines, long PartyPortionMinorUnits) buildSplitLines(
        Installment installment, CreditCard card, IReadOnlyList<PaymentPlanSplitParticipant> splitParticipants) {
        long[] weights = [1L, .. splitParticipants.Select(participant => participant.Weight)];
        var shares = new PhantomPennyAllocator().Allocate(installment.Amount, weights);
        var lines = new List<PostTransactionLine>();
        if(shares[0].MinorUnits > 0) {
            lines.Add(new PostTransactionLine(card.ExpenseAccountId, DebitOrCredit.Debit, shares[0]));
        }
        var partyPortion = 0L;
        for(var index = 0; index < splitParticipants.Count; index++) {
            var partyShare = shares[index + 1];
            if(partyShare.MinorUnits <= 0) {
                continue;
            }
            lines.Add(new PostTransactionLine(splitParticipants[index].ReceivableAccountId, DebitOrCredit.Debit, partyShare));
            partyPortion += partyShare.MinorUnits;
        }
        lines.Add(new PostTransactionLine(card.LiabilityAccountId, DebitOrCredit.Credit, installment.Amount));
        return (lines, partyPortion);
    }
}
