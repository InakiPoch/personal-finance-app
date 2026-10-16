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
/// Records a payment (full or partial) against one creditor-financed installment. Moves the Paid from Bank/Cash account (ADR 0018): on a party split
/// my share goes to "Creditor payments" first and only the remainder fronts the parties' unpaid shares.
/// An installment on a credit-card plan is rejected with <see cref="FinancingErrors.NotACreditorInstallment"/>.
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
        var posted = await PostPaymentAsync(plan, installment, applied.Value, command.BankAccountId, now, cancellationToken);
        if(posted.IsFailure) {
            return posted.Error;
        }
        try {
            await context.SaveChangesAsync(cancellationToken);
        } catch {
            await CreditorPostingHelper.ReverseAsync(ledgerApi, posted.Value, now);
            throw;
        }
        return installment.Id;
    }

    private async Task<Result<IReadOnlyList<Guid>>> PostPaymentAsync(PaymentPlan plan, Installment installment, CreditorInstallmentPayment payment, Guid bankAccountId, DateTimeOffset now, CancellationToken cancellationToken) {
        var creditorName = await context.Creditors
            .Where(candidate => candidate.Id == plan.CreditorId)
            .Select(candidate => candidate.Name)
            .FirstAsync(cancellationToken);
        var splits = await CreditorPostingHelper.LoadSplitsAsync(context, [plan], cancellationToken);
        return await CreditorPostingHelper.PostPiecesAsync(ledgerApi, bankAccountId, [(installment, payment)], splits, $"Installment {installment.Sequence}/{plan.InstallmentCount} · {plan.Description} · {creditorName}", now, cancellationToken);
    }
}
