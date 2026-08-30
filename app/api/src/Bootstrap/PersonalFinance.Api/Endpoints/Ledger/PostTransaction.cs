using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Ledger;

public static class PostTransaction {
    public static async Task<Results<Ok<PostTransactionResultDto>, BadRequest<ProblemDetails>>> Handle(PostTransactionDto body, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync<Guid>(body.ToPostTransactionCommand(), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Ok(result.Value.ToPostTransactionResultDto());
    }
}
