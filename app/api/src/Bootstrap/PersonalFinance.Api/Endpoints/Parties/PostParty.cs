using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Parties;

public static class PostParty {
    public static async Task<Results<Created<PartyResultDto>, ProblemHttpResult>> Handle(CreatePartyDto body, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync<Guid>(body.ToCreatePartyCommand(), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Created((string?)null, result.Value.ToPartyResultDto());
    }
}
