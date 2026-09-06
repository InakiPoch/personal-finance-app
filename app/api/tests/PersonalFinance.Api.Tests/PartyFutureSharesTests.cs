using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PartyFutureSharesTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task A_card_split_surfaces_one_scheduled_row_per_unaccrued_installment_for_the_party() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var partyResponse = await client.PostAsJsonAsync("/v1/parties", new { name = "Nadia" }, cancellationToken);
        partyResponse.EnsureSuccessStatusCode();
        var partyId = (await partyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
            .GetProperty("id").GetGuid();

        var cardResponse = await client.PostAsJsonAsync(
            "/v1/instruments",
            new { type = "credit", name = "Visa Future", cutoffDate = 15 },
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

        var response = await client.GetAsync($"/v1/parties/{partyId}/future-shares", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var rows = document.RootElement.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => {
            Assert.Equal(1_500, row.GetProperty("shareMinorUnits").GetInt64());
            Assert.Equal("ARS", row.GetProperty("currencyCode").GetString());
            Assert.Equal("Visa Future — Shared laptop", row.GetProperty("sourceLabel").GetString());
            Assert.True(row.GetProperty("cycleYear").GetInt32() >= 2026);
            Assert.InRange(row.GetProperty("cycleMonth").GetInt32(), 1, 12);
        });
    }

    [Fact]
    public async Task Party_future_shares_route_is_advertised_in_openapi_under_the_parties_tag_with_200() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/parties/{id}/future-shares")
            .GetProperty("get");
        Assert.Equal("Parties", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }
}
