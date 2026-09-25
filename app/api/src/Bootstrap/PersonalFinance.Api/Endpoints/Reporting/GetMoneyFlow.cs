using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Reporting.Reports;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Endpoints.Reporting;

public static class GetMoneyFlow {
    public static async Task<Results<Ok<MoneyFlowDto>, ProblemHttpResult>> Handle(string? month, IQueryBus queryBus, CancellationToken cancellationToken) {
        if(string.IsNullOrWhiteSpace(month)) {
            return ProblemResultsHelper.From(new Error("Reporting.MonthRequired", "The month query parameter (YYYY-MM) is required."));
        }
        var response = await queryBus.AskAsync(new MoneyFlowQuery(month), cancellationToken);
        return TypedResults.Ok(response.ToMoneyFlowDto());
    }
}
