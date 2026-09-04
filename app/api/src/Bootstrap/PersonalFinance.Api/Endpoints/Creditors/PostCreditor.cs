using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts;

namespace PersonalFinance.Api.Endpoints.Creditors;

public static class PostCreditor {
    public static async Task<Results<Created<CreditorResultDto>, ProblemHttpResult>> Handle(CreateCreditorDto body, IFinancingApi financing, CancellationToken cancellationToken) {
        var result = await financing.CreateCreditorAsync(body.ToCreateCreditorCommand(), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Created((string?)null, result.Value.ToCreditorResultDto());
    }
}
