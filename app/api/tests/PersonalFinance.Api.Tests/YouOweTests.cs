using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class YouOweTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    private static string MonthOffset(int months) {
        return DateTime.UtcNow.AddMonths(months).ToString("yyyy-MM");
    }

    private static string Today() {
        return DateTime.UtcNow.ToString("yyyy-MM-dd");
    }

    [Fact]
    public async Task Borrowings_show_per_currency_and_repayments_reduce_them() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateBankAsync(client, "Owe Bank 1", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Owe Ana", cancellationToken);
        await BorrowAsync(client, partyId, bankId, 10_000, "ARS", cancellationToken);
        await BorrowAsync(client, partyId, bankId, 6_000, "USD", cancellationToken);
        (await client.PostAsJsonAsync($"/v1/parties/{partyId}/repayments", new {
            amountMinorUnits = 1_000,
            sourceAccountId = bankId,
            paidOn = Today(),
            currencyCode = "USD"
        }, cancellationToken)).EnsureSuccessStatusCode();

        var rows = await GetRowsAsync(client, MonthOffset(0), partyId, cancellationToken);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Owe Ana", rows[0].GetProperty("partyName").GetString());
        Assert.Equal(10_000, AmountFor(rows, "ARS"));
        Assert.Equal(5_000, AmountFor(rows, "USD"));
    }

    [Fact]
    public async Task A_past_month_ignores_entries_dated_after_its_end() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateBankAsync(client, "Owe Bank 2", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Owe Bruno", cancellationToken);
        await BorrowAsync(client, partyId, bankId, 10_000, "ARS", cancellationToken);

        Assert.Empty(await GetRowsAsync(client, MonthOffset(-1), partyId, cancellationToken));
    }

    [Fact]
    public async Task A_future_month_adds_scheduled_installments_due_by_that_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Owe Carla", cancellationToken);
        var first = DateTime.UtcNow.AddMonths(1);
        (await client.PostAsJsonAsync($"/v1/parties/{partyId}/purchases", new {
            shareMinorUnits = 5_000,
            currencyCode = "ARS",
            description = "Fridge",
            categoryName = "Home",
            purchaseDate = Today(),
            kind = "credit",
            installmentCount = 3,
            firstPaymentMonth = new DateOnly(first.Year, first.Month, 1).ToString("yyyy-MM-dd")
        }, cancellationToken)).EnsureSuccessStatusCode();

        Assert.Empty(await GetRowsAsync(client, MonthOffset(0), partyId, cancellationToken));
        Assert.Equal(5_000, AmountFor(await GetRowsAsync(client, MonthOffset(1), partyId, cancellationToken), "ARS"));
        Assert.Equal(15_000, AmountFor(await GetRowsAsync(client, MonthOffset(24), partyId, cancellationToken), "ARS"));
    }

    [Fact]
    public async Task Fully_repaid_parties_are_omitted() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateBankAsync(client, "Owe Bank 3", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Owe Dario", cancellationToken);
        await BorrowAsync(client, partyId, bankId, 4_000, "ARS", cancellationToken);
        (await client.PostAsJsonAsync($"/v1/parties/{partyId}/repayments", new {
            amountMinorUnits = 4_000,
            sourceAccountId = bankId,
            paidOn = Today(),
            currencyCode = "ARS"
        }, cancellationToken)).EnsureSuccessStatusCode();

        Assert.Empty(await GetRowsAsync(client, MonthOffset(0), partyId, cancellationToken));
    }

    [Fact]
    public async Task Route_is_advertised_in_openapi_under_the_reporting_tag_with_200() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement.GetProperty("paths").GetProperty("/v1/reports/parties/you-owe").GetProperty("get");
        Assert.Equal("Reporting", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }

    private static long AmountFor(List<JsonElement> rows, string currency) {
        return rows.Single(row => row.GetProperty("currencyCode").GetString() == currency).GetProperty("amountMinorUnits").GetInt64();
    }

    private static async Task<List<JsonElement>> GetRowsAsync(HttpClient client, string month, Guid partyId, CancellationToken cancellationToken) {
        var response = await client.GetAsync($"/v1/reports/parties/you-owe?month={month}&today={Today()}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return [.. body.GetProperty("rows").EnumerateArray()
            .Where(row => row.GetProperty("partyId").GetGuid() == partyId)
            .OrderBy(row => row.GetProperty("currencyCode").GetString())];
    }

    private static async Task BorrowAsync(HttpClient client, Guid partyId, Guid destinationAccountId, long amount, string currency, CancellationToken cancellationToken) {
        (await client.PostAsJsonAsync($"/v1/parties/{partyId}/borrowings", new {
            amountMinorUnits = amount,
            destinationAccountId,
            borrowedOn = Today(),
            description = "Loan",
            currencyCode = currency
        }, cancellationToken)).EnsureSuccessStatusCode();
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
}
