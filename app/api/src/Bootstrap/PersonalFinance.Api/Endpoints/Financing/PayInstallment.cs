using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class PayInstallment {
    public static async Task<Results<Created<PayInstallmentResultDto>, ProblemHttpResult>> Handle(Guid id, PayInstallmentDto body, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync<Guid>(body.ToPayInstallmentCommand(id), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Created((string?)null, result.Value.ToPayInstallmentResultDto());
    }
}
