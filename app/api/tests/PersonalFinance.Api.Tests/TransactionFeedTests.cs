using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class TransactionFeedTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Feed_lists_every_posted_transaction_and_flags_the_undone_pair() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var accountA = await registerDebitAccount(client, "Feed Checking A", cancellationToken);
        var accountB = await registerDebitAccount(client, "Feed Checking B", cancellationToken);
        var accountC = await registerDebitAccount(client, "Feed Checking C", cancellationToken);
        var firstId = await postTransfer(client, accountA, accountB, 500_000, "2026-09-01T10:00:00Z", cancellationToken);
        await postTransfer(client, accountC, accountA, 250_000, "2026-09-02T10:00:00Z", cancellationToken);
        var reversal = await client.PostAsync($"/v1/ledger/transactions/{firstId}/reversal", content: null, cancellationToken);
        reversal.EnsureSuccessStatusCode();
        var reversalId = (await reversal.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("reversalTransactionId").GetGuid();
        var feed = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/transactions?accountId={accountA}", cancellationToken);
        var rows = feed.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(3, rows.Count);

        var undoRow = rows.Single(row => row.GetProperty("id").GetGuid() == reversalId);
        Assert.True(undoRow.GetProperty("isUndoEntry").GetBoolean());
        Assert.Equal("Undo entry", undoRow.GetProperty("kind").GetString());
        Assert.Equal("Undid: Manual entry", undoRow.GetProperty("description").GetString());
        Assert.Empty(undoRow.GetProperty("impactLines").EnumerateArray());

        var firstRow = rows.Single(row => row.GetProperty("id").GetGuid() == firstId);
        Assert.False(firstRow.GetProperty("isUndoEntry").GetBoolean());
        Assert.True(firstRow.GetProperty("isUndone").GetBoolean());
        Assert.Equal("Manual entry", firstRow.GetProperty("description").GetString());
        Assert.Equal(500_000, firstRow.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("Already undone.", firstRow.GetProperty("impactLines")[0].GetString());
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
        var feed = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/transactions?accountId={accountC}", cancellationToken);
        var row = Assert.Single(feed.GetProperty("rows").EnumerateArray());
        Assert.Equal(onlyC, row.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Date_range_filter_is_inclusive_and_excludes_transactions_outside_the_window() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var accountA = await registerDebitAccount(client, "Window Checking A", cancellationToken);
        var accountB = await registerDebitAccount(client, "Window Checking B", cancellationToken);
        var onFrom = await postTransfer(client, accountA, accountB, 500_000, "2026-09-01T10:00:00Z", cancellationToken);
        var onTo = await postTransfer(client, accountA, accountB, 300_000, "2026-09-05T23:00:00Z", cancellationToken);
        await postTransfer(client, accountA, accountB, 250_000, "2026-09-20T10:00:00Z", cancellationToken);
        var feed = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/transactions?accountId={accountA}&from=2026-09-01&to=2026-09-05", cancellationToken);
        var ids = feed.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(2, ids.Count);
        Assert.Contains(onFrom, ids);
        Assert.Contains(onTo, ids);
    }

    [Fact]
    public async Task Get_by_id_returns_one_explained_row() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var accountA = await registerDebitAccount(client, "Single Checking A", cancellationToken);
        var accountB = await registerDebitAccount(client, "Single Checking B", cancellationToken);
        var id = await postTransfer(client, accountA, accountB, 500_000, "2026-09-01T10:00:00Z", cancellationToken);
        var response = await client.GetAsync($"/v1/reports/transactions/{id}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(id, row.GetProperty("id").GetGuid());
        Assert.Equal("Other", row.GetProperty("kind").GetString());
        Assert.Equal(["Single Checking A"], row.GetProperty("fromAccounts").EnumerateArray().Select(name => name.GetString()).ToList());
        Assert.Equal(["Single Checking B"], row.GetProperty("toAccounts").EnumerateArray().Select(name => name.GetString()).ToList());
    }

    [Fact]
    public async Task Unknown_transaction_id_is_404_with_the_domain_code() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync($"/v1/reports/transactions/{Guid.NewGuid()}", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(404, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Reporting.TransactionNotFound", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Both_routes_are_advertised_in_openapi_under_the_reporting_tag() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        var feed = paths.GetProperty("/v1/reports/transactions").GetProperty("get");
        Assert.Equal("Reporting", feed.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(feed.GetProperty("responses").TryGetProperty("200", out _));
        var single = paths.GetProperty("/v1/reports/transactions/{id}").GetProperty("get");
        Assert.Equal("Reporting", single.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(single.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(single.GetProperty("responses").TryGetProperty("404", out _));
    }

    [Fact]
    public async Task The_old_ledger_feed_route_is_gone_but_the_reversal_route_stays() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        Assert.False(paths.TryGetProperty("/v1/ledger/transactions", out var old) && old.TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/v1/ledger/transactions/{id}/reversal").TryGetProperty("post", out _));
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
