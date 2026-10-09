using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Parties;

public static class PostBorrowing {
    public static async Task<Results<Created<BorrowingResultDto>, ProblemHttpResult>> Handle(Guid id, RecordBorrowingDto body, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync<Guid>(body.ToRecordBorrowingCommand(id), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Created((string?)null, result.Value.ToBorrowingResultDto());
    }
}
