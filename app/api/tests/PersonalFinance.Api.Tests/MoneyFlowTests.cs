using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class MoneyFlowTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Rejects_a_missing_month_with_a_4xx() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/reports/money-flow", cancellationToken);
        Assert.True((int)response.StatusCode is >= 400 and < 500, $"Expected a 4xx, got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task Returns_an_income_row_for_the_requested_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var checkingId = await RegisterDebitAsync(client, "Money flow checking", cancellationToken);
        await client.PostAsJsonAsync("/v1/ledger/incomes", new {
            amountMinorUnits = 250_00,
            targetAccountId = checkingId,
            receivedOn = "2026-04-10",
            description = "Freelance"
        }, cancellationToken);
        var response = await client.GetAsync("/v1/reports/money-flow?month=2026-04", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var row = Assert.Single(body.GetProperty("rows").EnumerateArray());
        Assert.Equal("Income", row.GetProperty("kind").GetString());
        Assert.Equal("Freelance", row.GetProperty("description").GetString());
        Assert.Equal("Money flow checking", row.GetProperty("accountName").GetString());
        Assert.Equal(250_00, row.GetProperty("amountMinorUnits").GetInt64());
    }

    [Fact]
    public async Task Route_is_advertised_in_openapi_under_the_reporting_tag_with_200() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/reports/money-flow")
            .GetProperty("get");
        Assert.Equal("Reporting", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }

    private static async Task<Guid> RegisterDebitAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/instruments", new { type = "debit", name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}
