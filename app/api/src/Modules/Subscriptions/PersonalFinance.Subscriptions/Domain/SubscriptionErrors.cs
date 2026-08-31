using PersonalFinance.SharedKernel;

namespace PersonalFinance.Subscriptions.Domain;

internal static class SubscriptionErrors {
    public static readonly Error InvalidName = new(
        "Subscriptions.InvalidName",
        "A subscription name must not be blank."
    );

    public static readonly Error InvalidCategory = new(
        "Subscriptions.InvalidCategory",
        "A subscription category must not be blank."
    );

    public static readonly Error NonPositiveAmount = new(
        "Subscriptions.NonPositiveAmount",
        "A subscription amount must be a positive number of minor units."
    );

    public static readonly Error InvalidAnchorDay = new(
        "Subscriptions.InvalidAnchorDay",
        "The recurrence anchor day must be between 1 and 31."
    );

    public static readonly Error InvalidFundingAccount = new(
        "Subscriptions.InvalidFundingAccount",
        "A subscription requires a valid funding account."
    );

    public static readonly Error SubscriptionNotFound = new(
        "Subscriptions.SubscriptionNotFound",
        "The referenced subscription was not found."
    );

    public static readonly Error SubscriptionNotActive = new(
        "Subscriptions.SubscriptionNotActive",
        "The subscription is no longer active."
    );
}
