using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class DueThisMonthTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Due_this_month_route_is_advertised_in_openapi_with_200() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/financing/due-this-month")
            .GetProperty("get");
        Assert.Equal("Financing", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }
}
