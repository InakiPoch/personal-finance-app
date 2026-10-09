using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.RecordRepayment;

internal static class RepayPartyValidator {
    public static Result Validate(RepayPartyCommand command) {
        if(command.AmountMinorUnits <= 0) {
            return Result.Failure(PartiesErrors.NonPositiveAmount);
        }
        if(command.CurrencyCode is not ("ARS" or "USD")) {
            return Result.Failure(PartiesErrors.InvalidCurrencyCode);
        }
        if(command.PartyId == Guid.Empty) {
            return Result.Failure(PartiesErrors.PartyNotFound);
        }
        return command.SourceAccountId == Guid.Empty ? Result.Failure(PartiesErrors.UnknownFundingAccount) : Result.Success();
    }
}
