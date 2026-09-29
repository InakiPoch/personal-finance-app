using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class PayCreditorInstallmentPartyShare {
    public static async Task<Results<Ok<PayCreditorInstallmentPartyShareResultDto>, ProblemHttpResult>> Handle(Guid id, PayCreditorInstallmentPartyShareRequestDto dto, ICommandBus commandBus, CancellationToken cancellationToken) {
        var result = await commandBus.SendAsync<Guid>(new PayCreditorInstallmentPartyShareCommand(id, dto.PartyId, dto.BankAccountId), cancellationToken);
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Ok(result.Value.ToPayCreditorInstallmentPartyShareResultDto());
    }
}
