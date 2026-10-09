using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PartyRepaymentTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task A_partial_repayment_lowers_the_bank_and_what_i_owe_and_shows_on_the_i_owe_timeline() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var (bankId, partyId) = await BorrowedAsync(client, "Repay Bank 1", "Lucia", 20_000, cancellationToken);
        var response = await PostRepaymentAsync(client, partyId, bankId, 8_000, "ARS", null, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(12_000, await AccountBalanceAsync(client, bankId, cancellationToken));
        Assert.Equal(12_000, await PartyBalanceAsync(client, partyId, "payableBalances", "ARS", cancellationToken));
        var owe = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/parties/{partyId}/timeline?side=payable", cancellationToken);
        var last = owe.GetProperty("rows").EnumerateArray().Last();
        Assert.Equal("Paid back to Lucia", last.GetProperty("description").GetString());
        Assert.Equal(-8_000, last.GetProperty("deltaMinorUnits").GetInt64());
    }

    [Fact]
    public async Task Repaying_everything_zeroes_what_i_owe() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var (bankId, partyId) = await BorrowedAsync(client, "Repay Bank 2", "Bruno", 5_000, cancellationToken);
        (await PostRepaymentAsync(client, partyId, bankId, 5_000, "ARS", null, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(0, await PartyBalanceAsync(client, partyId, "payableBalances", "ARS", cancellationToken));
    }

    [Fact]
    public async Task Repaying_more_than_i_owe_is_409() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var (bankId, partyId) = await BorrowedAsync(client, "Repay Bank 3", "Carla", 5_000, cancellationToken);
        var response = await PostRepaymentAsync(client, partyId, bankId, 5_001, "ARS", null, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var otherCurrency = await PostRepaymentAsync(client, partyId, bankId, 1, "USD", null, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, otherCurrency.StatusCode);
        Assert.Equal(5_000, await PartyBalanceAsync(client, partyId, "payableBalances", "ARS", cancellationToken));
    }

    [Fact]
    public async Task Undoing_a_repayment_restores_the_bank_and_what_i_owe() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var (bankId, partyId) = await BorrowedAsync(client, "Repay Bank 4", "Dario", 7_000, cancellationToken);
        var response = await PostRepaymentAsync(client, partyId, bankId, 3_000, "ARS", null, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var reverse = await client.PostAsync($"/v1/ledger/transactions/{body.GetProperty("ledgerTransactionId").GetGuid()}/reversal", null, cancellationToken);
        Assert.True(reverse.IsSuccessStatusCode, await reverse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(7_000, await AccountBalanceAsync(client, bankId, cancellationToken));
        Assert.Equal(7_000, await PartyBalanceAsync(client, partyId, "payableBalances", "ARS", cancellationToken));
    }

    [Fact]
    public async Task An_unknown_party_is_404() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Repay Bank 5", "Asset", "Bank", cancellationToken);
        var response = await PostRepaymentAsync(client, Guid.NewGuid(), bankId, 1_000, "ARS", null, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0, "ARS", "bank", 0)]
    [InlineData(100, "EUR", "bank", 0)]
    [InlineData(100, "ARS", "bank", 2)]
    [InlineData(100, "ARS", "expense", 0)]
    public async Task Invalid_repayments_are_rejected_with_422(long amount, string currency, string source, int daysAhead) {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var (bankId, partyId) = await BorrowedAsync(client, $"Repay Bank Invalid {Guid.NewGuid():N}", "Eva", 5_000, cancellationToken);
        var sourceId = source == "bank" ? bankId : await CreateAccountAsync(client, $"Repay Expense {Guid.NewGuid():N}", "Expense", "Expense", cancellationToken);
        var paidOn = DateTime.UtcNow.Date.AddDays(daysAhead).ToString("yyyy-MM-dd");
        var response = await PostRepaymentAsync(client, partyId, sourceId, amount, currency, paidOn, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static async Task<(Guid BankId, Guid PartyId)> BorrowedAsync(HttpClient client, string bankName, string partyName, long amount, CancellationToken cancellationToken) {
        var bankId = await CreateAccountAsync(client, bankName, "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, partyName, cancellationToken);
        var response = await client.PostAsJsonAsync($"/v1/parties/{partyId}/borrowings", new {
            amountMinorUnits = amount,
            destinationAccountId = bankId,
            borrowedOn = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-dd"),
            description = "Loan",
            currencyCode = "ARS"
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (bankId, partyId);
    }

    private static Task<HttpResponseMessage> PostRepaymentAsync(HttpClient client, Guid partyId, Guid sourceAccountId, long amount, string currency, string? paidOn, CancellationToken cancellationToken) {
        return client.PostAsJsonAsync($"/v1/parties/{partyId}/repayments", new {
            amountMinorUnits = amount,
            sourceAccountId,
            paidOn = paidOn ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
            currencyCode = currency
        }, cancellationToken);
    }

    private static async Task<long> PartyBalanceAsync(HttpClient client, Guid partyId, string side, string currency, CancellationToken cancellationToken) {
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/parties/{partyId}/balance", cancellationToken);
        var rows = balance.GetProperty(side).EnumerateArray().Where(row => row.GetProperty("currencyCode").GetString() == currency).ToList();
        return rows.Count == 0 ? 0 : rows[0].GetProperty("balanceMinorUnits").GetInt64();
    }

    private static async Task<long> AccountBalanceAsync(HttpClient client, Guid accountId, CancellationToken cancellationToken) {
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/ledger/accounts/{accountId}/balance", cancellationToken);
        return balance.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("balanceMinorUnits").GetInt64()).FirstOrDefault();
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, string type, string kind, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/ledger/accounts", new { name, type, kind }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("accountId").GetGuid();
    }

    private static async Task<Guid> CreatePartyAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/parties", new { name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}
