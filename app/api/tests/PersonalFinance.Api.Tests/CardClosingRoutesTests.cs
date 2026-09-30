using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class CardClosingRoutesTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Closing_routes_return_404_for_an_unknown_card() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var baseUrl = $"/v1/instruments/cards/{Guid.NewGuid()}";
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{baseUrl}/closing-day", new { day = 20 }, cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{baseUrl}/closing-dates", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{baseUrl}/closing-dates/2026/10", new { day = 20 }, cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{baseUrl}/closing-dates/2026/10", cancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Closing_routes_cover_200_204_and_422_for_a_real_card() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/v1/instruments", new { type = "credit", name = "Closing Visa", cutoffDate = 15 }, cancellationToken);
        created.EnsureSuccessStatusCode();
        using var createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync(cancellationToken));
        var baseUrl = $"/v1/instruments/cards/{createdDocument.RootElement.GetProperty("id").GetGuid()}";
        var next = DateTime.UtcNow.AddMonths(2);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"{baseUrl}/closing-day", new { day = 20 }, cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PutAsJsonAsync($"{baseUrl}/closing-day", new { day = 40 }, cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"{baseUrl}/closing-dates/{next.Year}/{next.Month}", new { day = 10 }, cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PutAsJsonAsync($"{baseUrl}/closing-dates/{next.Year}/{next.Month}", new { day = 40 }, cancellationToken)).StatusCode);
        var schedule = await client.GetAsync($"{baseUrl}/closing-dates", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
        using var document = JsonDocument.Parse(await schedule.Content.ReadAsStringAsync(cancellationToken));
        var overridden = document.RootElement.GetProperty("rows").EnumerateArray().Single(row => row.GetProperty("year").GetInt32() == next.Year && row.GetProperty("month").GetInt32() == next.Month);
        Assert.True(overridden.GetProperty("isOverride").GetBoolean());
        Assert.Equal(10, DateOnly.Parse(overridden.GetProperty("closingDate").GetString()!).Day);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{baseUrl}/closing-dates/{next.Year}/{next.Month}", cancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Instruments_list_exposes_the_next_closing_date_of_a_credit_card() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/v1/instruments", new { type = "credit", name = "Next Closing Visa", cutoffDate = 15 }, cancellationToken)).EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/v1/instruments", cancellationToken));
        var row = document.RootElement.GetProperty("rows").EnumerateArray().Single(candidate => candidate.GetProperty("name").GetString() == "Next Closing Visa");
        Assert.Equal(15, DateOnly.Parse(row.GetProperty("nextClosingDate").GetString()!).Day);
    }
}
