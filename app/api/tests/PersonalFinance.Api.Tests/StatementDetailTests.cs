using System.Net;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class StatementDetailTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Unknown_statement_id_is_404_with_the_domain_code() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync($"/v1/financing/statements/{Guid.NewGuid()}", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("Financing.StatementNotFound", root.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Statement_detail_route_is_advertised_in_openapi_with_200_and_404() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/financing/statements/{id}")
            .GetProperty("get");
        Assert.Equal("Financing", get.GetProperty("tags").EnumerateArray().Single().GetString());
        var responses = get.GetProperty("responses");
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("404", out _));
    }
}
