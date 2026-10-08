using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Domain;

internal static class PartiesErrors {
    public static readonly Error InvalidName = new(
        "Parties.InvalidName",
        "A party name must not be blank."
    );

    public static readonly Error PartyNotFound = new(
        "Parties.PartyNotFound",
        "The referenced party was not found."
    );

    public static readonly Error InvalidParticipants = new(
        "Parties.InvalidParticipants",
        "A shared expense needs at least one participant, each a known party with a positive weight."
    );

    public static readonly Error NonPositiveAmount = new(
        "Parties.NonPositiveAmount",
        "An amount must be a positive number of minor units."
    );

    public static readonly Error UnknownFundingAccount = new(
        "Parties.UnknownFundingAccount",
        "A shared expense requires a valid expense account and funding account."
    );

    public static readonly Error SplitNotFound = new(
        "Parties.SplitNotFound",
        "The referenced expense split was not found."
    );

    public static readonly Error SettlementExceedsBalance = new(
        "Parties.SettlementExceedsBalance",
        "A settlement cannot exceed the party's outstanding balance."
    );

    public static readonly Error InvalidLoanDescription = new(
        "Parties.InvalidLoanDescription",
        "A loan needs a single-line description of at most 120 characters."
    );

    public static readonly Error LoanDateInFuture = new(
        "Parties.LoanDateInFuture",
        "A loan cannot be dated in the future."
    );

    public static readonly Error InvalidCurrencyCode = new(
        "Parties.InvalidCurrencyCode",
        "Currency code must be ARS or USD."
    );
}
