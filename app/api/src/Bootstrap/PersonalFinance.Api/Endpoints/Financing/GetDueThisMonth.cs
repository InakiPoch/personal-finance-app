using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class GetDueThisMonth {
    public static async Task<Ok<DueThisMonthDto>> Handle(string? month, DateOnly? today, IQueryBus queryBus, CancellationToken cancellationToken) {
        var requested = month is null ? (DateOnly?)null : MonthQueryHelper.Parse(month);
        var response = await queryBus.AskAsync(new GetDueThisMonthQuery(requested, today), cancellationToken);
        return TypedResults.Ok(response.ToDueThisMonthDto());
    }
}
