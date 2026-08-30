using PersonalFinance.Financing.Application.Commands.PayStatement;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public class PayStatementNettingTests {
    private static readonly Guid liabilityAccountId = Guid.CreateVersion7();
    private static readonly Guid bankAccountId = Guid.CreateVersion7();
    private static readonly Guid cardCreditAccountId = Guid.CreateVersion7();

    [Fact]
    public void No_carried_credit_keeps_the_plain_two_leg_posting() {
        var posting = StatementPaymentCalculator.Build(Money.Zero(Currency.Reference), minorUnits(1000), liabilityAccountId, bankAccountId, cardCreditAccountId);
        Assert.Equal(0, posting.CreditApplied.MinorUnits);
        Assert.Equal(2, posting.Lines.Count);
        assertLeg(posting.Lines[0], liabilityAccountId, DebitOrCredit.Debit, 1000);
        assertLeg(posting.Lines[1], bankAccountId, DebitOrCredit.Credit, 1000);
    }

    [Fact]
    public void Partial_credit_nets_the_bank_leg_and_adds_a_card_credit_leg() {
        var posting = StatementPaymentCalculator.Build(minorUnits(400), minorUnits(1000), liabilityAccountId, bankAccountId, cardCreditAccountId);
        Assert.Equal(400, posting.CreditApplied.MinorUnits);
        Assert.Equal(3, posting.Lines.Count);
        assertLeg(posting.Lines[0], liabilityAccountId, DebitOrCredit.Debit, 1000);
        assertLeg(posting.Lines[1], bankAccountId, DebitOrCredit.Credit, 600);
        assertLeg(posting.Lines[2], cardCreditAccountId, DebitOrCredit.Credit, 400);
        Assert.Equal(0, signedTotal(posting));
    }

    [Fact]
    public void Credit_exceeding_the_statement_is_capped_and_drops_the_bank_leg() {
        var posting = StatementPaymentCalculator.Build(minorUnits(1500), minorUnits(1000), liabilityAccountId, bankAccountId, cardCreditAccountId);
        Assert.Equal(1000, posting.CreditApplied.MinorUnits);
        Assert.Equal(2, posting.Lines.Count);
        assertLeg(posting.Lines[0], liabilityAccountId, DebitOrCredit.Debit, 1000);
        assertLeg(posting.Lines[1], cardCreditAccountId, DebitOrCredit.Credit, 1000);
        Assert.DoesNotContain(posting.Lines, line => line.AccountId == bankAccountId);
    }

    [Fact]
    public void Credit_exactly_equal_to_the_statement_drops_the_bank_leg() {
        var posting = StatementPaymentCalculator.Build(minorUnits(1000), minorUnits(1000), liabilityAccountId, bankAccountId, cardCreditAccountId);
        Assert.Equal(1000, posting.CreditApplied.MinorUnits);
        Assert.Equal(2, posting.Lines.Count);
        assertLeg(posting.Lines[1], cardCreditAccountId, DebitOrCredit.Credit, 1000);
        Assert.Equal(0, signedTotal(posting));
    }

    private static void assertLeg(PostTransactionLine line, Guid accountId, DebitOrCredit direction, long minor) {
        Assert.Equal(accountId, line.AccountId);
        Assert.Equal(direction, line.Direction);
        Assert.Equal(minor, line.Amount.MinorUnits);
    }

    private static long signedTotal(StatementPaymentPosting posting) {
        var total = 0L;
        foreach(var line in posting.Lines) {
            total += line.Direction == DebitOrCredit.Debit ? line.Amount.MinorUnits : -line.Amount.MinorUnits;
        }
        return total;
    }

    private static Money minorUnits(long amount) {
        return Money.FromMinorUnits(amount, Currency.Reference);
    }
}
