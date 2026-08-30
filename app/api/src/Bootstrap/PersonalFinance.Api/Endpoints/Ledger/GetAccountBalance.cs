using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Ledger.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Ledger;

public static class GetAccountBalance {
    public static async Task<Ok<AccountBalanceDto>> Handle(Guid id, IQueryBus queryBus, CancellationToken cancellationToken) {
        var balance = await queryBus.AskAsync(new GetAccountBalanceQuery(id), cancellationToken);
        return TypedResults.Ok(balance.ToAccountBalanceDto(id));
    }
}
