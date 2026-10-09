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

    [Fact]
    public async Task A_party_with_nothing_in_either_direction_is_settled_up() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var id = await CreatePartyAsync(client, "List Settled", cancellationToken);
        var row = await RowAsync(client, id, cancellationToken);
        Assert.True(row.GetProperty("settledUp").GetBoolean());
        Assert.Empty(row.GetProperty("owedToYou").EnumerateArray());
        Assert.Empty(row.GetProperty("youOwe").EnumerateArray());
    }

    [Fact]
    public async Task Both_sides_are_listed_per_currency_and_the_party_is_not_settled_up() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "List Bank 1", cancellationToken);
        var id = await CreatePartyAsync(client, "List Both", cancellationToken);
        (await client.PostAsJsonAsync($"/v1/parties/{id}/loans", new { amountMinorUnits = 3_000, sourceAccountId = bankId, lentOn = Today(), description = "Lunch", currencyCode = "ARS" }, cancellationToken)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/v1/parties/{id}/borrowings", new { amountMinorUnits = 800, destinationAccountId = bankId, borrowedOn = Today(), description = "Gas", currencyCode = "USD" }, cancellationToken)).EnsureSuccessStatusCode();
        var row = await RowAsync(client, id, cancellationToken);
        Assert.False(row.GetProperty("settledUp").GetBoolean());
        var owed = Assert.Single(row.GetProperty("owedToYou").EnumerateArray());
        Assert.Equal("ARS", owed.GetProperty("currencyCode").GetString());
        Assert.Equal(3_000, owed.GetProperty("balanceMinorUnits").GetInt64());
        var owe = Assert.Single(row.GetProperty("youOwe").EnumerateArray());
        Assert.Equal("USD", owe.GetProperty("currencyCode").GetString());
        Assert.Equal(800, owe.GetProperty("balanceMinorUnits").GetInt64());
    }

    [Fact]
    public async Task A_party_with_only_scheduled_installments_i_owe_is_never_settled_up() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var id = await CreatePartyAsync(client, "List Scheduled", cancellationToken);
        var nextMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1);
        (await client.PostAsJsonAsync($"/v1/parties/{id}/purchases", new {
            shareMinorUnits = 1_000, currencyCode = "ARS", description = "Fridge", categoryName = "Home",
            purchaseDate = Today(), kind = "credit", installmentCount = 2, firstPaymentMonth = nextMonth.ToString("yyyy-MM-dd")
        }, cancellationToken)).EnsureSuccessStatusCode();
        var row = await RowAsync(client, id, cancellationToken);
        Assert.False(row.GetProperty("settledUp").GetBoolean());
        Assert.Equal(2, row.GetProperty("scheduledYouOweCount").GetInt32());
        Assert.Empty(row.GetProperty("youOwe").EnumerateArray());
    }

    private static string Today() => DateTime.UtcNow.ToString("yyyy-MM-dd");

    private static async Task<JsonElement> RowAsync(HttpClient client, Guid id, CancellationToken cancellationToken) {
        var body = await client.GetFromJsonAsync<JsonElement>("/v1/parties", cancellationToken);
        return body.GetProperty("rows").EnumerateArray().Single(row => row.GetProperty("id").GetGuid() == id);
    }

    private static async Task<Guid> CreatePartyAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/parties", new { name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/ledger/accounts", new { name, type = "Asset", kind = "Bank" }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("accountId").GetGuid();
    }
}
