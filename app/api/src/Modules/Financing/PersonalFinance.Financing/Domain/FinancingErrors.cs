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

    public static readonly Error InstallmentNotFound = new(
        "Financing.InstallmentNotFound",
        "The referenced installment was not found."
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

    public static readonly Error InstallmentSplitAlreadyAccrued = new(
        "Financing.InstallmentSplitAlreadyAccrued",
        "The installment's split receivable has already been accrued to the ledger."
    );

    public static readonly Error InstallmentAlreadyPaid = new(
        "Financing.InstallmentAlreadyPaid",
        "The installment has already been paid."
    );

    public static readonly Error InstallmentNotAccrued = new(
        "Financing.InstallmentNotAccrued",
        "The installment has not been accrued to a statement yet and cannot be paid individually."
    );

    public static readonly Error NotACreditorInstallment = new(
        "Financing.NotACreditorInstallment",
        "The referenced installment belongs to a credit-card plan, not a creditor-financed one."
    );

    public static readonly Error NonPositiveCreditAmount = new(
        "Financing.NonPositiveCreditAmount",
        "A card credit amount must be a positive number of minor units."
    );

    public static readonly Error InvalidSplitWeights = new(
        "Financing.InvalidSplitWeights",
        "A split must have at least one participant and every weight must be positive."
    );

    public static readonly Error PaymentPlanNotFound = new(
        "Financing.PaymentPlanNotFound",
        "The referenced payment plan was not found."
    );

    public static readonly Error InvalidSplitReference = new(
        "Financing.InvalidSplitReference",
        "A split link requires a split reference and at least one resolved party receivable account."
    );

    public static readonly Error InvalidCreditorName = new(
        "Financing.InvalidCreditorName",
        "A creditor name must not be blank."
    );

    public static readonly Error PlanNeedsCardOrCreditor = new(
        "Financing.PlanNeedsCardOrCreditor",
        "A payment plan must reference either a credit card or a creditor."
    );

    public static readonly Error PlanCannotMixCardAndCreditor = new(
        "Financing.PlanCannotMixCardAndCreditor",
        "A payment plan cannot reference both a credit card and a creditor."
    );

    public static readonly Error CreditorAccountRequired = new(
        "Financing.CreditorAccountRequired",
        "A creditor-financed payment plan requires a creditor account to pay."
    );

    public static readonly Error CreditorNotFound = new(
        "Financing.CreditorNotFound",
        "The referenced creditor was not found."
    );

    public static readonly Error CreditorAccountMismatch = new(
        "Financing.CreditorAccountMismatch",
        "The referenced account does not belong to the creditor."
    );

    public static readonly Error BlankDescription = new(
        "Financing.BlankDescription",
        "A payment plan description must not be blank."
    );

    public static readonly Error DescriptionTooLong = new(
        "Financing.DescriptionTooLong",
        "A payment plan description must be at most 120 characters."
    );

    public static readonly Error DescriptionMustBeSingleLine = new(
        "Financing.DescriptionMustBeSingleLine",
        "A payment plan description must not contain line breaks."
    );

    public static readonly Error FuturePurchaseDate = new(
        "Financing.FuturePurchaseDate",
        "A payment plan purchase date must not be in the future."
    );

    public static readonly Error BackdatedCardBankAccountRequired = new(
        "Financing.BackdatedCardBankAccountRequired",
        "A back-dated card purchase with an already-elapsed installment requires a bank account to fund its retroactive payment."
    );
}
