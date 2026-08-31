using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Reporting.Dashboards;

namespace PersonalFinance.Api.Endpoints.Reporting;

public static class GetMonthlyExpenses {
    public static async Task<Ok<MonthlyExpensesDto>> Handle(string? month, IQueryBus queryBus, CancellationToken cancellationToken) {
        var response = await queryBus.AskAsync(new MonthlyExpensesQuery(month), cancellationToken);
        return TypedResults.Ok(response.ToMonthlyExpensesDto());
    }
}
