using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Ledger.Contracts.Commands;

namespace PersonalFinance.Api.Endpoints.Ledger;

public static class ReverseTransaction {
    public static async Task<Results<Ok<ReverseTransactionResultDto>, ProblemHttpResult>> Handle(Guid id, ICommandBus commandBus, CancellationToken cancellationToken) {
        var command = new ReverseTransactionCommand(id, DateTimeOffset.UtcNow);
        var result = await commandBus.SendAsync<ReverseTransactionResult>(command, cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Ok(result.Value.ToReverseTransactionResultDto(id));
    }
}
