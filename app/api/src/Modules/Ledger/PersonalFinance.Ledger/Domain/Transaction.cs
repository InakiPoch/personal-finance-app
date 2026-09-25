using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Domain.Events;
using PersonalFinance.Ledger.Domain.Rules;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Domain;

internal sealed class Transaction : AggregateRoot<Guid> {
    public DateTimeOffset PostedOnUtc { get; }
    public Guid? OriginalTransactionId { get; }
    public bool IsReversal => OriginalTransactionId is not null;
    public SplitReference? SplitReference { get; private set; }
    public InstallmentReference? InstallmentReference { get; private set; }
    public SubscriptionReference? SubscriptionReference { get; private set; }
    public IReadOnlyCollection<Entry> Entries => entries;
    private readonly List<Entry> entries = [];

    private Transaction(Guid id, DateTimeOffset postedOnUtc, Guid? originalTransactionId) : base(id) {
        PostedOnUtc = postedOnUtc;
        OriginalTransactionId = originalTransactionId;
    }

    public static Result<Transaction> Post(
        IReadOnlyList<EntryDraft> lines,
        DateTimeOffset postedOnUtc,
        SplitReference? splitReference = null,
        InstallmentReference? installmentReference = null,
        SubscriptionReference? subscriptionReference = null) {
        var check = DoubleEntryMustBalance.Check(lines);
        if(check.IsFailure) {
            return check.Error;
        }
        return build(lines, postedOnUtc, originalTransactionId: null, splitReference, installmentReference, subscriptionReference);
    }

    public static Result<Transaction> Reverse(Transaction original, DateTimeOffset reversedOnUtc) {
        if(original.OriginalTransactionId is not null) {
            return LedgerErrors.CannotReverseAReversal;
        }
        var mirrored = original.entries
            .Select(entry => new EntryDraft(entry.AccountId, flip(entry.Direction), entry.Amount))
            .ToList();
        var check = DoubleEntryMustBalance.Check(mirrored);
        if(check.IsFailure) {
            return check.Error;
        }
        return build(mirrored, reversedOnUtc, original.Id, original.SplitReference, original.InstallmentReference, original.SubscriptionReference);
    }

    private static Transaction build(
        IReadOnlyList<EntryDraft> lines,
        DateTimeOffset postedOnUtc,
        Guid? originalTransactionId,
        SplitReference? splitReference,
        InstallmentReference? installmentReference,
        SubscriptionReference? subscriptionReference) {
        var transaction = new Transaction(Guid.CreateVersion7(), postedOnUtc, originalTransactionId) {
            SplitReference = splitReference,
            InstallmentReference = installmentReference,
            SubscriptionReference = subscriptionReference
        };
        foreach(var line in lines) {
            transaction.entries.Add(new Entry(Guid.CreateVersion7(), line.AccountId, line.Direction, line.Amount.MinorUnits, line.Amount.Currency));
        }
        transaction.RaiseDomainEvent(new TransactionPosted(transaction.Id, postedOnUtc, originalTransactionId is not null));
        return transaction;
    }

    private static DebitOrCredit flip(DebitOrCredit direction) {
        return direction == DebitOrCredit.Debit ? DebitOrCredit.Credit : DebitOrCredit.Debit;
    }
}
