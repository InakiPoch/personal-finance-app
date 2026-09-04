using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class GetCardPurchases {
    public static async Task<Ok<CardPurchasesDto>> Handle(Guid id, IQueryBus queryBus, CancellationToken cancellationToken) {
        var purchases = await queryBus.AskAsync(new GetCardPurchasesQuery(id), cancellationToken);
        return TypedResults.Ok(purchases.ToCardPurchasesDto());
    }
}
