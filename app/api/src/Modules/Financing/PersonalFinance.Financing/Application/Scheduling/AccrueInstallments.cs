using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;
using PersonalFinance.Financing.Contracts.IntegrationEvents;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Infrastructure.Scheduling;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Financing.Application.Scheduling;

internal sealed class AccrueInstallments(
    IServiceScopeFactory scopeFactory, ILogger<AccrueInstallments> logger, TimeProvider timeProvider) : SchedulerBase(logger) {
    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);
    protected override bool RunOnStartup => true;

    protected override async Task TickAsync(CancellationToken cancellationToken) {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinancingDbContext>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var parties = scope.ServiceProvider.GetRequiredService<IPartiesApi>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();
        var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<AccrueInstallments>>();
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        await accrueClosedCyclesAsync(context, ledger, dispatcher, scopedLogger, now, today, cancellationToken);
        await accrueDueSplitReceivablesAsync(context, ledger, parties, scopedLogger, now, today, cancellationToken);
        await accrueDueCreditorSplitReceivablesAsync(context, ledger, parties, scopedLogger, now, today, cancellationToken);
    }

    private static async Task accrueClosedCyclesAsync(
        FinancingDbContext context, ILedgerApi ledger, IIntegrationEventDispatcher dispatcher,
        ILogger logger, DateTimeOffset now, DateOnly today, CancellationToken cancellationToken
    ) {
        await context.Set<ClosingOverride>().ToListAsync(cancellationToken);
        var pending = await (
            from installment in context.Set<Installment>()
            where installment.AccruedOnUtc == null
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CardId != null
            from card in context.CreditCards.Where(candidate => candidate.Id == plan.CardId)
            select new { installment, card, plan }
        ).ToListAsync(cancellationToken);
        foreach(var row in pending) {
            var installment = row.installment;
            var card = row.card;
            var plan = row.plan;
            if(installment.AccruedOnUtc is not null) {
                continue;
            }
            if(!card.IsClosedAsOf(installment.Cycle, today)) {
                continue;
            }
            var statement = await context.MonthlyStatements.FirstOrDefaultAsync(
                candidate => candidate.CardId == card.Id
                    && candidate.CycleYear == installment.CycleYear
                    && candidate.CycleMonth == installment.CycleMonth
                    && candidate.Currency == installment.Amount.Currency,
                cancellationToken
            );
            if(statement is null) {
                statement = MonthlyStatement.Open(card.Id, installment.Cycle, installment.Amount.Currency);
                context.MonthlyStatements.Add(statement);
            }
            var lines = new List<PostTransactionLine> {
                new(card.ExpenseAccountId, DebitOrCredit.Debit, installment.Amount),
                new(card.LiabilityAccountId, DebitOrCredit.Credit, installment.Amount)
            };
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
                logger.LogWarning(
                    "Skipping accrual of installment {InstallmentId}: ledger post failed ({ErrorCode}).",
                    installment.Id, posting.Error.Code
                );
                continue;
            }
            var accrual = statement.Accrue(installment);
            if(accrual.IsFailure) {
                logger.LogWarning(
                    "Skipping accrual of installment {InstallmentId}: {ErrorCode}.",
                    installment.Id, accrual.Error.Code
                );
                continue;
            }
            var marked = installment.MarkAccrued(now, statement);
            if(marked.IsFailure) {
                logger.LogWarning(
                    "Skipping accrual of installment {InstallmentId}: {ErrorCode}.",
                    installment.Id, marked.Error.Code
                );
                continue;
            }
            await context.SaveChangesAsync(cancellationToken);
            await dispatcher.DispatchAsync(
                new InstallmentAccruedIntegrationEvent(
                    Guid.CreateVersion7(),
                    now,
                    installment.Id,
                    card.Id,
                    statement.Id,
                    installment.Amount.MinorUnits
                ),
                cancellationToken
            );
        }
    }

    private static async Task accrueDueSplitReceivablesAsync(
        FinancingDbContext context, ILedgerApi ledger, IPartiesApi parties,
        ILogger logger, DateTimeOffset now, DateOnly today, CancellationToken cancellationToken) {
        var pending = await (
            from installment in context.Set<Installment>()
            where installment.AccruedOnUtc != null
            where installment.SplitAccruedOnUtc == null
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CardId != null
            where plan.SplitReferenceId != null
            from card in context.CreditCards.Where(candidate => candidate.Id == plan.CardId)
            select new { installment, card, plan }
        ).ToListAsync(cancellationToken);
        if(pending.Count == 0) {
            return;
        }
        var currentCycleOrdinal = (today.Year * 12) + today.Month;
        foreach(var row in pending) {
            var installment = row.installment;
            var card = row.card;
            var plan = row.plan;
            if(installment.SplitAccruedOnUtc is not null) {
                continue;
            }
            var dueCycle = installment.DueCycle;
            if((dueCycle.Year * 12) + dueCycle.Month > currentCycleOrdinal) {
                continue;
            }
            var splitParticipants = await context.Set<PaymentPlanSplitParticipant>()
                .Where(participant => participant.PaymentPlanId == plan.Id)
                .OrderBy(participant => participant.PartyId)
                .ToListAsync(cancellationToken);
            var (lines, partyPortion) = buildSplitReceivableLines(installment, card, splitParticipants);
            if(partyPortion.MinorUnits <= 0) {
                installment.MarkSplitAccrued(now);
                await context.SaveChangesAsync(cancellationToken);
                continue;
            }
            var posting = await ledger.PostTransactionAsync(
                new PostTransactionCommand(
                    lines,
                    now,
                    SplitReferenceId: plan.SplitReferenceId,
                    Description: "Split receivable — due month"
                ),
                cancellationToken
            );
            if(posting.IsFailure) {
                logger.LogWarning(
                    "Skipping due-month split accrual of installment {InstallmentId}: ledger post failed ({ErrorCode}).",
                    installment.Id, posting.Error.Code
                );
                continue;
            }
            var marked = installment.MarkSplitAccrued(now);
            if(marked.IsFailure) {
                logger.LogWarning(
                    "Skipping due-month split accrual of installment {InstallmentId}: {ErrorCode}.",
                    installment.Id, marked.Error.Code
                );
                continue;
            }
            await context.SaveChangesAsync(cancellationToken);
            var splitAccrual = await parties.RecordSplitAccrualAsync(
                new RecordSplitAccrualCommand(plan.SplitReferenceId!.Value, partyPortion.MinorUnits),
                cancellationToken
            );
            if(splitAccrual.IsFailure) {
                logger.LogWarning(
                    "Split accrual metadata not recorded for installment {InstallmentId} (split {SplitReferenceId}): {ErrorCode}.",
                    installment.Id, plan.SplitReferenceId, splitAccrual.Error.Code
                );
            }
        }
    }

    private static async Task accrueDueCreditorSplitReceivablesAsync(
        FinancingDbContext context, ILedgerApi ledger, IPartiesApi parties,
        ILogger logger, DateTimeOffset now, DateOnly today, CancellationToken cancellationToken) {
        var pending = await (
            from installment in context.Set<Installment>()
            where installment.SplitAccruedOnUtc == null
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CardId == null
            where plan.SplitReferenceId != null
            where plan.CreditorPayableAccountId != null
            select new { installment, plan }
        ).ToListAsync(cancellationToken);
        if(pending.Count == 0) {
            return;
        }
        var currentCycleOrdinal = (today.Year * 12) + today.Month;
        foreach(var row in pending) {
            var installment = row.installment;
            var plan = row.plan;
            if(installment.SplitAccruedOnUtc is not null) {
                continue;
            }
            var dueCycle = installment.DueCycle;
            if((dueCycle.Year * 12) + dueCycle.Month > currentCycleOrdinal) {
                continue;
            }
            var splitParticipants = await context.Set<PaymentPlanSplitParticipant>()
                .Where(participant => participant.PaymentPlanId == plan.Id)
                .OrderBy(participant => participant.PartyId)
                .ToListAsync(cancellationToken);
            var (lines, partyPortionMinorUnits) = CreditorSplitReceivableCalculator.BuildLines(
                installment.Amount, splitParticipants, plan.CreditorPayableAccountId!.Value
            );
            if(partyPortionMinorUnits <= 0) {
                installment.MarkSplitAccrued(now);
                await context.SaveChangesAsync(cancellationToken);
                continue;
            }
            var posting = await ledger.PostTransactionAsync(
                new PostTransactionCommand(
                    lines,
                    now,
                    SplitReferenceId: plan.SplitReferenceId,
                    Description: "Creditor-financed split accrual"
                ),
                cancellationToken
            );
            if(posting.IsFailure) {
                logger.LogWarning(
                    "Skipping due-month creditor-split accrual of installment {InstallmentId}: ledger post failed ({ErrorCode}).",
                    installment.Id, posting.Error.Code
                );
                continue;
            }
            var marked = installment.MarkSplitAccrued(now);
            if(marked.IsFailure) {
                logger.LogWarning(
                    "Skipping due-month creditor-split accrual of installment {InstallmentId}: {ErrorCode}.",
                    installment.Id, marked.Error.Code
                );
                continue;
            }
            await context.SaveChangesAsync(cancellationToken);
            var splitAccrual = await parties.RecordSplitAccrualAsync(
                new RecordSplitAccrualCommand(plan.SplitReferenceId!.Value, partyPortionMinorUnits),
                cancellationToken
            );
            if(splitAccrual.IsFailure) {
                logger.LogWarning(
                    "Creditor-split accrual metadata not recorded for installment {InstallmentId} (split {SplitReferenceId}): {ErrorCode}.",
                    installment.Id, plan.SplitReferenceId, splitAccrual.Error.Code
                );
            }
        }
    }

    private static (List<PostTransactionLine> Lines, Money PartyPortion) buildSplitReceivableLines(
        Installment installment, CreditCard card, IReadOnlyList<PaymentPlanSplitParticipant> splitParticipants) {
        long[] weights = [1L, .. splitParticipants.Select(participant => participant.Weight)];
        var shares = new PhantomPennyAllocator().Allocate(installment.Amount, weights);
        var lines = new List<PostTransactionLine>();
        var partyPortion = Money.Zero(installment.Amount.Currency);
        for(var index = 0; index < splitParticipants.Count; index++) {
            var partyShare = shares[index + 1];
            if(partyShare.MinorUnits <= 0) {
                continue;
            }
            lines.Add(new PostTransactionLine(splitParticipants[index].ReceivableAccountId, DebitOrCredit.Debit, partyShare));
            partyPortion += partyShare;
        }
        if(partyPortion.MinorUnits > 0) {
            lines.Add(new PostTransactionLine(card.ExpenseAccountId, DebitOrCredit.Credit, partyPortion));
        }
        return (lines, partyPortion);
    }
}
