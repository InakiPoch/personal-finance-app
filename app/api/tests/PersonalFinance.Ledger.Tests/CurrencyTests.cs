using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public class CurrencyTests {
    [Fact]
    public void FromCode_ars_returns_the_reference_currency() {
        Assert.Equal(Currency.Reference, Currency.FromCode("ARS"));
    }

    [Fact]
    public void FromCode_usd_returns_the_usd_currency() {
        Assert.Equal(Currency.Usd, Currency.FromCode("USD"));
    }

    [Fact]
    public void FromCode_an_unknown_code_throws() {
        Assert.Throws<ArgumentOutOfRangeException>(() => Currency.FromCode("EUR"));
    }
}
