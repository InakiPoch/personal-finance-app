using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PartiesListTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task A_created_party_is_returned_by_the_list_with_its_id_and_name() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/v1/parties", new { name = "Wanda" }, cancellationToken);
        created.EnsureSuccessStatusCode();
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var id = createdBody.GetProperty("id").GetGuid();

        var response = await client.GetAsync("/v1/parties", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var rows = document.RootElement.GetProperty("rows").EnumerateArray().ToList();
        var row = rows.Single(candidate => candidate.GetProperty("id").GetGuid() == id);
        Assert.Equal("Wanda", row.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Parties_list_route_is_advertised_in_openapi_under_the_parties_tag_with_200() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/parties")
            .GetProperty("get");
        Assert.Equal("Parties", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }
}
