using System.Net;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class CardStatementsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Unknown_card_id_returns_200_with_an_empty_row_list() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var cardId = Guid.NewGuid();
        var response = await client.GetAsync($"/v1/financing/cards/{cardId}/statements", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        Assert.Equal(cardId, root.GetProperty("cardId").GetGuid());
        Assert.Empty(root.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task Card_statements_route_is_advertised_in_openapi_with_200() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/financing/cards/{id}/statements")
            .GetProperty("get");
        Assert.Equal("Financing", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }
}
