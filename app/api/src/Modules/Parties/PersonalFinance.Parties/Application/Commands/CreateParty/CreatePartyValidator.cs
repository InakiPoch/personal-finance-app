using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.CreateParty;

internal static class CreatePartyValidator {
    public static Result Validate(CreatePartyCommand command) {
        return string.IsNullOrWhiteSpace(command.Name)
            ? Result.Failure(PartiesErrors.InvalidName)
        : Result.Success();
    }
}
