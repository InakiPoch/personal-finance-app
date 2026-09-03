using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class InstrumentsListTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Registered_debit_and_credit_instruments_are_listed_with_their_type_and_cutoff() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var debit = await client.PostAsJsonAsync("/v1/instruments", new { type = "debit", name = "Everyday Checking" }, cancellationToken);
        var credit = await client.PostAsJsonAsync("/v1/instruments", new { type = "credit", name = "Rewards Visa", cutoffDate = 15 }, cancellationToken);
        debit.EnsureSuccessStatusCode();
        credit.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/v1/instruments", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var rows = document.RootElement.GetProperty("rows").EnumerateArray().ToList();

        var debitRow = rows.Single(row => row.GetProperty("name").GetString() == "Everyday Checking");
        Assert.Equal("debit", debitRow.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, debitRow.GetProperty("cutoffDate").ValueKind);

        var creditRow = rows.Single(row => row.GetProperty("name").GetString() == "Rewards Visa");
        Assert.Equal("credit", creditRow.GetProperty("type").GetString());
        Assert.Equal(15, creditRow.GetProperty("cutoffDate").GetInt32());
    }

    [Fact]
    public async Task Instruments_list_route_is_advertised_in_openapi_under_the_instruments_tag_with_200() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/instruments")
            .GetProperty("get");
        Assert.Equal("Instruments", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }
}
