using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PartyPurchaseTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task A_debit_purchase_raises_what_i_owe_and_is_my_expense_without_touching_any_bank() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Purchase Bank 1", "Asset", "Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Marta", cancellationToken);
        var response = await PostPurchaseAsync(client, partyId, 12_000, "ARS", "Dinner", "Eating Out 1", "debit", null, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.NotEqual(Guid.Empty, body.GetProperty("purchaseId").GetGuid());
        Assert.Equal(12_000, await PartyBalanceAsync(client, partyId, "payableBalances", "ARS", cancellationToken));
        Assert.Equal(0, await PartyBalanceAsync(client, partyId, "balances", "ARS", cancellationToken));
        Assert.Equal(0, await AccountBalanceAsync(client, bankId, cancellationToken));
        var categories = await client.GetFromJsonAsync<JsonElement>("/v1/expense-categories", cancellationToken);
        Assert.Contains("Eating Out 1", categories.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task The_category_is_reused_case_insensitively() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Nico", cancellationToken);
        (await PostPurchaseAsync(client, partyId, 1_000, "ARS", "A", "Kiosk Snacks", "debit", null, cancellationToken)).EnsureSuccessStatusCode();
        (await PostPurchaseAsync(client, partyId, 1_000, "ARS", "B", "kiosk snacks", "debit", null, cancellationToken)).EnsureSuccessStatusCode();
        var categories = await client.GetFromJsonAsync<JsonElement>("/v1/expense-categories", cancellationToken);
        Assert.Single(categories.GetProperty("rows").EnumerateArray(), row => string.Equals(row.GetProperty("name").GetString(), "Kiosk Snacks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_purchase_shows_on_the_i_owe_timeline_with_its_purchase_id_and_not_on_owed_to_me() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Olga", cancellationToken);
        var created = await (await PostPurchaseAsync(client, partyId, 3_000, "ARS", "Cinema", "Fun", "debit", null, cancellationToken)).Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var owe = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/parties/{partyId}/timeline?side=payable", cancellationToken);
        var row = Assert.Single(owe.GetProperty("rows").EnumerateArray());
        Assert.Equal("Paid by Olga: Cinema", row.GetProperty("description").GetString());
        Assert.Equal(3_000, row.GetProperty("deltaMinorUnits").GetInt64());
        Assert.Equal(created.GetProperty("purchaseId").GetGuid(), row.GetProperty("purchaseId").GetGuid());
        var owed = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/parties/{partyId}/timeline", cancellationToken);
        Assert.Empty(owed.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task Undoing_a_purchase_reverses_it_and_a_second_undo_is_409() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Pablo", cancellationToken);
        var created = await (await PostPurchaseAsync(client, partyId, 8_000, "ARS", "Taxi", "Transport X", "debit", null, cancellationToken)).Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var purchaseId = created.GetProperty("purchaseId").GetGuid();
        var undo = await client.PostAsync($"/v1/parties/{partyId}/purchases/{purchaseId}/undo", null, cancellationToken);
        Assert.True(undo.IsSuccessStatusCode, await undo.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(0, await PartyBalanceAsync(client, partyId, "payableBalances", "ARS", cancellationToken));
        var owe = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/parties/{partyId}/timeline?side=payable", cancellationToken);
        var last = owe.GetProperty("rows").EnumerateArray().Last();
        Assert.Equal("Reversal", last.GetProperty("description").GetString());
        Assert.Equal(purchaseId, last.GetProperty("purchaseId").GetGuid());
        var again = await client.PostAsync($"/v1/parties/{partyId}/purchases/{purchaseId}/undo", null, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Undoing_an_unknown_purchase_or_one_of_another_party_is_404() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Quino", cancellationToken);
        var otherId = await CreatePartyAsync(client, "Rita", cancellationToken);
        var created = await (await PostPurchaseAsync(client, partyId, 500, "ARS", "Gum", "Kiosk Y", "debit", null, cancellationToken)).Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var purchaseId = created.GetProperty("purchaseId").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/v1/parties/{partyId}/purchases/{Guid.NewGuid()}/undo", null, cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/v1/parties/{otherId}/purchases/{purchaseId}/undo", null, cancellationToken)).StatusCode);
    }

    [Fact]
    public async Task An_unknown_party_is_404() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await PostPurchaseAsync(client, Guid.NewGuid(), 1_000, "ARS", "Gum", "Kiosk Z", "debit", null, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0, "ARS", "Dinner", "Food", "debit", 0)]
    [InlineData(1_000, "EUR", "Dinner", "Food", "debit", 0)]
    [InlineData(1_000, "ARS", "", "Food", "debit", 0)]
    [InlineData(1_000, "ARS", "two\nlines", "Food", "debit", 0)]
    [InlineData(1_000, "ARS", "LONG", "Food", "debit", 0)]
    [InlineData(1_000, "ARS", "Dinner", "", "debit", 0)]
    [InlineData(1_000, "ARS", "Dinner", "Food", "debit", 2)]
    [InlineData(1_000, "ARS", "Dinner", "Food", "weird", 0)]
    public async Task Invalid_purchases_are_rejected_with_422(long amount, string currency, string description, string category, string kind, int daysAhead) {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Sofia", cancellationToken);
        var text = description == "LONG" ? new string('x', 121) : description;
        var purchasedOn = DateTime.UtcNow.Date.AddDays(daysAhead).ToString("yyyy-MM-dd");
        var response = await PostPurchaseAsync(client, partyId, amount, currency, text, category, kind, purchasedOn, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PostPurchaseAsync(HttpClient client, Guid partyId, long share, string currency, string description, string category, string kind, string? purchasedOn, CancellationToken cancellationToken) {
        return client.PostAsJsonAsync($"/v1/parties/{partyId}/purchases", new {
            shareMinorUnits = share,
            currencyCode = currency,
            description,
            categoryName = category,
            purchaseDate = purchasedOn ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
            kind
        }, cancellationToken);
    }

    private static async Task<long> PartyBalanceAsync(HttpClient client, Guid partyId, string side, string currency, CancellationToken cancellationToken) {
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/parties/{partyId}/balance", cancellationToken);
        var rows = balance.GetProperty(side).EnumerateArray().Where(row => row.GetProperty("currencyCode").GetString() == currency).ToList();
        return rows.Count == 0 ? 0 : rows[0].GetProperty("balanceMinorUnits").GetInt64();
    }

    private static async Task<long> AccountBalanceAsync(HttpClient client, Guid accountId, CancellationToken cancellationToken) {
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/ledger/accounts/{accountId}/balance", cancellationToken);
        return balance.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("balanceMinorUnits").GetInt64()).FirstOrDefault();
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, string type, string kind, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/ledger/accounts", new { name, type, kind }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("accountId").GetGuid();
    }

    private static async Task<Guid> CreatePartyAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/parties", new { name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}
