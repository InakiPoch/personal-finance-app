using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;

namespace PersonalFinance.Ledger.Application.Queries.GetTransactions;

internal sealed class GetTransactionsHandler(LedgerDbContext context) : IQueryHandler<GetTransactionsQuery, TransactionFeedResponse> {
    public async Task<TransactionFeedResponse> HandleAsync(GetTransactionsQuery query, CancellationToken cancellationToken) {
        var transactions = context.Transactions.Include(transaction => transaction.Entries).AsQueryable();
        if(query.AccountId is { } accountId) {
            transactions = transactions.Where(transaction => transaction.Entries.Any(entry => entry.AccountId == accountId));
        }
        var loaded = await transactions.ToListAsync(cancellationToken);
        var fromUtc = query.FromUtc?.ToDateTime(TimeOnly.MinValue);
        var toExclusiveUtc = query.ToUtc?.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var reversedIds = loaded
            .Where(transaction => transaction.OriginalTransactionId is not null)
            .Select(transaction => transaction.OriginalTransactionId!.Value)
            .ToHashSet();
        var rows = loaded
            .Where(transaction => fromUtc is null || transaction.PostedOnUtc.UtcDateTime >= fromUtc)
            .Where(transaction => toExclusiveUtc is null || transaction.PostedOnUtc.UtcDateTime < toExclusiveUtc)
            .OrderByDescending(transaction => transaction.PostedOnUtc)
            .Select(transaction => new TransactionFeedRow(
                transaction.Id,
                transaction.PostedOnUtc,
                describe(transaction),
                sumDebitMinorUnits(transaction),
                transaction.IsReversal,
                reversedIds.Contains(transaction.Id),
                transaction.InstallmentReference?.Value,
                transaction.SplitReference?.Value))
            .ToList();
        return new TransactionFeedResponse(rows);
    }

    private static long sumDebitMinorUnits(Transaction transaction) {
        return transaction.Entries
            .Where(entry => entry.Direction == DebitOrCredit.Debit)
        .Sum(entry => entry.Amount.MinorUnits);
    }

    private static string describe(Transaction transaction) {
        if(transaction.IsReversal) {
            return "Reversal";
        }
        if(transaction.InstallmentReference is not null) {
            return "Installment accrual";
        }
        if(transaction.SplitReference is not null) {
            return "Shared expense / split";
        }
        if(transaction.SubscriptionReference is not null) {
            return "Subscription charge";
        }
        return "Manual entry";
    }
}
