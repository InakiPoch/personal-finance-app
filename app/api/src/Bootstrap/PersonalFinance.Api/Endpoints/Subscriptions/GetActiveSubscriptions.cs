using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Subscriptions.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Subscriptions;

public static class GetActiveSubscriptions {
    public static async Task<Ok<ActiveSubscriptionsDto>> Handle(IQueryBus queryBus, CancellationToken cancellationToken) {
        var active = await queryBus.AskAsync(new GetActiveSubscriptionsQuery(), cancellationToken);
        return TypedResults.Ok(active.ToActiveSubscriptionsDto());
    }
}
