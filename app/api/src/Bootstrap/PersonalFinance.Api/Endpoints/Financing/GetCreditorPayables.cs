using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class GetCreditorPayables {
    public static async Task<Ok<CreditorPayablesDto>> Handle(IQueryBus queryBus, CancellationToken cancellationToken) {
        var response = await queryBus.AskAsync(new GetCreditorPayablesQuery(), cancellationToken);
        return TypedResults.Ok(response.ToCreditorPayablesDto());
    }
}
