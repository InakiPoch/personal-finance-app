using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.RecordPartyPurchase;

internal static class RecordPartyPurchaseValidator {
    public const int MaxDescriptionLength = 120;
    public const int MaxInstallmentCount = 60;

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
        if(command.Kind is not ("debit" or "credit")) {
            return Result.Failure(PartiesErrors.InvalidPurchaseKind);
        }
        if(command.Kind == "debit") {
            return Result.Success();
        }
        if(command.InstallmentCount is < 1 or > MaxInstallmentCount) {
            return Result.Failure(PartiesErrors.InvalidInstallmentCount);
        }
        var purchaseMonth = new DateOnly(command.PurchaseDate.Year, command.PurchaseDate.Month, 1);
        if(command.FirstPaymentMonth is null || command.FirstPaymentMonth.Value < purchaseMonth) {
            return Result.Failure(PartiesErrors.InvalidFirstPaymentMonth);
        }
        return Result.Success();
    }
}
