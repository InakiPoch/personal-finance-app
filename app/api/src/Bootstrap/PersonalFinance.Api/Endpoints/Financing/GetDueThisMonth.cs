using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class GetDueThisMonth {
    public static async Task<Ok<DueThisMonthDto>> Handle(IQueryBus queryBus, CancellationToken cancellationToken) {
        var response = await queryBus.AskAsync(new GetDueThisMonthQuery(), cancellationToken);
        return TypedResults.Ok(response.ToDueThisMonthDto());
    }
}
