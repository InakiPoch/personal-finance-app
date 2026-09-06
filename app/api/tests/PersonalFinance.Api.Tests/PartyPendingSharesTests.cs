using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PartyPendingSharesTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Pending_shares_lists_a_party_with_a_fresh_card_split_as_scheduled_but_zero_now() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var partyResponse = await client.PostAsJsonAsync("/v1/parties", new { name = "Ophelia" }, cancellationToken);
        partyResponse.EnsureSuccessStatusCode();
        var partyId = (await partyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
            .GetProperty("id").GetGuid();

        var cardResponse = await client.PostAsJsonAsync(
            "/v1/instruments",
            new { type = "credit", name = "Visa Pending", cutoffDate = 15 },
            cancellationToken
        );
        cardResponse.EnsureSuccessStatusCode();
        var cardId = (await cardResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
            .GetProperty("id").GetGuid();

        var planResponse = await client.PostAsJsonAsync(
            "/v1/financing/payment-plans",
            new {
                amountMinorUnits = 9_000,
                cardId,
                installmentCount = 3,
                purchaseDate = "2026-01-10",
                description = "Shared laptop",
                split = new[] { new { partyId, weight = 1L } }
            },
            cancellationToken
        );
        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var response = await client.GetAsync("/v1/parties/pending-shares", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var row = document.RootElement.GetProperty("rows").EnumerateArray()
            .Single(row => row.GetProperty("partyId").GetGuid() == partyId);
        Assert.Equal(3, row.GetProperty("scheduledCount").GetInt32());
        Assert.Equal(4_500, row.GetProperty("scheduledTotalMinorUnits").GetInt64());
        Assert.Equal("ARS", row.GetProperty("currencyCode").GetString());
    }

    [Fact]
    public async Task Pending_shares_route_is_advertised_in_openapi_under_the_parties_tag_with_200() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/parties/pending-shares")
            .GetProperty("get");
        Assert.Equal("Parties", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }
}
