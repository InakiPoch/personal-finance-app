using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.RecordPartyPurchase;

internal static class RecordPartyPurchaseValidator {
    public const int MaxDescriptionLength = 120;

    public static Result Validate(RecordPartyPurchaseCommand command) {
        if(command.ShareMinorUnits <= 0) {
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
            return Result.Failure(PartiesErrors.InvalidPurchaseDescription);
        }
        if(string.IsNullOrWhiteSpace(command.CategoryName)) {
            return Result.Failure(PartiesErrors.InvalidPurchaseCategory);
        }
        return command.Kind is not ("debit" or "credit") ? Result.Failure(PartiesErrors.InvalidPurchaseKind) : Result.Success();
    }
}
