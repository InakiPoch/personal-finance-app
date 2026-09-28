using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorExpense;

/// <summary>
/// Pays one creditor-financed purchase's installments in <see cref="Installment.Sequence"/> order via <see cref="CreditorPaymentWaterfall"/>
/// </summary>
internal sealed class PayCreditorExpenseHandler(FinancingDbContext context, TimeProvider timeProvider) : ICommandHandler<PayCreditorExpenseCommand, int> {
    public async Task<Result<int>> HandleAsync(PayCreditorExpenseCommand command, CancellationToken cancellationToken) {
        var validation = PayCreditorExpenseValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var plan = await context.PaymentPlans
            .FirstOrDefaultAsync(candidate => candidate.Id == command.PaymentPlanId, cancellationToken);
        if(plan is null) {
            return FinancingErrors.PaymentPlanNotFound;
        }
        if(plan.CreditorId is null) {
            return FinancingErrors.NotACreditorInstallment;
        }
        var installments = await context.Set<Installment>()
            .Include(candidate => candidate.Payments)
            .Where(candidate => candidate.PaymentPlanId == plan.Id)
            .OrderBy(candidate => candidate.Sequence)
            .ToListAsync(cancellationToken);
        var candidates = installments.Where(candidate => !candidate.IsReversed && candidate.RemainingMinorUnits > 0).ToList();
        if(candidates.Count == 0) {
            return 0;
        }
        var totalRemaining = candidates.Sum(candidate => candidate.RemainingMinorUnits);
        var amount = command.AmountMinorUnits ?? totalRemaining;
        if(amount <= 0) {
            return FinancingErrors.InvalidPaymentAmount;
        }
        if(amount > totalRemaining) {
            return FinancingErrors.PaymentExceedsRemaining;
        }
        var byId = candidates.ToDictionary(candidate => candidate.Id);
        var allocation = CreditorPaymentWaterfall.Allocate(
            candidates.Select(candidate => (candidate.Id, candidate.RemainingMinorUnits)), amount);
        var now = timeProvider.GetUtcNow();
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
