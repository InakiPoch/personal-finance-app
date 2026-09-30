using System.Text.Json;
using PersonalFinance.Api.Endpoints;
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

    [Fact]
    public async Task Due_this_month_route_documents_an_optional_month_query_parameter() {
        var get = await GetOperationAsync("/v1/financing/due-this-month");
        var month = QueryParameter(get, "month");
        Assert.False(month.TryGetProperty("required", out var required) && required.GetBoolean());
    }

    [Fact]
    public async Task Subscriptions_by_month_route_is_advertised_with_a_required_month_parameter() {
        var get = await GetOperationAsync("/v1/subscriptions/by-month");
        Assert.Equal("Subscriptions", get.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(QueryParameter(get, "month").GetProperty("required").GetBoolean());
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));
    }

    [Theory]
    [InlineData("/v1/financing/due-this-month")]
    [InlineData("/v1/subscriptions/by-month")]
    public async Task Month_routes_document_a_400_response(string path) {
        var get = await GetOperationAsync(path);
        Assert.True(get.GetProperty("responses").TryGetProperty("400", out _));
    }

    [Fact]
    public void Month_parser_accepts_yyyy_MM_and_rejects_anything_else() {
        Assert.Equal(new DateOnly(2026, 8, 1), MonthQueryHelper.Parse("2026-08"));
        Assert.Throws<FormatException>(() => MonthQueryHelper.Parse("2026-13"));
        Assert.Throws<FormatException>(() => MonthQueryHelper.Parse("2026-8"));
        Assert.Throws<FormatException>(() => MonthQueryHelper.Parse("2026-08-01"));
        Assert.Throws<FormatException>(() => MonthQueryHelper.Parse("abc"));
    }

    private async Task<JsonElement> GetOperationAsync(string path) {
        var cancellationToken = TestContext.Current.CancellationToken;
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json", cancellationToken);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get").Clone();
    }

    private static JsonElement QueryParameter(JsonElement operation, string name) {
        return operation.GetProperty("parameters").EnumerateArray().Single(parameter => parameter.GetProperty("name").GetString() == name && parameter.GetProperty("in").GetString() == "query");
    }
}
