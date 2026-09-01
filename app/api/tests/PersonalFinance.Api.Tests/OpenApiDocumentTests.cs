using System.Net;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class OpenApiDocumentTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task OpenApi_document_is_served_with_title_and_a_non_empty_path_set() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        Assert.Equal("PersonalFinance API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.NotEmpty(root.GetProperty("paths").EnumerateObject());
    }
}
