using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class RecordIncomeTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Records_an_income_and_surfaces_it_on_monthly_incomes() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var checkingId = await RegisterDebitAsync(client, "Income checking", cancellationToken);
        var response = await client.PostAsJsonAsync("/v1/ledger/incomes", new {
            amountMinorUnits = 500_00,
            targetAccountId = checkingId,
            receivedOn = "2026-03-10",
            description = "Salary"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.NotEqual(Guid.Empty, created.GetProperty("id").GetGuid());
        var monthly = await client.GetFromJsonAsync<JsonElement>("/v1/reports/monthly-incomes?month=2026-03", cancellationToken);
        var row = Assert.Single(monthly.GetProperty("rows").EnumerateArray());
        Assert.Equal("2026-03", row.GetProperty("month").GetString());
        Assert.Equal(500_00, row.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("ARS", row.GetProperty("currencyCode").GetString());
    }

    [Fact]
    public async Task Rejects_a_future_received_on_date_with_422() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var checkingId = await RegisterDebitAsync(client, "Future income checking", cancellationToken);
        var response = await client.PostAsJsonAsync("/v1/ledger/incomes", new {
            amountMinorUnits = 100_00,
            targetAccountId = checkingId,
            receivedOn = "2099-01-01",
            description = "Salary"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("Ledger.IncomeDateInFuture", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Rejects_an_unknown_target_account_with_404() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/ledger/incomes", new {
            amountMinorUnits = 100_00,
            targetAccountId = Guid.NewGuid(),
            receivedOn = "2026-03-10",
            description = "Salary"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("Ledger.AccountNotFound", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Incomes_route_is_advertised_in_openapi_under_the_ledger_tag_with_201() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var post = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/ledger/incomes")
            .GetProperty("post");
        Assert.Equal("Ledger", post.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(post.GetProperty("responses").TryGetProperty("201", out _));
    }

    private static async Task<Guid> RegisterDebitAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/instruments", new { type = "debit", name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}
