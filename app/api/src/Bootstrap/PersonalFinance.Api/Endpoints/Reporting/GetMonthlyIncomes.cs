using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Reporting.Dashboards;

namespace PersonalFinance.Api.Endpoints.Reporting;

public static class GetMonthlyIncomes {
    public static async Task<Ok<MonthlyIncomesDto>> Handle(string? month, IQueryBus queryBus, CancellationToken cancellationToken) {
        var response = await queryBus.AskAsync(new MonthlyIncomesQuery(month), cancellationToken);
        return TypedResults.Ok(response.ToMonthlyIncomesDto());
    }
}
