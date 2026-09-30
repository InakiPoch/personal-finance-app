using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Ledger.Infrastructure.Persistence;

namespace PersonalFinance.Ledger.Application.Queries.FindPaidSubscriptionIds;

/// <summary>
/// A charge counts as paid when an original (non-reversal) transaction tagged with the subscription was posted in the month
/// and no reversal transaction points back at it (storno model, D3).
/// </summary>
internal sealed class FindPaidSubscriptionIdsHandler(LedgerDbContext context) : IQueryHandler<FindPaidSubscriptionIdsQuery, PaidSubscriptionIdsResponse> {
    public async Task<PaidSubscriptionIdsResponse> HandleAsync(FindPaidSubscriptionIdsQuery query, CancellationToken cancellationToken) {
        if(query.SubscriptionIds.Count == 0) {
            return new PaidSubscriptionIdsResponse([]);
        }
        var wanted = query.SubscriptionIds.ToHashSet();
        var tagged = await context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.SubscriptionReference != null)
            .Select(transaction => new { transaction.Id, transaction.OriginalTransactionId, Reference = transaction.SubscriptionReference, transaction.PostedOnUtc })
            .ToListAsync(cancellationToken);
        var reversedIds = tagged
            .Where(transaction => transaction.OriginalTransactionId != null)
            .Select(transaction => transaction.OriginalTransactionId!.Value)
            .ToHashSet();
        var paid = tagged
            .Where(transaction => transaction.OriginalTransactionId == null
                && !reversedIds.Contains(transaction.Id)
                && transaction.PostedOnUtc.UtcDateTime.Year == query.Month.Year
                && transaction.PostedOnUtc.UtcDateTime.Month == query.Month.Month
                && wanted.Contains(transaction.Reference!.Value))
            .Select(transaction => transaction.Reference!.Value)
            .Distinct()
            .ToList();
        return new PaidSubscriptionIdsResponse(paid);
    }
}
