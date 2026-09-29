using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public class InstallmentPaymentTests {
    [Fact]
    public void ApplyPayment_partial_reduces_remaining_without_marking_paid() {
        var installment = newInstallment(10_000);
        var result = installment.ApplyPayment(4_000, DateTimeOffset.UtcNow);
        Assert.True(result.IsSuccess);
        Assert.Equal(6_000, installment.RemainingMinorUnits);
        Assert.Equal(4_000, installment.PaidMinorUnits);
        Assert.False(installment.IsPaid);
    }

    [Fact]
    public void ApplyPayment_exact_remainder_marks_the_installment_paid() {
        var installment = newInstallment(10_000);
        var now = DateTimeOffset.UtcNow;
        var result = installment.ApplyPayment(10_000, now);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, installment.RemainingMinorUnits);
        Assert.True(installment.IsPaid);
        Assert.Equal(now, installment.PaidOnUtc);
    }

    [Fact]
    public void ApplyPayment_over_the_remaining_amount_reports_payment_exceeds_remaining() {
        var installment = newInstallment(10_000);

        var result = installment.ApplyPayment(10_001, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.PaymentExceedsRemaining, result.Error);
        Assert.Equal(0, installment.PaidMinorUnits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void ApplyPayment_a_non_positive_amount_reports_invalid_payment_amount(long amount) {
        var installment = newInstallment(10_000);
        var result = installment.ApplyPayment(amount, DateTimeOffset.UtcNow);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.InvalidPaymentAmount, result.Error);
    }

    [Fact]
    public void ApplyPayment_on_a_reversed_installment_reports_already_reversed() {
        var installment = newInstallment(10_000);
        installment.MarkReversed();
        var result = installment.ApplyPayment(1_000, DateTimeOffset.UtcNow);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.InstallmentAlreadyReversed, result.Error);
    }

    [Fact]
    public void UndoLastPayment_removes_the_newest_row_and_clears_paid_on_utc() {
        var installment = newInstallment(10_000);
        installment.ApplyPayment(4_000, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        installment.ApplyPayment(6_000, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        Assert.True(installment.IsPaid);
        var result = installment.UndoLastPayment();
        Assert.True(result.IsSuccess);
        Assert.Equal(6_000, result.Value.AmountMinorUnits);
        Assert.False(installment.IsPaid);
        Assert.Null(installment.PaidOnUtc);
        Assert.Equal(4_000, installment.PaidMinorUnits);
        Assert.Equal(6_000, installment.RemainingMinorUnits);
    }

    [Fact]
    public void UndoLastPayment_with_no_payments_reports_no_payment_to_undo() {
        var installment = newInstallment(10_000);
        var result = installment.UndoLastPayment();
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.NoPaymentToUndo, result.Error);
    }

    private static Installment newInstallment(long amountMinorUnits) {
        return Installment.Schedule(Guid.NewGuid(), 1, Money.FromMinorUnits(amountMinorUnits, Currency.Reference), new BillingCycle(2026, 3));
    }
}
