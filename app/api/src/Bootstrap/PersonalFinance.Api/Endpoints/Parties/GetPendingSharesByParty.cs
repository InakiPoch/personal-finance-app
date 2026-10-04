using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Parties;

public static class GetPendingSharesByParty {
    public static async Task<Ok<PendingSharesByPartyDto>> Handle(IFinancingApi financing, CancellationToken cancellationToken) {
        var response = await financing.GetPendingSharesByPartyAsync(new GetPendingSharesByPartyQuery(), cancellationToken);
        return TypedResults.Ok(response.ToPendingSharesByPartyDto());
    }
}
