using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class GetCreditorDetail {
    public static async Task<Results<Ok<CreditorDetailDto>, ProblemHttpResult>> Handle(Guid creditorId, IQueryBus queryBus, CancellationToken cancellationToken) {
        var response = await queryBus.AskAsync(new GetCreditorDetailQuery(creditorId), cancellationToken);
        if(!response.Found) {
            return ProblemResultsHelper.From(new Error("Financing.CreditorNotFound", "The referenced creditor was not found."));
        }
        return TypedResults.Ok(response.ToCreditorDetailDto());
    }
}
