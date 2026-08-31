using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Subscriptions.Contracts.Commands;

namespace PersonalFinance.Api.Endpoints.Subscriptions;

public static class DeleteSubscription {
    public static async Task<Results<NoContent, BadRequest<ProblemDetails>>> Handle(Guid id, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync(new CancelSubscriptionCommand(id), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.NoContent();
    }
}
