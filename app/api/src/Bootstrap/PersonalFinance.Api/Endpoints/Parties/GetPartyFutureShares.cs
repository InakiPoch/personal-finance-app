using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Parties.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Parties;

public static class GetPartyFutureShares {
    public static async Task<Ok<FuturePartySharesDto>> Handle(Guid id, string? side, IFinancingApi financing, IQueryBus queryBus, CancellationToken cancellationToken) {
        if(side == "payable") {
            var scheduled = await queryBus.AskAsync(new GetPartyScheduledInstallmentsQuery(id), cancellationToken);
            return TypedResults.Ok(scheduled.ToFuturePartySharesDto());
        }
        var response = await financing.GetFuturePartySharesAsync(new GetFuturePartySharesQuery(id), cancellationToken);
        return TypedResults.Ok(response.ToFuturePartySharesDto());
    }
}
