using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Ledger.Application.Commands.ReverseTransaction;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public class ReversalCalculatorTests {
    private static readonly Guid cardCreditAccountId = Guid.CreateVersion7();
    private static readonly Guid cardLiabilityAccountId = Guid.CreateVersion7();

    [Fact]
    public void No_installment_reference_and_no_split_yields_a_plain_storno() {
        var decision = ReversalCalculator.Decide(hasInstallmentRef: false, status: null, hasSplitRef: false);
        Assert.False(decision.PostCompensating);
        Assert.False(decision.MarkReversed);
        Assert.False(decision.CorrectParty);
        Assert.Equal(0, decision.CompensatingAmount);
    }

    [Fact]
    public void A_split_reference_without_an_installment_only_flags_the_party_cascade() {
        var decision = ReversalCalculator.Decide(hasInstallmentRef: false, status: null, hasSplitRef: true);
        Assert.True(decision.CorrectParty);
        Assert.False(decision.PostCompensating);
        Assert.False(decision.MarkReversed);
    }

    [Fact]
    public void An_unknown_installment_yields_a_plain_storno() {
        var decision = ReversalCalculator.Decide(hasInstallmentRef: true, status: notFound(), hasSplitRef: false);
        Assert.False(decision.PostCompensating);
        Assert.False(decision.MarkReversed);
    }

    [Fact]
    public void An_accrued_but_unpaid_installment_is_flagged_reversed_without_a_compensating_entry() {
        var decision = ReversalCalculator.Decide(hasInstallmentRef: true, status: found(paid: false, reversed: false, amount: 1000), hasSplitRef: false);
        Assert.False(decision.PostCompensating);
        Assert.Equal(0, decision.CompensatingAmount);
        Assert.True(decision.MarkReversed);
    }

    [Fact]
    public void A_paid_installment_posts_a_compensating_card_credit_entry_and_is_flagged_reversed() {
        var decision = ReversalCalculator.Decide(hasInstallmentRef: true, status: found(paid: true, reversed: false, amount: 1500), hasSplitRef: false);
        Assert.True(decision.PostCompensating);
        Assert.Equal(1500, decision.CompensatingAmount);
        Assert.Equal(cardCreditAccountId, decision.CompensatingDebitAccountId);
        Assert.Equal(cardLiabilityAccountId, decision.CompensatingCreditAccountId);
        Assert.True(decision.MarkReversed);
    }

    [Fact]
    public void An_already_reversed_installment_is_left_untouched() {
        var decision = ReversalCalculator.Decide(hasInstallmentRef: true, status: found(paid: true, reversed: true, amount: 1500), hasSplitRef: false);
        Assert.False(decision.PostCompensating);
        Assert.False(decision.MarkReversed);
    }

    [Fact]
    public void A_paid_split_installment_posts_the_compensating_entry_and_flags_the_party_cascade() {
        var decision = ReversalCalculator.Decide(hasInstallmentRef: true, status: found(paid: true, reversed: false, amount: 2000), hasSplitRef: true);
        Assert.True(decision.PostCompensating);
        Assert.True(decision.CorrectParty);
        Assert.True(decision.MarkReversed);
    }

    private static InstallmentStatusResponse notFound() {
        return new InstallmentStatusResponse(false, false, false, null, 0, false, Guid.Empty, Guid.Empty, Guid.Empty);
    }

    private static InstallmentStatusResponse found(bool paid, bool reversed, long amount) {
        return new InstallmentStatusResponse(
            Exists: true,
            Accrued: true,
            Paid: paid,
            StatementId: Guid.CreateVersion7(),
            AmountMinorUnits: amount,
            Reversed: reversed,
            CardId: Guid.CreateVersion7(),
            CardCreditAccountId: cardCreditAccountId,
            CardLiabilityAccountId: cardLiabilityAccountId
        );
    }
}
