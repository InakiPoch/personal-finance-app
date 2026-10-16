using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorInstallment;

/// <summary>
/// Records a payment (full or partial) against one creditor-financed installment. A plan without a party
/// split also moves the Paid from Bank/Cash account: Dr "Creditor payments" expense / Cr the account (ADR 0018).
/// An installment on a credit-card plan is rejected with <see cref="FinancingErrors.NotACreditorInstallment"/>.
/// </summary>
internal sealed class PayCreditorInstallmentHandler(FinancingDbContext context, TimeProvider timeProvider, ILedgerApi ledgerApi) : ICommandHandler<PayCreditorInstallmentCommand, Guid> {
    internal const string CreditorPaymentsCategory = "Creditor payments";

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
        // ponytail: split plans stay display-only until the split-allocation ticket (#61) decides what moves the bank.
        var movesBank = plan.SplitReferenceId is null;
        if(movesBank) {
            var instruments = await ledgerApi.ListInstrumentAccountsAsync(new ListInstrumentAccountsQuery(), cancellationToken);
            if(instruments.Rows.All(row => row.AccountId != command.BankAccountId)) {
                return FinancingErrors.InvalidPaidFromAccount;
            }
        }
        var now = timeProvider.GetUtcNow();
        var applied = installment.ApplyPayment(command.AmountMinorUnits ?? installment.RemainingMinorUnits, now);
        if(applied.IsFailure) {
            return applied.Error;
        }
        if(movesBank) {
            var posted = await PostPaymentAsync(plan, installment, applied.Value, command.BankAccountId, now, cancellationToken);
            if(posted.IsFailure) {
                return posted.Error;
            }
            applied.Value.RecordLedgerTransaction(posted.Value);
        }
        try {
            await context.SaveChangesAsync(cancellationToken);
        } catch {
            if(applied.Value.LedgerTransactionId is { } ledgerTransactionId) {
                await ledgerApi.ReverseTransactionAsync(new ReverseTransactionCommand(ledgerTransactionId, now), CancellationToken.None);
            }
            throw;
        }
        return installment.Id;
    }

    private async Task<Result<Guid>> PostPaymentAsync(PaymentPlan plan, Installment installment, CreditorInstallmentPayment payment, Guid bankAccountId, DateTimeOffset now, CancellationToken cancellationToken) {
        var category = await ledgerApi.GetOrCreateExpenseCategoryAsync(new GetOrCreateExpenseCategoryCommand(CreditorPaymentsCategory), cancellationToken);
        if(category.IsFailure) {
            return category.Error;
        }
        var creditorName = await context.Creditors
            .Where(candidate => candidate.Id == plan.CreditorId)
            .Select(candidate => candidate.Name)
            .FirstAsync(cancellationToken);
        var amount = Money.FromMinorUnits(payment.AmountMinorUnits, installment.Currency);
        return await ledgerApi.PostTransactionAsync(
            new PostTransactionCommand(
                [
                    new PostTransactionLine(category.Value, DebitOrCredit.Debit, amount),
                    new PostTransactionLine(bankAccountId, DebitOrCredit.Credit, amount)
                ],
                now,
                Description: $"Installment {installment.Sequence}/{plan.InstallmentCount} · {plan.Description} · {creditorName}"
            ),
            cancellationToken
        );
    }
}
