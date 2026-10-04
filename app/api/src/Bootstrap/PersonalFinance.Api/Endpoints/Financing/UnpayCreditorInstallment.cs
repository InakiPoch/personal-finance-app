using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class UnpayCreditorInstallment {
    public static async Task<Results<Ok<PayCreditorInstallmentResultDto>, ProblemHttpResult>> Handle(Guid id, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync<Guid>(new UnpayCreditorInstallmentCommand(id), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Ok(result.Value.ToPayCreditorInstallmentResultDto());
    }
}
