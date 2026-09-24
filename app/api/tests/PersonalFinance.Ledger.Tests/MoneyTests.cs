using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public class MoneyTests {
    [Fact]
    public void Adding_amounts_in_different_currencies_throws() {
        var ars = Money.FromMinorUnits(1000, Currency.Reference);
        var usd = Money.FromMinorUnits(1000, Currency.Usd);
        Assert.Throws<InvalidOperationException>(() => ars + usd);
    }

    [Fact]
    public void Comparing_amounts_in_different_currencies_throws() {
        var ars = Money.FromMinorUnits(1000, Currency.Reference);
        var usd = Money.FromMinorUnits(1000, Currency.Usd);
        Assert.Throws<InvalidOperationException>(() => ars < usd);
    }

    [Fact]
    public void Adding_amounts_in_the_same_currency_succeeds() {
        var first = Money.FromMinorUnits(1000, Currency.Usd);
        var second = Money.FromMinorUnits(500, Currency.Usd);
        Assert.Equal(Money.FromMinorUnits(1500, Currency.Usd), first + second);
    }
}
