using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class CorsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private const string allowedOrigin = "http://localhost:4200";

    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Preflight_from_the_configured_origin_is_allowed() {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/ledger/transactions");
        request.Headers.Add("Origin", allowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal(allowedOrigin, Assert.Single(values));
    }

    [Fact]
    public async Task Preflight_from_an_unlisted_origin_gets_no_allow_origin_header() {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/ledger/transactions");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
