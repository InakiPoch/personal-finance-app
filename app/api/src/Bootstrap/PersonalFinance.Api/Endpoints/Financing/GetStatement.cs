using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class GetStatement {
    public static async Task<Results<Ok<MonthlyStatementDetailDto>, ProblemHttpResult>> Handle(Guid id, IQueryBus queryBus, CancellationToken cancellationToken) {
        var response = await queryBus.AskAsync(new GetMonthlyStatementQuery(id), cancellationToken);
        if(!response.Found) {
            return ProblemResultsHelper.From(new Error("Financing.StatementNotFound", "The referenced monthly statement was not found."));
        }
        return TypedResults.Ok(response.ToMonthlyStatementDetailDto());
    }
}
