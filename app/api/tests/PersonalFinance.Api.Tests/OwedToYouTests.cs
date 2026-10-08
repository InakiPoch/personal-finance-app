using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class OwedToYouTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    private static string MonthOffset(int months) {
        return DateTime.UtcNow.AddMonths(months).ToString("yyyy-MM");
    }

    private static string Today() {
        return DateTime.UtcNow.ToString("yyyy-MM-dd");
    }

    [Fact]
    public async Task Debit_split_share_and_partial_settlement_show_the_remaining_balance_for_the_current_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateBankAsync(client, "Owed Bank 1", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Owed Ana", cancellationToken);
        await RecordSplitExpenseAsync(client, bankId, partyId, 10_000, "ARS", cancellationToken);
        await RecordSplitExpenseAsync(client, bankId, partyId, 6_000, "USD", cancellationToken);
        await client.PostAsJsonAsync($"/v1/parties/{partyId}/settlements", new {
            amountMinorUnits = 1_000,
            bankAccountId = bankId,
            settledOnUtc = DateTimeOffset.UtcNow,
            currencyCode = "USD"
        }, cancellationToken);

        var rows = await GetRowsAsync(client, MonthOffset(0), partyId, cancellationToken);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Owed Ana", rows[0].GetProperty("partyName").GetString());
        Assert.Equal(5_000, AmountFor(rows, "ARS"));
        Assert.Equal(2_000, AmountFor(rows, "USD"));
    }

    [Fact]
    public async Task A_past_month_ignores_entries_dated_after_its_end() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateBankAsync(client, "Owed Bank 2", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Owed Bruno", cancellationToken);
        await RecordSplitExpenseAsync(client, bankId, partyId, 10_000, "ARS", cancellationToken);

        var rows = await GetRowsAsync(client, MonthOffset(-1), partyId, cancellationToken);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task A_future_month_adds_scheduled_card_split_shares_up_to_that_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Owed Carla", cancellationToken);
        await CreateCardSplitPlanAsync(client, "Owed Visa", partyId, cancellationToken);

        var farFuture = await GetRowsAsync(client, MonthOffset(24), partyId, cancellationToken);
        var current = await GetRowsAsync(client, MonthOffset(0), partyId, cancellationToken);

        Assert.Equal(4_500, AmountFor(farFuture, "ARS"));
        Assert.True(current.Sum(row => row.GetProperty("amountMinorUnits").GetInt64()) < 4_500);
    }

    [Fact]
    public async Task Fully_settled_parties_are_omitted() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateBankAsync(client, "Owed Bank 3", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Owed Dario", cancellationToken);
        await RecordSplitExpenseAsync(client, bankId, partyId, 10_000, "ARS", cancellationToken);
        await client.PostAsJsonAsync($"/v1/parties/{partyId}/settlements", new {
            amountMinorUnits = 5_000,
            bankAccountId = bankId,
            settledOnUtc = DateTimeOffset.UtcNow,
            currencyCode = "ARS"
        }, cancellationToken);

        var rows = await GetRowsAsync(client, MonthOffset(0), partyId, cancellationToken);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Route_is_advertised_in_openapi_under_the_reporting_tag_with_200() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement.GetProperty("paths").GetProperty("/v1/reports/parties/owed-to-you").GetProperty("get");
        Assert.Equal("Reporting", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }

    private static long AmountFor(List<JsonElement> rows, string currency) {
        return rows.Single(row => row.GetProperty("currencyCode").GetString() == currency).GetProperty("amountMinorUnits").GetInt64();
    }

    private static async Task<List<JsonElement>> GetRowsAsync(HttpClient client, string month, Guid partyId, CancellationToken cancellationToken) {
        var response = await client.GetAsync($"/v1/reports/parties/owed-to-you?month={month}&today={Today()}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("rows").EnumerateArray()
            .Where(row => row.GetProperty("partyId").GetGuid() == partyId)
            .OrderBy(row => row.GetProperty("currencyCode").GetString())
            .ToList();
    }

    private static async Task<Guid> CreateBankAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/ledger/accounts", new { name, type = "Asset", kind = "Bank" }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("accountId").GetGuid();
    }

    private static async Task<Guid> CreatePartyAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/parties", new { name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
    }

    private static async Task RecordSplitExpenseAsync(HttpClient client, Guid sourceInstrumentId, Guid partyId, long amountMinorUnits, string currencyCode, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/ledger/expenses", new {
            description = $"{currencyCode} split",
            amountMinorUnits,
            categoryName = "Owed Dining",
            sourceInstrumentId,
            purchaseDate = Today(),
            split = new[] { new { partyId, weight = 1L } },
            currencyCode
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task CreateCardSplitPlanAsync(HttpClient client, string cardName, Guid partyId, CancellationToken cancellationToken) {
        var cardResponse = await client.PostAsJsonAsync("/v1/instruments", new { type = "credit", name = cardName, cutoffDate = 15 }, cancellationToken);
        cardResponse.EnsureSuccessStatusCode();
        var cardId = (await cardResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        var planResponse = await client.PostAsJsonAsync("/v1/financing/payment-plans", new {
            amountMinorUnits = 9_000,
            cardId,
            installmentCount = 3,
            purchaseDate = Today(),
            description = "Shared laptop",
            split = new[] { new { partyId, weight = 1L } }
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);
    }
}
