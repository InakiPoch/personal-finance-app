using PersonalFinance.Financing.Contracts.Queries;

namespace PersonalFinance.Ledger.Application.Commands.ReverseTransaction;

/// <summary>
/// What a reversal must do beyond the storno, decided purely from the reversed transaction's references and the Financing-side installment status.
/// </summary>
/// <param name="PostCompensating">Post a compensating <c>Dr CardCredit / Cr CardLiability</c> entry.</param>
/// <param name="CompensatingAmount">Minor units for the compensating entry.</param>
/// <param name="CompensatingDebitAccountId">Card credit account to debit.</param>
/// <param name="CompensatingCreditAccountId">Card liability account to credit.</param>
/// <param name="MarkReversed">Tell Financing to flag the installment reversed.</param>
/// <param name="CorrectParty">Cascade to Parties because the original transaction carried a split reference.</param>
internal sealed record ReversalDecision(
    bool PostCompensating,
    long CompensatingAmount,
    Guid CompensatingDebitAccountId,
    Guid CompensatingCreditAccountId,
    bool MarkReversed,
    bool CorrectParty
);

internal static class ReversalCalculator {
    public static ReversalDecision Decide(bool hasInstallmentRef, InstallmentStatusResponse? status, bool hasSplitRef) {
        if(!hasInstallmentRef || status is not { Exists: true }) {
            return new ReversalDecision(
                PostCompensating: false,
                CompensatingAmount: 0,
                CompensatingDebitAccountId: Guid.Empty,
                CompensatingCreditAccountId: Guid.Empty,
                MarkReversed: false,
                CorrectParty: hasSplitRef
            );
        }
        var postCompensating = status is { Paid: true, Reversed: false };
        return new ReversalDecision(
            PostCompensating: postCompensating,
            CompensatingAmount: postCompensating ? status.AmountMinorUnits : 0,
            CompensatingDebitAccountId: postCompensating ? status.CardCreditAccountId : Guid.Empty,
            CompensatingCreditAccountId: postCompensating ? status.CardLiabilityAccountId : Guid.Empty,
            MarkReversed: !status.Reversed,
            CorrectParty: hasSplitRef
        );
    }
}
