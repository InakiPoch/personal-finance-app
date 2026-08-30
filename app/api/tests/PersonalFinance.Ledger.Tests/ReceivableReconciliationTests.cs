using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public class ReceivableReconciliationTests {
    private static readonly DateTimeOffset postedOn = new(2026, 8, 29, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Receivable_shaped_transaction_balances_and_reconciles_the_outflow() {
        var expenseAccountId = Guid.CreateVersion7();
        var receivableAccountId = Guid.CreateVersion7();
        var bankAccountId = Guid.CreateVersion7();
        var ownShare = Money.FromMinorUnits(500, Currency.Reference);
        var receivableShare = Money.FromMinorUnits(500, Currency.Reference);
        EntryDraft[] lines = [
            new(expenseAccountId, DebitOrCredit.Debit, ownShare),
            new(receivableAccountId, DebitOrCredit.Debit, receivableShare),
            new(bankAccountId, DebitOrCredit.Credit, ownShare + receivableShare)
        ];
        var result = Transaction.Post(lines, postedOn);
        Assert.True(result.IsSuccess);
        var balances = result.Value.Entries
            .GroupBy(entry => entry.AccountId)
            .ToDictionary(group => group.Key, signedMinorUnits);

        Assert.Equal(-1000, balances[bankAccountId]);
        Assert.Equal(500, balances[expenseAccountId]);
        Assert.Equal(500, balances[receivableAccountId]);
        Assert.Equal(-balances[bankAccountId], balances[expenseAccountId] + balances[receivableAccountId]);
    }

    private static long signedMinorUnits(IEnumerable<Entry> entries) {
        return entries.Sum(entry => entry.Direction == DebitOrCredit.Debit ? entry.Amount.MinorUnits : -entry.Amount.MinorUnits);
    }
}
