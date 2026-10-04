using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints;

public static class PutCardClosingDay {
    public static async Task<Results<NoContent, ProblemHttpResult>> Handle(Guid id, PutClosingDayDto body, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync(new ChangeCardUsualClosingDayCommand(id, body.Day), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.NoContent();
    }
}
