using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorFullDebt;

/// <summary>
/// Settles a creditor's debt in one transaction. A null amount pays every unpaid, non-reversed
/// installment across all of that creditor's purchases in full. A set amount fills only that currency's remaining installments
/// </summary>
internal sealed class PayCreditorFullDebtHandler(FinancingDbContext context, TimeProvider timeProvider) : ICommandHandler<PayCreditorFullDebtCommand, int> {
    public async Task<Result<int>> HandleAsync(PayCreditorFullDebtCommand command, CancellationToken cancellationToken) {
        var validation = PayCreditorFullDebtValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var creditorExists = await context.Creditors
            .AnyAsync(candidate => candidate.Id == command.CreditorId, cancellationToken);
        if(creditorExists == false) {
            return FinancingErrors.CreditorNotFound;
        }
        var installments = await (
            from installment in context.Set<Installment>().Include(candidate => candidate.Payments)
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CreditorId == command.CreditorId
            select new { installment, plan.PurchaseDate }
        ).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        if(command.AmountMinorUnits is null) {
            var settledAll = 0;
            foreach(var row in installments) {
                if(row.installment.IsReversed || row.installment.RemainingMinorUnits == 0) {
                    continue;
                }
                var applied = row.installment.ApplyPayment(row.installment.RemainingMinorUnits, now);
                if(applied.IsFailure) {
                    return applied.Error;
                }
                settledAll++;
            }
            await context.SaveChangesAsync(cancellationToken);
            return settledAll;
        }
        var candidates = installments
            .Where(row => !row.installment.IsReversed && row.installment.RemainingMinorUnits > 0 && row.installment.Currency.Code == command.CurrencyCode)
            .OrderBy(row => row.installment.DueCycle.Year)
            .ThenBy(row => row.installment.DueCycle.Month)
            .ThenBy(row => row.PurchaseDate)
            .ThenBy(row => row.installment.Sequence)
            .Select(row => row.installment)
            .ToList();
        var totalRemaining = candidates.Sum(candidate => candidate.RemainingMinorUnits);
        var amount = command.AmountMinorUnits.Value;
        if(amount <= 0) {
            return FinancingErrors.InvalidPaymentAmount;
        }
        if(amount > totalRemaining) {
            return FinancingErrors.PaymentExceedsRemaining;
        }
        var byId = candidates.ToDictionary(candidate => candidate.Id);
        var allocation = CreditorPaymentWaterfall.Allocate(
            candidates.Select(candidate => (candidate.Id, candidate.RemainingMinorUnits)), amount);
        var settled = 0;
        foreach(var (installmentId, pieceAmount) in allocation) {
            var installment = byId[installmentId];
            var applied = installment.ApplyPayment(pieceAmount, now);
            if(applied.IsFailure) {
                return applied.Error;
            }
            if(installment.RemainingMinorUnits == 0) {
                settled++;
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        return settled;
    }
}
