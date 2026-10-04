using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints;

public static class DeleteCardClosingDate {
    public static async Task<Results<NoContent, ProblemHttpResult>> Handle(Guid id, int year, int month, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync(new ClearCardClosingDayCommand(id, year, month), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.NoContent();
    }
}
