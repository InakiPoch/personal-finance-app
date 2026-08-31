using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Reporting.Dashboards;

namespace PersonalFinance.Api.Endpoints.Reporting;

public static class GetCardDueByMonth {
    public static async Task<Ok<CardDueByMonthDto>> Handle(IQueryBus queryBus, CancellationToken cancellationToken) {
        var response = await queryBus.AskAsync(new CardDueByMonthQuery(), cancellationToken);
        return TypedResults.Ok(response.ToCardDueByMonthDto());
    }
}
