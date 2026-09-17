using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PaySubscriptionTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Unknown_subscription_id_is_404_with_the_domain_code() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/v1/subscriptions/{Guid.NewGuid()}/pay", new { }, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("Subscriptions.SubscriptionNotFound", root.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Pay_route_is_advertised_in_openapi_with_200_404_and_409() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var post = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/subscriptions/{id}/pay")
            .GetProperty("post");
        Assert.Equal("Subscriptions", post.GetProperty("tags").EnumerateArray().Single().GetString());
        var responses = post.GetProperty("responses");
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("404", out _));
        Assert.True(responses.TryGetProperty("409", out _));
    }
}
