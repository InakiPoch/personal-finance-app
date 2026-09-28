using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.UnpayCreditorInstallment;

/// <summary>
/// Undoes the last payment recorded against a creditor installment.
/// </summary>
internal sealed class UnpayCreditorInstallmentHandler(FinancingDbContext context, TimeProvider timeProvider, ILedgerApi ledgerApi) : ICommandHandler<UnpayCreditorInstallmentCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(UnpayCreditorInstallmentCommand command, CancellationToken cancellationToken) {
        var validation = UnpayCreditorInstallmentValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var installment = await context.Set<Installment>()
            .Include(candidate => candidate.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.InstallmentId, cancellationToken);
        if(installment is null) {
            return FinancingErrors.InstallmentNotFound;
        }
        var plan = await context.PaymentPlans
            .FirstOrDefaultAsync(candidate => candidate.Id == installment.PaymentPlanId, cancellationToken);
        if(plan is null || plan.CreditorId is null) {
            return FinancingErrors.NotACreditorInstallment;
        }
        if(installment.IsReversed) {
            return FinancingErrors.InstallmentAlreadyReversed;
        }
        var undone = installment.UndoLastPayment();
        if(undone.IsFailure) {
            return undone.Error;
        }
        if(undone.Value.SettlementTransactionId is { } settlementTransactionId) {
            var reversed = await ledgerApi.ReverseTransactionAsync(new ReverseTransactionCommand(settlementTransactionId, timeProvider.GetUtcNow()), cancellationToken);
            if(reversed.IsFailure) {
                return reversed.Error;
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        return installment.Id;
    }
}
