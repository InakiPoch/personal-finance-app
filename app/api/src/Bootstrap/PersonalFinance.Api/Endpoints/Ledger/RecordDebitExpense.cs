using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Ledger;

public static class RecordDebitExpense {
    public static async Task<Results<Created<RecordDebitExpenseResultDto>, ProblemHttpResult>> Handle(RecordDebitExpenseDto body, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync<Guid>(body.ToRecordDebitExpenseCommand(), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Created((string?)null, result.Value.ToRecordDebitExpenseResultDto());
    }
}
