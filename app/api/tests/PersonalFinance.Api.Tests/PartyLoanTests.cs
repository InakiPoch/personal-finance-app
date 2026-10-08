using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PartyLoanTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Recording_a_loan_raises_the_party_balance_and_lowers_the_source_account() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Loan Bank 1", "Asset", "Bank", cancellationToken);
        await FundAsync(client, bankId, 50_000, cancellationToken);
        var partyId = await CreatePartyAsync(client, "Lola", cancellationToken);

        var response = await PostLoanAsync(client, partyId, bankId, 12_000, "ARS", "Rent help", null, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(12_000, await PartyBalanceAsync(client, partyId, "ARS", cancellationToken));
        Assert.Equal(38_000, await AccountBalanceAsync(client, bankId, cancellationToken));
    }

    [Fact]
    public async Task The_loan_shows_on_the_timeline_as_lent_to_on_its_lent_on_date() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Loan Bank 2", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Mateo", cancellationToken);
        var lentOn = DateTime.UtcNow.Date.AddDays(-3);

        await PostLoanAsync(client, partyId, bankId, 5_000, "USD", "Trip", lentOn.ToString("yyyy-MM-dd"), cancellationToken);

        var timeline = await client.GetFromJsonAsync<JsonElement>($"/v1/parties/{partyId}/timeline", cancellationToken);
        var row = Assert.Single(timeline.GetProperty("rows").EnumerateArray());
        Assert.Equal("Lent to Mateo", row.GetProperty("description").GetString());
        Assert.Equal(5_000, row.GetProperty("deltaMinorUnits").GetInt64());
        Assert.Equal(lentOn.Date, row.GetProperty("movementOnUtc").GetDateTimeOffset().UtcDateTime.Date);
    }

    [Fact]
    public async Task A_partial_settlement_pays_down_the_loan() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Loan Bank 3", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Nora", cancellationToken);
        await PostLoanAsync(client, partyId, bankId, 10_000, "ARS", "Loan", null, cancellationToken);

        var settle = await client.PostAsJsonAsync($"/v1/parties/{partyId}/settlements", new {
            amountMinorUnits = 4_000,
            bankAccountId = bankId,
            settledOnUtc = DateTimeOffset.UtcNow,
            currencyCode = "ARS"
        }, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, settle.StatusCode);
        Assert.Equal(6_000, await PartyBalanceAsync(client, partyId, "ARS", cancellationToken));
    }

    [Fact]
    public async Task Undoing_the_loan_restores_both_balances() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Loan Bank 4", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Omar", cancellationToken);
        var response = await PostLoanAsync(client, partyId, bankId, 7_000, "ARS", "Loan", null, cancellationToken);
        var loan = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var transactionId = loan.GetProperty("ledgerTransactionId").GetGuid();

        var reverse = await client.PostAsync($"/v1/ledger/transactions/{transactionId}/reversal", null, cancellationToken);

        Assert.True(reverse.IsSuccessStatusCode, await reverse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(0, await PartyBalanceAsync(client, partyId, "ARS", cancellationToken));
        Assert.Equal(0, await AccountBalanceAsync(client, bankId, cancellationToken));
    }

    [Fact]
    public async Task An_unknown_party_is_404() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Loan Bank 5", "Asset", "Bank", cancellationToken);

        var response = await PostLoanAsync(client, Guid.NewGuid(), bankId, 1_000, "ARS", "Loan", null, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0, "ARS", "Loan", "bank", 0)]
    [InlineData(-5, "ARS", "Loan", "bank", 0)]
    [InlineData(1_000, "EUR", "Loan", "bank", 0)]
    [InlineData(1_000, "ARS", "", "bank", 0)]
    [InlineData(1_000, "ARS", "two\nlines", "bank", 0)]
    [InlineData(1_000, "ARS", "LONG", "bank", 0)]
    [InlineData(1_000, "ARS", "Loan", "bank", 2)]
    [InlineData(1_000, "ARS", "Loan", "expense", 0)]
    public async Task Invalid_loans_are_rejected_with_422(long amount, string currency, string description, string source, int daysAhead) {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, $"Loan Bank Invalid {Guid.NewGuid():N}", "Asset", "Bank", cancellationToken);
        var sourceId = source == "bank" ? bankId : await CreateAccountAsync(client, $"Loan Expense {Guid.NewGuid():N}", "Expense", "Expense", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Pia", cancellationToken);
        var text = description == "LONG" ? new string('x', 121) : description;
        var lentOn = DateTime.UtcNow.Date.AddDays(daysAhead).ToString("yyyy-MM-dd");

        var response = await PostLoanAsync(client, partyId, sourceId, amount, currency, text, lentOn, cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PostLoanAsync(HttpClient client, Guid partyId, Guid sourceAccountId, long amount, string currency, string description, string? lentOn, CancellationToken cancellationToken) {
        return client.PostAsJsonAsync($"/v1/parties/{partyId}/loans", new {
            amountMinorUnits = amount,
            sourceAccountId,
            lentOn = lentOn ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
            description,
            currencyCode = currency
        }, cancellationToken);
    }

    private static async Task<long> PartyBalanceAsync(HttpClient client, Guid partyId, string currency, CancellationToken cancellationToken) {
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/parties/{partyId}/balance", cancellationToken);
        var rows = balance.GetProperty("balances").EnumerateArray().Where(row => row.GetProperty("currencyCode").GetString() == currency).ToList();
        return rows.Count == 0 ? 0 : rows[0].GetProperty("balanceMinorUnits").GetInt64();
    }

    private static async Task<long> AccountBalanceAsync(HttpClient client, Guid accountId, CancellationToken cancellationToken) {
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/ledger/accounts/{accountId}/balance", cancellationToken);
        return balance.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("balanceMinorUnits").GetInt64()).FirstOrDefault();
    }

    private static async Task FundAsync(HttpClient client, Guid bankId, long amount, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/ledger/incomes", new {
            amountMinorUnits = amount,
            targetAccountId = bankId,
            receivedOn = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            description = "Seed",
            currencyCode = "ARS"
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
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
