using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.RegisterSharedExpense;

internal static class RegisterSharedExpenseValidator {
    public static Result Validate(RegisterSharedExpenseCommand command) {
        if(command.TotalMinorUnits <= 0) {
            return Result.Failure(PartiesErrors.NonPositiveAmount);
        }
        if(command.CurrencyCode is not ("ARS" or "USD")) {
            return Result.Failure(PartiesErrors.InvalidCurrencyCode);
        }
        if(command.ExpenseAccountId == Guid.Empty || command.FundingAccountId == Guid.Empty) {
            return Result.Failure(PartiesErrors.UnknownFundingAccount);
        }
        if(command.Participants is not { Count: > 0 }) {
            return Result.Failure(PartiesErrors.InvalidParticipants);
        }
        var seen = new HashSet<Guid>();
        foreach(var participant in command.Participants) {
            if(participant.PartyId == Guid.Empty || participant.Weight <= 0 || !seen.Add(participant.PartyId)) {
                return Result.Failure(PartiesErrors.InvalidParticipants);
            }
        }
        return Result.Success();
    }
}
