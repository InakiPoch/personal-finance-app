using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorInstallment;

/// <summary>
/// Pays a full or partial amount of one creditor installment from a Bank/Cash account.
/// </summary>
internal sealed class PayCreditorInstallmentHandler(FinancingDbContext context, TimeProvider timeProvider, ILedgerApi ledgerApi) : ICommandHandler<PayCreditorInstallmentCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(PayCreditorInstallmentCommand command, CancellationToken cancellationToken) {
        var validation = PayCreditorInstallmentValidator.Validate(command);
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
        var paidFrom = await CreditorPostingHelper.ValidatePaidFromAsync(ledgerApi, command.BankAccountId, cancellationToken);
        if(paidFrom.IsFailure) {
            return paidFrom.Error;
        }
        var now = timeProvider.GetUtcNow();
        var applied = installment.ApplyPayment(command.AmountMinorUnits ?? installment.RemainingMinorUnits, now);
        if(applied.IsFailure) {
            return applied.Error;
        }
        var posted = await postPaymentAsync(plan, installment, applied.Value, command.BankAccountId, now, cancellationToken);
        if(posted.IsFailure) {
            return posted.Error;
        }
        await CreditorPostingHelper.SaveOrReverseAsync(context, ledgerApi, posted.Value, now, cancellationToken);
        return installment.Id;
    }

    private async Task<Result<IReadOnlyList<Guid>>> postPaymentAsync(PaymentPlan plan, Installment installment, CreditorInstallmentPayment payment, Guid bankAccountId, DateTimeOffset now, CancellationToken cancellationToken) {
        var creditorName = await CreditorPostingHelper.LoadCreditorNameAsync(context, plan.CreditorId, cancellationToken);
        if(creditorName is null) {
            return FinancingErrors.CreditorNotFound;
        }
        var splits = await CreditorPostingHelper.LoadSplitsAsync(context, [plan], cancellationToken);
        return await CreditorPostingHelper.PostPiecesAsync(ledgerApi, bankAccountId, [(installment, payment)], splits, CreditorPaymentDescriptionHelper.ForInstallment(plan, installment, creditorName), now, cancellationToken);
    }
}
