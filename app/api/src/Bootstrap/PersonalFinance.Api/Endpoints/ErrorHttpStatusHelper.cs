namespace PersonalFinance.Api.Endpoints;

/// <summary>
/// Pure mapping from a <see cref="PersonalFinance.SharedKernel.Error"/> code to an HTTP status.
/// </summary>
internal static class ErrorHttpStatusHelper {
    public static int From(string errorCode) {
        return errorCode switch {
            // 404 — the referenced resource does not exist
            "Ledger.OriginalTransactionNotFound" => StatusCodes.Status404NotFound,
            "Ledger.AccountNotFound" => StatusCodes.Status404NotFound,
            "Financing.CardNotFound" => StatusCodes.Status404NotFound,
            "Financing.StatementNotFound" => StatusCodes.Status404NotFound,
            "Financing.InstallmentNotFound" => StatusCodes.Status404NotFound,
            "Financing.PaymentPlanNotFound" => StatusCodes.Status404NotFound,
            "Subscriptions.SubscriptionNotFound" => StatusCodes.Status404NotFound,
            "Parties.PartyNotFound" => StatusCodes.Status404NotFound,
            "Parties.SplitNotFound" => StatusCodes.Status404NotFound,

            // 409 — the resource exists but is in a state that forbids the transition
            "Ledger.CannotReverseAReversal" => StatusCodes.Status409Conflict,
            "Ledger.TransactionAlreadyReversed" => StatusCodes.Status409Conflict,
            "Financing.StatementAlreadyPaid" => StatusCodes.Status409Conflict,
            "Financing.InstallmentAlreadyAccrued" => StatusCodes.Status409Conflict,
            "Financing.InstallmentAlreadyReversed" => StatusCodes.Status409Conflict,
            "Financing.InstallmentAlreadyPaid" => StatusCodes.Status409Conflict,
            "Financing.ClosingMonthLocked" => StatusCodes.Status409Conflict,
            "Financing.ClosingChangeMovesChargedPurchase" => StatusCodes.Status409Conflict,
            "Financing.InstallmentNotAccrued" => StatusCodes.Status409Conflict,
            "Financing.NotACreditorInstallment" => StatusCodes.Status409Conflict,
            "Financing.NoPaymentToUndo" => StatusCodes.Status409Conflict,
            "Financing.PartyShareNotDue" => StatusCodes.Status409Conflict,
            "Financing.PartyNotInSplit" => StatusCodes.Status409Conflict,
            "Financing.PartyShareAlreadyPaid" => StatusCodes.Status409Conflict,
            "Subscriptions.SubscriptionNotActive" => StatusCodes.Status409Conflict,
            "Subscriptions.SubscriptionNotPaid" => StatusCodes.Status409Conflict,
            "Parties.SettlementExceedsBalance" => StatusCodes.Status409Conflict,

            // 422 — well-formed request that violates a domain rule the caller could in principle fix
            "Ledger.Unbalanced" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.MixedCurrency" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.DegenerateTransaction" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.InvalidAccountName" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.IncoherentAccountKind" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.NonPositiveEntryAmount" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.InvalidExpenseCategory" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.InvalidExpenseDescription" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.InvalidExpenseSplit" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.SourceAccountNotSpendable" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.InvalidCurrencyCode" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.InvalidIncomeDescription" => StatusCodes.Status422UnprocessableEntity,
            "Ledger.IncomeDateInFuture" => StatusCodes.Status422UnprocessableEntity,
            "Financing.InvalidCardName" => StatusCodes.Status422UnprocessableEntity,
            "Financing.InvalidCutoffDay" => StatusCodes.Status422UnprocessableEntity,
            "Financing.InvalidClosingDay" => StatusCodes.Status422UnprocessableEntity,
            "Financing.NonPositivePlanAmount" => StatusCodes.Status422UnprocessableEntity,
            "Financing.InvalidInstallmentCount" => StatusCodes.Status422UnprocessableEntity,
            "Financing.InvalidBankAccount" => StatusCodes.Status422UnprocessableEntity,
            "Financing.NonPositiveCreditAmount" => StatusCodes.Status422UnprocessableEntity,
            "Financing.InvalidSplitWeights" => StatusCodes.Status422UnprocessableEntity,
            "Financing.InvalidSplitReference" => StatusCodes.Status422UnprocessableEntity,
            "Financing.BlankDescription" => StatusCodes.Status422UnprocessableEntity,
            "Financing.DescriptionTooLong" => StatusCodes.Status422UnprocessableEntity,
            "Financing.DescriptionMustBeSingleLine" => StatusCodes.Status422UnprocessableEntity,
            "Financing.FuturePurchaseDate" => StatusCodes.Status422UnprocessableEntity,
            "Financing.BackdatedCardBankAccountRequired" => StatusCodes.Status422UnprocessableEntity,
            "Financing.InvalidCurrencyCode" => StatusCodes.Status422UnprocessableEntity,
            "Subscriptions.InvalidName" => StatusCodes.Status422UnprocessableEntity,
            "Subscriptions.InvalidCategory" => StatusCodes.Status422UnprocessableEntity,
            "Subscriptions.NonPositiveAmount" => StatusCodes.Status422UnprocessableEntity,
            "Subscriptions.InvalidAnchorDay" => StatusCodes.Status422UnprocessableEntity,
            "Subscriptions.InvalidFundingAccount" => StatusCodes.Status422UnprocessableEntity,
            "Subscriptions.InvalidCurrencyCode" => StatusCodes.Status422UnprocessableEntity,
            "Parties.InvalidName" => StatusCodes.Status422UnprocessableEntity,
            "Parties.InvalidParticipants" => StatusCodes.Status422UnprocessableEntity,
            "Parties.NonPositiveAmount" => StatusCodes.Status422UnprocessableEntity,
            "Parties.UnknownFundingAccount" => StatusCodes.Status422UnprocessableEntity,
            "Parties.InvalidLoanDescription" => StatusCodes.Status422UnprocessableEntity,
            "Parties.LoanDateInFuture" => StatusCodes.Status422UnprocessableEntity,
            "Parties.InvalidBorrowingDescription" => StatusCodes.Status422UnprocessableEntity,
            "Parties.BorrowingDateInFuture" => StatusCodes.Status422UnprocessableEntity,
            "Parties.InvalidCurrencyCode" => StatusCodes.Status422UnprocessableEntity,
            "Instruments.CutoffRequired" => StatusCodes.Status422UnprocessableEntity,

            // 400 — unroutable / unknown-shape codes
            "Instruments.UnknownType" => StatusCodes.Status400BadRequest,
            "Request.Malformed" => StatusCodes.Status400BadRequest,
            "Financing.InvalidPaymentAmount" => StatusCodes.Status400BadRequest,
            "Financing.PaymentExceedsRemaining" => StatusCodes.Status400BadRequest,

            _ => fromSuffix(errorCode)
        };
    }

    private static int fromSuffix(string errorCode) {
        if(errorCode.EndsWith("NotFound", StringComparison.Ordinal)) {
            return StatusCodes.Status404NotFound;
        }
        if(errorCode.EndsWith("AlreadyPaid", StringComparison.Ordinal)
            || errorCode.EndsWith("AlreadyAccrued", StringComparison.Ordinal)
            || errorCode.EndsWith("AlreadyReversed", StringComparison.Ordinal)
            || errorCode.EndsWith("NotActive", StringComparison.Ordinal)) {
            return StatusCodes.Status409Conflict;
        }
        if(errorCode.Contains(".Invalid", StringComparison.Ordinal)
            || errorCode.Contains("NonPositive", StringComparison.Ordinal)) {
            return StatusCodes.Status422UnprocessableEntity;
        }
        return StatusCodes.Status400BadRequest;
    }
}
