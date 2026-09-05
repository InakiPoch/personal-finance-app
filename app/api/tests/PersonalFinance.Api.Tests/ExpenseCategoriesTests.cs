using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class ExpenseCategoriesTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Expense_categories_list_is_empty_on_a_fresh_database() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var body = await client.GetFromJsonAsync<JsonElement>("/v1/expense-categories", cancellationToken);
        Assert.Empty(body.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task Expense_categories_route_is_advertised_in_openapi_with_200() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var get = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/expense-categories")
            .GetProperty("get");
        Assert.Equal("Expense categories", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }
}
