using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public class InstallmentAllocationTests {
    [Fact]
    public void Create_splits_the_total_with_no_dropped_or_duplicated_cents() {
        var rng = new Random(20260830);
        var allocator = new PhantomPennyAllocator();
        for(var iteration = 0; iteration < 500; iteration++) {
            var count = rng.Next(1, 61);
            var total = count + rng.Next(0, 5_000_000);
            var cutoffDay = rng.Next(1, 32);
            var purchaseDate = new DateOnly(2026, 1, 1).AddDays(rng.Next(0, 365));
            var money = Money.FromMinorUnits(total, Currency.Reference);
            var result = PaymentPlan.Create(Guid.CreateVersion7(), money, count, purchaseDate, cutoffDay, allocator);
            Assert.True(result.IsSuccess);
            var plan = result.Value;
            Assert.Equal(count, plan.Installments.Count);
            var sum = plan.Installments.Aggregate(0L, (acc, installment) => acc + installment.Amount.MinorUnits);
            Assert.Equal(total, sum);
            Assert.All(plan.Installments, installment => Assert.True(installment.Amount.MinorUnits > 0));
            var expectedCycle = BillingCycleCalculator.ResolveCycle(purchaseDate, cutoffDay);
            for(var i = 0; i < count; i++) {
                Assert.Equal(i + 1, plan.Installments[i].Sequence);
                Assert.Equal(expectedCycle.AddMonths(i), plan.Installments[i].Cycle);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_a_non_positive_total(long minorUnits) {
        var result = PaymentPlan.Create(
            Guid.CreateVersion7(),
            Money.FromMinorUnits(minorUnits, Currency.Reference),
            installmentCount: 3,
            purchaseDate: new DateOnly(2026, 3, 10),
            cutoffDay: 15,
            new PhantomPennyAllocator()
        );
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.NonPositivePlanAmount", result.Error.Code);
    }

    [Fact]
    public void Create_rejects_an_installment_count_below_one() {
        var result = PaymentPlan.Create(
            Guid.CreateVersion7(),
            Money.FromMinorUnits(10_000, Currency.Reference),
            installmentCount: 0,
            purchaseDate: new DateOnly(2026, 3, 10),
            cutoffDay: 15,
            new PhantomPennyAllocator()
        );
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.InvalidInstallmentCount", result.Error.Code);
    }
}
