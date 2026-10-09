using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Parties.Contracts.Commands;

namespace PersonalFinance.Api.Endpoints.Parties;

public static class PostUndoPartyPurchase {
    public static async Task<Results<NoContent, ProblemHttpResult>> Handle(Guid id, Guid purchaseId, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync(new UndoPartyPurchaseCommand(id, purchaseId), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.NoContent();
    }
}
