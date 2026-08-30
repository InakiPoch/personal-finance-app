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

namespace PersonalFinance.Financing.Application.Scheduling;

/// <summary>
/// Once a billing cycle has closed, each of its un-accrued installments is posted to the ledger (Dr card expense / Cr card liability) and moved onto a monthly statement.
/// </summary>
internal sealed class AccrueInstallments(IServiceScopeFactory scopeFactory, ILogger<AccrueInstallments> logger) : SchedulerBase(logger) {
    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);
    protected override bool RunOnStartup => true;

    protected override async Task TickAsync(CancellationToken cancellationToken) {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinancingDbContext>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
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
            select new { installment, card }
        ).ToListAsync(cancellationToken);
        foreach(var row in pending) {
            var installment = row.installment;
            var card = row.card;
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
            var posting = await ledger.PostTransactionAsync(
                new PostTransactionCommand(
                    [
                        new PostTransactionLine(card.ExpenseAccountId, DebitOrCredit.Debit, installment.Amount),
                        new PostTransactionLine(card.LiabilityAccountId, DebitOrCredit.Credit, installment.Amount)
                    ],
                    now,
                    InstallmentReferenceId: installment.Id
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
}
