using PersonalFinance.Financing.Domain;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands;

/// <summary>
/// Posts the Bank/Cash outflow of a creditor pay action: Dr "Creditor payments" expense / Cr the Paid from account.
/// </summary>
internal static class CreditorPostingHelper {
    internal const string CreditorPaymentsCategory = "Creditor payments";

    public static async Task<Result> ValidatePaidFromAsync(ILedgerApi ledgerApi, Guid bankAccountId, CancellationToken cancellationToken) {
        var instruments = await ledgerApi.ListInstrumentAccountsAsync(new ListInstrumentAccountsQuery(), cancellationToken);
        return instruments.Rows.Any(row => row.AccountId == bankAccountId) ? Result.Success() : Result.Failure(FinancingErrors.InvalidPaidFromAccount);
    }

    public static async Task<Result<Guid>> PostAsync(ILedgerApi ledgerApi, Guid bankAccountId, Money amount, string description, DateTimeOffset now, CancellationToken cancellationToken) {
        var category = await ledgerApi.GetOrCreateExpenseCategoryAsync(new GetOrCreateExpenseCategoryCommand(CreditorPaymentsCategory), cancellationToken);
        if(category.IsFailure) {
            return category.Error;
        }
        return await ledgerApi.PostTransactionAsync(
            new PostTransactionCommand(
                [
                    new PostTransactionLine(category.Value, DebitOrCredit.Debit, amount),
                    new PostTransactionLine(bankAccountId, DebitOrCredit.Credit, amount)
                ],
                now,
                Description: description
            ),
            cancellationToken
        );
    }

    /// <summary>
    /// Posts one transaction per currency for the pieces of a waterfall action and stamps its id on every piece's payment row.
    /// This is the single place that decides how much of a piece moves the bank.
    /// </summary>
    public static async Task<Result<IReadOnlyList<Guid>>> PostPiecesAsync(ILedgerApi ledgerApi, Guid bankAccountId, IReadOnlyList<(Installment Installment, CreditorInstallmentPayment Payment)> pieces, string description, DateTimeOffset now, CancellationToken cancellationToken) {
        var posted = new List<Guid>();
        foreach(var group in pieces.GroupBy(piece => piece.Installment.Currency)) {
            var amount = Money.FromMinorUnits(group.Sum(piece => piece.Payment.AmountMinorUnits), group.Key);
            var result = await PostAsync(ledgerApi, bankAccountId, amount, description, now, cancellationToken);
            if(result.IsFailure) {
                await ReverseAsync(ledgerApi, posted, now);
                return result.Error;
            }
            posted.Add(result.Value);
            foreach(var piece in group) {
                piece.Payment.RecordLedgerTransaction(result.Value);
            }
        }
        return posted;
    }

    public static async Task ReverseAsync(ILedgerApi ledgerApi, IEnumerable<Guid> transactionIds, DateTimeOffset now) {
        foreach(var transactionId in transactionIds) {
            await ledgerApi.ReverseTransactionAsync(new ReverseTransactionCommand(transactionId, now), CancellationToken.None);
        }
    }
}
