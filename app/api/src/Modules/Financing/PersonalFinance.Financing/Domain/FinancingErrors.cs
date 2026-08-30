using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal static class FinancingErrors {
    public static readonly Error InvalidCardName = new(
        "Financing.InvalidCardName",
        "A credit card name must not be blank."
    );
    
    public static readonly Error InvalidCutoffDay = new(
        "Financing.InvalidCutoffDay",
        "The statement cutoff day must be between 1 and 31."
    );

    public static readonly Error NonPositivePlanAmount = new(
        "Financing.NonPositivePlanAmount",
        "A payment plan amount must be a positive number of minor units."
    );

    public static readonly Error InvalidInstallmentCount = new(
        "Financing.InvalidInstallmentCount",
        "A payment plan must have at least one installment."
    );

    public static readonly Error CardNotFound = new(
        "Financing.CardNotFound",
        "The referenced credit card was not found."
    );

    public static readonly Error StatementNotFound = new(
        "Financing.StatementNotFound",
        "The referenced monthly statement was not found."
    );

    public static readonly Error InvalidBankAccount = new(
        "Financing.InvalidBankAccount",
        "A statement payment requires a valid bank account."
    );

    public static readonly Error StatementAlreadyPaid = new(
        "Financing.StatementAlreadyPaid",
        "The monthly statement has already been paid."
    );

    public static readonly Error InstallmentAlreadyAccrued = new(
        "Financing.InstallmentAlreadyAccrued",
        "The installment has already been accrued to the ledger."
    );

    public static readonly Error InstallmentAlreadyReversed = new(
        "Financing.InstallmentAlreadyReversed",
        "The installment has already been reversed."
    );

    public static readonly Error NonPositiveCreditAmount = new(
        "Financing.NonPositiveCreditAmount",
        "A card credit amount must be a positive number of minor units."
    );

    public static readonly Error InvalidSplitWeights = new(
        "Financing.InvalidSplitWeights",
        "A split must have at least one participant and every weight must be positive."
    );
}
