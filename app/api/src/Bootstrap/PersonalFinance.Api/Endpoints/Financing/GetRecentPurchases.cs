using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class GetRecentPurchases {
    public static async Task<Ok<RecentPurchasesDto>> Handle(IQueryBus queryBus, CancellationToken cancellationToken) {
        var purchases = await queryBus.AskAsync(new ListRecentPurchasesQuery(), cancellationToken);
        return TypedResults.Ok(purchases.ToRecentPurchasesDto());
    }
}
