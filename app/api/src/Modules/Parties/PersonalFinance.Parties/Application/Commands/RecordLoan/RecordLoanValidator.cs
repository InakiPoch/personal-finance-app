using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.RecordLoan;

internal static class RecordLoanValidator {
    public const int MaxDescriptionLength = 120;

    public static Result Validate(RecordLoanCommand command) {
        if(command.AmountMinorUnits <= 0) {
            return Result.Failure(PartiesErrors.NonPositiveAmount);
        }
        if(command.CurrencyCode is not ("ARS" or "USD")) {
            return Result.Failure(PartiesErrors.InvalidCurrencyCode);
        }
        if(command.PartyId == Guid.Empty) {
            return Result.Failure(PartiesErrors.PartyNotFound);
        }
        var description = command.Description?.Trim();
        if(string.IsNullOrEmpty(description) || description.Length > MaxDescriptionLength || description.Contains('\n') || description.Contains('\r')) {
            return Result.Failure(PartiesErrors.InvalidLoanDescription);
        }
        return command.SourceAccountId == Guid.Empty ? Result.Failure(PartiesErrors.UnknownFundingAccount) : Result.Success();
    }
}
