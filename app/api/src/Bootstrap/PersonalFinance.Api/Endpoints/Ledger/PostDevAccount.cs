using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Ledger;

public static class PostDevAccount {
    public static async Task<Results<Ok<CreateAccountResultDto>, ProblemHttpResult>> Handle(CreateAccountDto body, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync<Guid>(body.ToCreateAccountCommand(), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Ok(result.Value.ToCreateAccountResultDto());
    }
}
