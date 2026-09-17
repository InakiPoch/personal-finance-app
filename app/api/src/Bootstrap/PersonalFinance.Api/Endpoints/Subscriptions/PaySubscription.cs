using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Subscriptions.Contracts.Commands;

namespace PersonalFinance.Api.Endpoints.Subscriptions;

public static class PaySubscription {
    public static async Task<Results<Ok<PaySubscriptionResultDto>, ProblemHttpResult>> Handle(Guid id, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync<Guid>(new PaySubscriptionCommand(id), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Ok(result.Value.ToPaySubscriptionResultDto());
    }
}
