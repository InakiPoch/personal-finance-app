using System.Net;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class HealthEndpointTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Health_reports_healthy_and_includes_the_outbox_check() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        var checkNames = root.GetProperty("checks")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("outbox", checkNames);
    }
}
