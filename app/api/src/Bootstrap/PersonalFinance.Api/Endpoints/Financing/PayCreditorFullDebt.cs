using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class PayCreditorFullDebt {
    public static async Task<Results<Ok<PayCreditorFullDebtResultDto>, ProblemHttpResult>> Handle(Guid creditorId, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync<int>(new PayCreditorFullDebtCommand(creditorId), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Ok(result.Value.ToPayCreditorFullDebtResultDto());
    }
}
