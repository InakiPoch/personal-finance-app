using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Domain.Events;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public class DoubleEntryInvariantTests {
    private static readonly DateTimeOffset postedOn = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
    [Fact]
    public void Post_balanced_two_leg_transaction_succeeds() {
        EntryDraft[] lines = [
            draft(DebitOrCredit.Debit, 1000),
            draft(DebitOrCredit.Credit, 1000)
        ];
        var result = Transaction.Post(lines, postedOn);
        Assert.True(result.IsSuccess);
        var transaction = result.Value;
        Assert.Equal(2, transaction.Entries.Count);
        Assert.Null(transaction.OriginalTransactionId);
        Assert.False(transaction.IsReversal);
        var posted = Assert.Single(transaction.DomainEvents.OfType<TransactionPosted>());
        Assert.Equal(transaction.Id, posted.TransactionId);
        Assert.False(posted.IsReversal);
    }

    [Fact]
    public void Post_unbalanced_transaction_fails_as_unbalanced() {
        EntryDraft[] lines = [
            draft(DebitOrCredit.Debit, 1000),
            draft(DebitOrCredit.Credit, 900)
        ];
        var result = Transaction.Post(lines, postedOn);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.Unbalanced", result.Error.Code);
    }

    [Fact]
    public void Post_transaction_mixing_currencies_fails_as_mixed_currency() {
        var usd = new Currency("USD", 2);
        EntryDraft[] lines = [
            draft(DebitOrCredit.Debit, 1000),
            draft(DebitOrCredit.Credit, 1000, usd)
        ];
        var result = Transaction.Post(lines, postedOn);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.MixedCurrency", result.Error.Code);
    }

    [Fact]
    public void Post_transaction_with_a_single_leg_fails_as_degenerate() {
        EntryDraft[] lines = [draft(DebitOrCredit.Debit, 1000)];
        var result = Transaction.Post(lines, postedOn);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.DegenerateTransaction", result.Error.Code);
    }

    private static EntryDraft draft(DebitOrCredit direction, long minorUnits, Currency? currency = null) {
        return new EntryDraft(Guid.CreateVersion7(), direction, Money.FromMinorUnits(minorUnits, currency ?? Currency.Reference));
    }
}
