using PersonalFinance.Financing.Application.Scheduling;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public class CreditorSplitAccrualCalculatorTests {
    private static readonly Guid payableAccountId = Guid.CreateVersion7();

    [Fact]
    public void BuildLines_books_only_the_party_legs_and_credits_the_payable_for_their_sum() {
        var participants = new[] { participant(1), participant(1) };
        var (lines, partyPortion) = CreditorSplitAccrualCalculator.BuildLines(money(9000), participants, payableAccountId);

        Assert.Equal(6000, partyPortion);
        var debits = lines.Where(line => line.Direction == DebitOrCredit.Debit).ToList();
        Assert.Equal(
            new[] { participants[0].ReceivableAccountId, participants[1].ReceivableAccountId },
            debits.Select(debit => debit.AccountId)
        );
        Assert.All(debits, debit => Assert.Equal(3000, debit.Amount.MinorUnits));
        var credit = Assert.Single(lines, line => line.Direction == DebitOrCredit.Credit);
        Assert.Equal(payableAccountId, credit.AccountId);
        Assert.Equal(6000, credit.Amount.MinorUnits);
    }

    [Fact]
    public void BuildLines_keeps_the_debits_and_the_payable_credit_balanced() {
        var participants = new[] { participant(2), participant(1) };
        var (lines, _) = CreditorSplitAccrualCalculator.BuildLines(money(9001), participants, payableAccountId);

        var debitTotal = lines.Where(line => line.Direction == DebitOrCredit.Debit).Sum(line => line.Amount.MinorUnits);
        var creditTotal = lines.Where(line => line.Direction == DebitOrCredit.Credit).Sum(line => line.Amount.MinorUnits);
        Assert.Equal(debitTotal, creditTotal);
    }

    [Fact]
    public void BuildLines_lets_the_holder_absorb_the_phantom_penny() {
        // 100 over three equal weights is 33 each with one unit left over; largest-remainder ties break to
        // the lowest index — the holder at weight 0 — so only the two party shares of 33 post (66 total).
        var participants = new[] { participant(1), participant(1) };
        var (lines, partyPortion) = CreditorSplitAccrualCalculator.BuildLines(money(100), participants, payableAccountId);

        Assert.Equal(66, partyPortion);
        Assert.Equal(66, lines.Single(line => line.Direction == DebitOrCredit.Credit).Amount.MinorUnits);
    }

    [Fact]
    public void BuildLines_skips_a_participant_whose_share_rounds_to_zero() {
        // 2 minor units over three equal weights: the two leftover units go to indices 0 and 1, so the
        // holder and the first party get 1 each and the second party gets nothing and is left off the ledger.
        var participants = new[] { participant(1), participant(1) };
        var (lines, partyPortion) = CreditorSplitAccrualCalculator.BuildLines(money(2), participants, payableAccountId);

        var debit = Assert.Single(lines, line => line.Direction == DebitOrCredit.Debit);
        Assert.Equal(participants[0].ReceivableAccountId, debit.AccountId);
        Assert.Equal(1, partyPortion);
        Assert.Equal(1, lines.Single(line => line.Direction == DebitOrCredit.Credit).Amount.MinorUnits);
    }

    [Fact]
    public void BuildLines_returns_nothing_when_no_party_share_rounds_above_zero() {
        var participants = new[] { participant(1) };
        var (lines, partyPortion) = CreditorSplitAccrualCalculator.BuildLines(money(1), participants, payableAccountId);

        Assert.Empty(lines);
        Assert.Equal(0, partyPortion);
    }

    private static PaymentPlanSplitParticipant participant(long weight) {
        var participant = PaymentPlanSplitParticipant.For(Guid.CreateVersion7(), Guid.CreateVersion7(), weight);
        participant.AssignReceivableAccount(Guid.CreateVersion7());
        return participant;
    }

    private static Money money(long minorUnits) {
        return Money.FromMinorUnits(minorUnits, Currency.Reference);
    }
}
