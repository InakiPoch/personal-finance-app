using PersonalFinance.Ledger.Contracts;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Domain.Rules;

/// <summary>
/// Every transaction has at least two entries, a single currency, and Σdebits == Σcredits.
/// </summary>
internal static class DoubleEntryMustBalance {
    public static Result Check(IReadOnlyList<EntryDraft> lines) {
        if(lines.Count < 2) {
            return Result.Failure(LedgerErrors.DegenerateTransaction);
        }
        var currency = lines[0].Amount.Currency;
        if(lines.Any(line => line.Amount.Currency != currency)) {
            return Result.Failure(LedgerErrors.MixedCurrency);
        }
        var debits = Money.Zero(currency);
        var credits = Money.Zero(currency);
        foreach(var line in lines) {
            if(line.Direction == DebitOrCredit.Debit) {
                debits += line.Amount;
            } else {
                credits += line.Amount;
            }
        }
        return debits != credits ? Result.Failure(LedgerErrors.Unbalanced) : Result.Success();
    }
}
