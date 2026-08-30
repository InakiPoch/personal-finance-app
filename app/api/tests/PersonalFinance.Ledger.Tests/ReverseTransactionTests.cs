using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public class ReverseTransactionTests {
    private static readonly DateTimeOffset postedOn = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset reversedOn = new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reverse_mirrors_every_entry_and_links_back_without_touching_the_original() {
        var debitAccountId = Guid.CreateVersion7();
        var creditAccountId = Guid.CreateVersion7();
        var amount = Money.FromMinorUnits(1000, Currency.Reference);
        var original = post(debitAccountId, creditAccountId, amount);
        var originalSnapshot = snapshot(original);
        var result = Transaction.Reverse(original, reversedOn);
        Assert.True(result.IsSuccess);
        var reversal = result.Value;
        Assert.Equal(original.Id, reversal.OriginalTransactionId);
        Assert.True(reversal.IsReversal);
        Assert.Equal(reversedOn, reversal.PostedOnUtc);
        Assert.Equal(original.Entries.Count, reversal.Entries.Count);
        foreach(var originalEntry in original.Entries) {
            var mirror = Assert.Single(reversal.Entries, entry => entry.AccountId == originalEntry.AccountId);
            Assert.NotEqual(originalEntry.Direction, mirror.Direction);
            Assert.Equal(originalEntry.Amount, mirror.Amount);
        }
        Assert.Null(original.OriginalTransactionId);
        Assert.Equal(originalSnapshot, snapshot(original));
    }

    [Fact]
    public void Reverse_of_a_reversal_fails() {
        var debitAccountId = Guid.CreateVersion7();
        var creditAccountId = Guid.CreateVersion7();
        var amount = Money.FromMinorUnits(1000, Currency.Reference);
        var original = post(debitAccountId, creditAccountId, amount);
        var reversal = Transaction.Reverse(original, reversedOn).Value;
        var result = Transaction.Reverse(reversal, reversedOn);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.CannotReverseAReversal", result.Error.Code);
    }

    private static Transaction post(Guid debitAccountId, Guid creditAccountId, Money amount) {
        EntryDraft[] lines = [
            new(debitAccountId, DebitOrCredit.Debit, amount),
            new(creditAccountId, DebitOrCredit.Credit, amount)
        ];
        return Transaction.Post(lines, postedOn).Value;
    }

    private static List<(Guid AccountId, DebitOrCredit Direction, Money Amount)> snapshot(Transaction transaction) {
        return transaction.Entries
            .Select(entry => (entry.AccountId, entry.Direction, entry.Amount))
        .ToList();
    }
}
