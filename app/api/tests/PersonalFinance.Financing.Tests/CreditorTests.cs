using PersonalFinance.Financing.Domain;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public class CreditorTests {
    [Fact]
    public void Create_succeeds_with_a_valid_name_and_accounts_pass_through_with_generated_ids() {
        var result = Creditor.Create(Guid.NewGuid(), "Juan", [("Galicia", "CBU123"), ("Mercado Pago", "alias.mp")]);
        Assert.True(result.IsSuccess);
        var creditor = result.Value;
        Assert.Equal("Juan", creditor.Name);
        Assert.Equal(2, creditor.Accounts.Count);
        Assert.All(creditor.Accounts, account => Assert.NotEqual(Guid.Empty, account.Id));
        Assert.Equal("Galicia", creditor.Accounts[0].Label);
        Assert.Equal("CBU123", creditor.Accounts[0].Identifier);
    }

    [Fact]
    public void Create_trims_the_name() {
        var result = Creditor.Create(Guid.NewGuid(), "  Juan  ", []);
        Assert.True(result.IsSuccess);
        Assert.Equal("Juan", result.Value.Name);
    }

    [Fact]
    public void Create_fails_on_a_blank_name() {
        var result = Creditor.Create(Guid.NewGuid(), "   ", []);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.InvalidCreditorName", result.Error.Code);
    }

    [Fact]
    public void Create_allows_zero_accounts() {
        var result = Creditor.Create(Guid.NewGuid(), "Juan", []);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Accounts);
    }

    [Fact]
    public void Create_normalizes_a_blank_identifier_to_null() {
        var result = Creditor.Create(Guid.NewGuid(), "Juan", [("Galicia", "   ")]);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Accounts[0].Identifier);
    }
}
