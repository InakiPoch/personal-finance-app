using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Scheduling;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Commands;

namespace PersonalFinance.Financing.Application.Scheduling;

/// <summary>
/// Card-less creditor-financed counterpart of <see cref="AccrueInstallments"/>. As each installment's owed
/// month arrives, the co-borrowers' shares are posted to the Ledger (<c>Dr Receivable_k … / Cr CreditorPayable</c>)
/// and the split's accrued-receivable total is advanced. There is no billing cycle, no <c>MonthlyStatement</c>,
/// and no card-liability/expense leg — the holder's own share never touches the Ledger (D2/D7). A plain
/// creditor plan (no split) is untouched here.
/// </summary>
internal sealed class AccrueCreditorSplitInstallments(IServiceScopeFactory scopeFactory, ILogger<AccrueCreditorSplitInstallments> logger) : SchedulerBase(logger) {
    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);
    protected override bool RunOnStartup => true;

    protected override async Task TickAsync(CancellationToken cancellationToken) {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinancingDbContext>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var parties = scope.ServiceProvider.GetRequiredService<IPartiesApi>();
        var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<AccrueCreditorSplitInstallments>>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var currentMonthOrdinal = (today.Year * 12) + today.Month;
        var pending = await (
            from installment in context.Set<Installment>()
            where installment.AccruedOnUtc == null
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CardId == null
            where plan.SplitReferenceId != null
            where plan.CreditorPayableAccountId != null
            select new { installment, plan }
        ).ToListAsync(cancellationToken);
        foreach(var row in pending) {
            var installment = row.installment;
            var plan = row.plan;
            if(installment.AccruedOnUtc is not null) {
                continue;
            }
            if((installment.CycleYear * 12) + installment.CycleMonth > currentMonthOrdinal) {
                continue;
            }
            var participants = await context.Set<PaymentPlanSplitParticipant>()
                .Where(participant => participant.PaymentPlanId == plan.Id)
                .OrderBy(participant => participant.PartyId)
                .ToListAsync(cancellationToken);
            var (lines, partyPortionMinorUnits) = CreditorSplitAccrualCalculator.BuildLines(
                installment.Amount, participants, plan.CreditorPayableAccountId!.Value
            );
            if(partyPortionMinorUnits > 0) {
                var posting = await ledger.PostTransactionAsync(
                    new PostTransactionCommand(
                        lines,
                        now,
                        SplitReferenceId: plan.SplitReferenceId,
                        InstallmentReferenceId: installment.Id,
                        Description: "Creditor-financed split accrual"
                    ),
                    cancellationToken
                );
                if(posting.IsFailure) {
                    scopedLogger.LogWarning(
                        "Skipping creditor-split accrual of installment {InstallmentId}: ledger post failed ({ErrorCode}).",
                        installment.Id, posting.Error.Code
                    );
                    continue;
                }
            }
            var marked = installment.MarkCreditorAccrued(now);
            if(marked.IsFailure) {
                scopedLogger.LogWarning(
                    "Skipping creditor-split accrual of installment {InstallmentId}: {ErrorCode}.",
                    installment.Id, marked.Error.Code
                );
                continue;
            }
            await context.SaveChangesAsync(cancellationToken);
            if(partyPortionMinorUnits <= 0) continue;
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
    }
}
