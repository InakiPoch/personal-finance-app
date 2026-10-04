using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Subscriptions.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Subscriptions;

public static class GetSubscriptionsByMonth {
    public static async Task<Ok<SubscriptionsByMonthDto>> Handle(string month, IQueryBus queryBus, CancellationToken cancellationToken) {
        var response = await queryBus.AskAsync(new GetSubscriptionsByMonthQuery(MonthQueryHelper.Parse(month)), cancellationToken);
        return TypedResults.Ok(response.ToSubscriptionsByMonthDto());
    }
}
