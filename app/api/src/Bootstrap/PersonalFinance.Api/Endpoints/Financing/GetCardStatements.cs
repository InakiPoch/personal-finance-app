using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class GetCardStatements {
    public static async Task<Ok<CardStatementsDto>> Handle(Guid id, IQueryBus queryBus, CancellationToken cancellationToken) {
        var statements = await queryBus.AskAsync(new GetCardStatementsQuery(id), cancellationToken);
        return TypedResults.Ok(statements.ToCardStatementsDto(id));
    }
}
