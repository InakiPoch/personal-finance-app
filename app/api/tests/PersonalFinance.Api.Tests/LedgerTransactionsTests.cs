using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class LedgerTransactionsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Feed_lists_every_posted_transaction_and_flags_the_reversed_pair() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var accountA = await registerDebitAccount(client, "Feed Checking A", cancellationToken);
        var accountB = await registerDebitAccount(client, "Feed Checking B", cancellationToken);
        var accountC = await registerDebitAccount(client, "Feed Checking C", cancellationToken);
        var firstId = await postTransfer(client, accountA, accountB, 500_000, "2026-09-01T10:00:00Z", cancellationToken);
        await postTransfer(client, accountC, accountA, 250_000, "2026-09-02T10:00:00Z", cancellationToken);
        var reversal = await client.PostAsync($"/v1/ledger/transactions/{firstId}/reversal", content: null, cancellationToken);
        reversal.EnsureSuccessStatusCode();
        var reversalId = (await reversal.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)) .GetProperty("reversalTransactionId").GetGuid();
        var feed = await client.GetFromJsonAsync<JsonElement>($"/v1/ledger/transactions?accountId={accountA}", cancellationToken);
        var rows = feed.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(3, rows.Count);

        var reversalRow = rows.Single(row => row.GetProperty("transactionId").GetGuid() == reversalId);
        Assert.True(reversalRow.GetProperty("isReversal").GetBoolean());
        Assert.Equal("Reversal", reversalRow.GetProperty("description").GetString());

        var firstRow = rows.Single(row => row.GetProperty("transactionId").GetGuid() == firstId);
        Assert.False(firstRow.GetProperty("isReversal").GetBoolean());
        Assert.True(firstRow.GetProperty("isReversed").GetBoolean());
        Assert.Equal("Manual entry", firstRow.GetProperty("description").GetString());
        Assert.Equal(500_000, firstRow.GetProperty("amountMinorUnits").GetInt64());
    }

    [Fact]
    public async Task Account_filter_narrows_the_feed_to_transactions_touching_that_account() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var accountA = await registerDebitAccount(client, "Filter Checking A", cancellationToken);
        var accountB = await registerDebitAccount(client, "Filter Checking B", cancellationToken);
        var accountC = await registerDebitAccount(client, "Filter Checking C", cancellationToken);
        await postTransfer(client, accountA, accountB, 500_000, "2026-09-01T10:00:00Z", cancellationToken);
        var onlyC = await postTransfer(client, accountC, accountA, 250_000, "2026-09-02T10:00:00Z", cancellationToken);
        var feed = await client.GetFromJsonAsync<JsonElement>($"/v1/ledger/transactions?accountId={accountC}", cancellationToken);
        var rows = feed.GetProperty("rows").EnumerateArray().ToList();
        var row = Assert.Single(rows);
        Assert.Equal(onlyC, row.GetProperty("transactionId").GetGuid());
    }

    [Fact]
    public async Task Date_range_filter_excludes_transactions_posted_outside_the_window() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var accountA = await registerDebitAccount(client, "Window Checking A", cancellationToken);
        var accountB = await registerDebitAccount(client, "Window Checking B", cancellationToken);
        var inWindow = await postTransfer(client, accountA, accountB, 500_000, "2026-09-01T10:00:00Z", cancellationToken);
        await postTransfer(client, accountA, accountB, 250_000, "2026-09-20T10:00:00Z", cancellationToken);
        var feed = await client.GetFromJsonAsync<JsonElement>($"/v1/ledger/transactions?accountId={accountA}&from=2026-09-01&to=2026-09-05", cancellationToken);
        var rows = feed.GetProperty("rows").EnumerateArray().ToList();
        var row = Assert.Single(rows);
        Assert.Equal(inWindow, row.GetProperty("transactionId").GetGuid());
    }

    [Fact]
    public async Task Transactions_feed_route_is_advertised_in_openapi_under_the_ledger_tag_with_200() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/ledger/transactions")
            .GetProperty("get");
        Assert.Equal("Ledger", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }

    private static async Task<Guid> registerDebitAccount(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/instruments", new { type = "debit", name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> postTransfer(
        HttpClient client,
        Guid debitAccountId,
        Guid creditAccountId,
        long amountMinorUnits,
        string postedOnUtc,
        CancellationToken cancellationToken) {
        var body = new {
            lines = new[] {
                new { accountId = debitAccountId, direction = "Debit", amountMinorUnits },
                new { accountId = creditAccountId, direction = "Credit", amountMinorUnits }
            },
            postedOnUtc
        };
        var response = await client.PostAsJsonAsync("/v1/ledger/transactions", body, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return result.GetProperty("transactionId").GetGuid();
    }
}
