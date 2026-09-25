using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.SettleCurrentAccount;

internal static class SettleCurrentAccountValidator {
    public static Result Validate(SettleCurrentAccountCommand command) {
        if(command.AmountMinorUnits <= 0) {
            return Result.Failure(PartiesErrors.NonPositiveAmount);
        }
        if(command.CurrencyCode is not ("ARS" or "USD")) {
            return Result.Failure(PartiesErrors.InvalidCurrencyCode);
        }
        if(command.PartyId == Guid.Empty) {
            return Result.Failure(PartiesErrors.PartyNotFound);
        }
        return command.BankAccountId == Guid.Empty
            ? Result.Failure(PartiesErrors.UnknownFundingAccount)
        : Result.Success();
    }
}
