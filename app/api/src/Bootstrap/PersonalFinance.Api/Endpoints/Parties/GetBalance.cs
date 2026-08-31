using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Parties.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Parties;

public static class GetBalance {
    public static async Task<Ok<CurrentAccountBalanceDto>> Handle(Guid id, IQueryBus queryBus, CancellationToken cancellationToken) {
        var balance = await queryBus.AskAsync(new GetCurrentAccountBalanceQuery(id), cancellationToken);
        return TypedResults.Ok(balance.ToCurrentAccountBalanceDto());
    }
}
