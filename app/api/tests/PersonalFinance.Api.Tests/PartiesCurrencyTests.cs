using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PartiesCurrencyTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task A_party_with_an_ars_and_a_usd_split_gets_two_separated_balances() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Currency Bank", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Dana", cancellationToken);

        await RecordSplitExpenseAsync(client, bankId, partyId, 10_000, "ARS", cancellationToken);
        await RecordSplitExpenseAsync(client, bankId, partyId, 6_000, "USD", cancellationToken);

        var balances = await GetBalancesAsync(client, partyId, cancellationToken);
        Assert.Equal(2, balances.Count);
        Assert.Equal(5_000, balances.Single(row => row.GetProperty("currencyCode").GetString() == "ARS").GetProperty("balanceMinorUnits").GetInt64());
        Assert.Equal(3_000, balances.Single(row => row.GetProperty("currencyCode").GetString() == "USD").GetProperty("balanceMinorUnits").GetInt64());
    }

    [Fact]
    public async Task Settling_the_usd_balance_leaves_the_ars_balance_untouched() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Currency Bank 2", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Ezra", cancellationToken);

        await RecordSplitExpenseAsync(client, bankId, partyId, 10_000, "ARS", cancellationToken);
        await RecordSplitExpenseAsync(client, bankId, partyId, 6_000, "USD", cancellationToken);

        var settleResponse = await client.PostAsJsonAsync($"/v1/parties/{partyId}/settlements", new {
            amountMinorUnits = 1_000,
            bankAccountId = bankId,
            settledOnUtc = DateTimeOffset.UtcNow,
            currencyCode = "USD"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, settleResponse.StatusCode);

        var balances = await GetBalancesAsync(client, partyId, cancellationToken);
        Assert.Equal(5_000, balances.Single(row => row.GetProperty("currencyCode").GetString() == "ARS").GetProperty("balanceMinorUnits").GetInt64());
        Assert.Equal(2_000, balances.Single(row => row.GetProperty("currencyCode").GetString() == "USD").GetProperty("balanceMinorUnits").GetInt64());
    }

    [Fact]
    public async Task Rejects_an_unsupported_currency_code_on_a_split_expense_with_422() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Currency Bank 3", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Farid", cancellationToken);

        var response = await PostSplitExpenseAsync(client, bankId, partyId, 1_000, "EUR", cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("Ledger.InvalidCurrencyCode", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_removed_shared_expenses_route_is_gone() {
        var response = await factory.CreateClient().PostAsJsonAsync("/v1/parties/shared-expenses", new { }, TestContext.Current.CancellationToken);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    [Fact]
    public async Task Timeline_running_balance_never_crosses_currencies() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Currency Bank 4", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Gaia", cancellationToken);

        await RecordSplitExpenseAsync(client, bankId, partyId, 10_000, "ARS", cancellationToken);
        await RecordSplitExpenseAsync(client, bankId, partyId, 6_000, "USD", cancellationToken);
        await client.PostAsJsonAsync($"/v1/parties/{partyId}/settlements", new {
            amountMinorUnits = 1_000,
            bankAccountId = bankId,
            settledOnUtc = DateTimeOffset.UtcNow,
            currencyCode = "USD"
        }, cancellationToken);

        var timeline = await client.GetFromJsonAsync<JsonElement>($"/v1/parties/{partyId}/timeline", cancellationToken);
        var rows = timeline.GetProperty("rows").EnumerateArray().ToList();
        var arsRows = rows.Where(row => row.GetProperty("currencyCode").GetString() == "ARS").ToList();
        var usdRows = rows.Where(row => row.GetProperty("currencyCode").GetString() == "USD").ToList();
        Assert.Single(arsRows);
        Assert.Equal(5_000, arsRows[0].GetProperty("runningBalanceMinorUnits").GetInt64());
        Assert.Equal(2, usdRows.Count);
        Assert.Equal(3_000, usdRows[0].GetProperty("runningBalanceMinorUnits").GetInt64());
        Assert.Equal(2_000, usdRows[1].GetProperty("runningBalanceMinorUnits").GetInt64());
    }

    [Fact]
    public async Task Debt_summary_reports_one_row_per_currency_for_the_same_party() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Currency Bank 5", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Hiro", cancellationToken);

        await RecordSplitExpenseAsync(client, bankId, partyId, 10_000, "ARS", cancellationToken);
        await RecordSplitExpenseAsync(client, bankId, partyId, 6_000, "USD", cancellationToken);

        var summary = await client.GetFromJsonAsync<JsonElement>("/v1/reports/parties/debt-summary", cancellationToken);
        var rows = summary.GetProperty("rows").EnumerateArray()
            .Where(row => row.GetProperty("partyId").GetGuid() == partyId)
            .ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(5_000, rows.Single(row => row.GetProperty("currencyCode").GetString() == "ARS").GetProperty("netBalanceMinorUnits").GetInt64());
        Assert.Equal(3_000, rows.Single(row => row.GetProperty("currencyCode").GetString() == "USD").GetProperty("netBalanceMinorUnits").GetInt64());
    }

    private static async Task<List<JsonElement>> GetBalancesAsync(HttpClient client, Guid partyId, CancellationToken cancellationToken) {
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/parties/{partyId}/balance", cancellationToken);
        return balance.GetProperty("balances").EnumerateArray().ToList();
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

    private static async Task RecordSplitExpenseAsync(HttpClient client, Guid sourceInstrumentId, Guid partyId, long amountMinorUnits, string currencyCode, CancellationToken cancellationToken) {
        var response = await PostSplitExpenseAsync(client, sourceInstrumentId, partyId, amountMinorUnits, currencyCode, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PostSplitExpenseAsync(HttpClient client, Guid sourceInstrumentId, Guid partyId, long amountMinorUnits, string currencyCode, CancellationToken cancellationToken) {
        return client.PostAsJsonAsync("/v1/ledger/expenses", new {
            description = $"{currencyCode} split",
            amountMinorUnits,
            categoryName = "Currency Dining",
            sourceInstrumentId,
            purchaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            split = new[] { new { partyId, weight = 1L } },
            currencyCode
        }, cancellationToken);
    }
}
