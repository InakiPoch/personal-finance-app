using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Parties;

public static class GetPartyFutureShares {
    public static async Task<Ok<FuturePartySharesDto>> Handle(Guid id, IFinancingApi financing, CancellationToken cancellationToken) {
        var response = await financing.GetFuturePartySharesAsync(new GetFuturePartySharesQuery(id), cancellationToken);
        return TypedResults.Ok(response.ToFuturePartySharesDto());
    }
}
