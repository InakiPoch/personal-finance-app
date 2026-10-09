using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PartyBorrowingTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Recording_a_borrowing_raises_the_bank_and_what_i_owe_without_touching_the_receivable() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Borrow Bank 1", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Lucia", cancellationToken);
        var response = await PostBorrowingAsync(client, partyId, bankId, 20_000, "ARS", "Rent gap", null, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(20_000, await AccountBalanceAsync(client, bankId, cancellationToken));
        Assert.Equal(20_000, await PartyBalanceAsync(client, partyId, "payableBalances", "ARS", cancellationToken));
        Assert.Equal(0, await PartyBalanceAsync(client, partyId, "balances", "ARS", cancellationToken));
    }

    [Fact]
    public async Task Both_sides_are_tracked_per_currency_and_never_netted() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Borrow Bank 2", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Bruno", cancellationToken);
        (await PostBorrowingAsync(client, partyId, bankId, 9_000, "USD", "Gear", null, cancellationToken)).EnsureSuccessStatusCode();
        (await PostBorrowingAsync(client, partyId, bankId, 4_000, "ARS", "Gas", null, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(9_000, await PartyBalanceAsync(client, partyId, "payableBalances", "USD", cancellationToken));
        Assert.Equal(4_000, await PartyBalanceAsync(client, partyId, "payableBalances", "ARS", cancellationToken));
    }

    [Fact]
    public async Task The_borrowing_shows_on_the_i_owe_timeline_only() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Borrow Bank 3", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Carla", cancellationToken);
        await PostBorrowingAsync(client, partyId, bankId, 5_000, "ARS", "Trip", null, cancellationToken);
        var owe = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/parties/{partyId}/timeline?side=payable", cancellationToken);
        var row = Assert.Single(owe.GetProperty("rows").EnumerateArray());
        Assert.Equal("Trip", row.GetProperty("description").GetString());
        Assert.Equal(5_000, row.GetProperty("deltaMinorUnits").GetInt64());
        Assert.Equal(5_000, row.GetProperty("runningBalanceMinorUnits").GetInt64());
        var owed = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/parties/{partyId}/timeline", cancellationToken);
        Assert.Empty(owed.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task Undoing_a_borrowing_restores_the_bank_and_what_i_owe() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Borrow Bank 4", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Dario", cancellationToken);
        var response = await PostBorrowingAsync(client, partyId, bankId, 7_000, "ARS", "Loan", null, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var reverse = await client.PostAsync($"/v1/ledger/transactions/{body.GetProperty("ledgerTransactionId").GetGuid()}/reversal", null, cancellationToken);
        Assert.True(reverse.IsSuccessStatusCode, await reverse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(0, await AccountBalanceAsync(client, bankId, cancellationToken));
        Assert.Equal(0, await PartyBalanceAsync(client, partyId, "payableBalances", "ARS", cancellationToken));
        var owe = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/parties/{partyId}/timeline?side=payable", cancellationToken);
        Assert.Equal("Reversal", owe.GetProperty("rows").EnumerateArray().Last().GetProperty("description").GetString());
    }

    [Fact]
    public async Task An_unknown_party_is_404() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Borrow Bank 5", "Asset", "Bank", cancellationToken);
        var response = await PostBorrowingAsync(client, Guid.NewGuid(), bankId, 1_000, "ARS", "Loan", null, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0, "ARS", "Loan", "bank", 0)]
    [InlineData(1_000, "EUR", "Loan", "bank", 0)]
    [InlineData(1_000, "ARS", "", "bank", 0)]
    [InlineData(1_000, "ARS", "two\nlines", "bank", 0)]
    [InlineData(1_000, "ARS", "LONG", "bank", 0)]
    [InlineData(1_000, "ARS", "Loan", "bank", 2)]
    [InlineData(1_000, "ARS", "Loan", "expense", 0)]
    public async Task Invalid_borrowings_are_rejected_with_422(long amount, string currency, string description, string destination, int daysAhead) {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, $"Borrow Bank Invalid {Guid.NewGuid():N}", "Asset", "Bank", cancellationToken);
        var destinationId = destination == "bank" ? bankId : await CreateAccountAsync(client, $"Borrow Expense {Guid.NewGuid():N}", "Expense", "Expense", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Eva", cancellationToken);
        var text = description == "LONG" ? new string('x', 121) : description;
        var borrowedOn = DateTime.UtcNow.Date.AddDays(daysAhead).ToString("yyyy-MM-dd");
        var response = await PostBorrowingAsync(client, partyId, destinationId, amount, currency, text, borrowedOn, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PostBorrowingAsync(HttpClient client, Guid partyId, Guid destinationAccountId, long amount, string currency, string description, string? borrowedOn, CancellationToken cancellationToken) {
        return client.PostAsJsonAsync($"/v1/parties/{partyId}/borrowings", new {
            amountMinorUnits = amount,
            destinationAccountId,
            borrowedOn = borrowedOn ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
            description,
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
