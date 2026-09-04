using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Ledger;

public static class GetTransactions {
    public static async Task<Ok<TransactionFeedDto>> Handle(Guid? accountId, string? from, string? to, IQueryBus queryBus, CancellationToken cancellationToken) {
        var feed = await queryBus.AskAsync(accountId.ToGetTransactionsQuery(from, to), cancellationToken);
        return TypedResults.Ok(feed.ToTransactionFeedDto());
    }
}
