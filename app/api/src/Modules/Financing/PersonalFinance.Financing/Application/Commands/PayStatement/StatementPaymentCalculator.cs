using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayStatement;

/// <summary>
/// The ledger posting for a statement payment, with any carried card credit netted in.
/// </summary>
/// <param name="Lines">Balanced legs: <c>Dr CardLiability (payable)</c>, <c>Cr Bank (remainder)</c> when the bank still owes anything, <c>Cr CardCredit (credit applied)</c> when credit was used.</param>
/// <param name="CreditApplied">How much carried credit this payment consumes.</param>
internal sealed record StatementPaymentPosting(IReadOnlyList<PostTransactionLine> Lines, Money CreditApplied);

internal static class StatementPaymentCalculator {
    public static StatementPaymentPosting Build(Money carriedCredit, Money payable, Guid liabilityAccountId, Guid bankAccountId, Guid cardCreditAccountId) {
        var creditApplied = carriedCredit <= payable ? carriedCredit : payable;
        var fromBank = payable - creditApplied;
        var lines = new List<PostTransactionLine> {
            new(liabilityAccountId, DebitOrCredit.Debit, payable)
        };
        if(fromBank.MinorUnits > 0) {
            lines.Add(new PostTransactionLine(bankAccountId, DebitOrCredit.Credit, fromBank));
        }
        if(creditApplied.MinorUnits > 0) {
            lines.Add(new PostTransactionLine(cardCreditAccountId, DebitOrCredit.Credit, creditApplied));
        }
        return new StatementPaymentPosting(lines, creditApplied);
    }
}
