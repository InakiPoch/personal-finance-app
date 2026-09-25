using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class DebitExpenseTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Records_an_unsplit_debit_expense_and_categorizes_it_in_monthly_expenses() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var checkingId = await RegisterDebitAsync(client, "Everyday checking", cancellationToken);
        var response = await client.PostAsJsonAsync("/v1/ledger/expenses", new {
            amountMinorUnits = 250_00,
            sourceInstrumentId = checkingId,
            categoryName = "Groceries",
            purchaseDate = "2026-03-10",
            description = "Weekly shop"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.NotEqual(Guid.Empty, created.GetProperty("id").GetGuid());
        var categories = await client.GetFromJsonAsync<JsonElement>("/v1/expense-categories", cancellationToken);
        Assert.Contains("Groceries", categories.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("name").GetString()));
        var monthly = await client.GetFromJsonAsync<JsonElement>("/v1/reports/monthly-expenses", cancellationToken);
        var groceries = monthly.GetProperty("rows").EnumerateArray()
            .Single(row => row.GetProperty("category").GetString() == "Groceries");
        Assert.Equal("2026-03", groceries.GetProperty("month").GetString());
        Assert.Equal(250_00, groceries.GetProperty("amountMinorUnits").GetInt64());
    }

    [Fact]
    public async Task Reuses_a_category_case_insensitively_across_expenses() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var checkingId = await RegisterDebitAsync(client, "Case-fold checking", cancellationToken);
        await PostExpenseAsync(client, checkingId, "Transport", "2026-04-01", 10_00, cancellationToken);
        await PostExpenseAsync(client, checkingId, "  transport  ", "2026-04-05", 20_00, cancellationToken);
        var categories = await client.GetFromJsonAsync<JsonElement>("/v1/expense-categories", cancellationToken);
        var transportRows = categories.GetProperty("rows").EnumerateArray()
            .Count(row => string.Equals(row.GetProperty("name").GetString(), "Transport", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, transportRows);
    }

    [Fact]
    public async Task Splitting_a_debit_expense_posts_a_receivable_to_the_party() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var checkingId = await RegisterDebitAsync(client, "Split checking", cancellationToken);
        var aliceId = await CreatePartyAsync(client, "Alice", cancellationToken);
        var response = await client.PostAsJsonAsync("/v1/ledger/expenses", new {
            amountMinorUnits = 300_00,
            sourceInstrumentId = checkingId,
            categoryName = "Dining",
            purchaseDate = "2026-05-02",
            description = "Dinner with Alice",
            split = new[] { new { partyId = aliceId, weight = 1 } }
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/parties/{aliceId}/balance", cancellationToken);
        var arsBalance = balance.GetProperty("balances").EnumerateArray()
            .Single(row => row.GetProperty("currencyCode").GetString() == "ARS");
        Assert.Equal(150_00, arsBalance.GetProperty("balanceMinorUnits").GetInt64());
        var monthly = await client.GetFromJsonAsync<JsonElement>("/v1/reports/monthly-expenses", cancellationToken);
        var dining = monthly.GetProperty("rows").EnumerateArray()
            .Single(row => row.GetProperty("category").GetString() == "Dining");
        Assert.Equal(150_00, dining.GetProperty("amountMinorUnits").GetInt64());
    }

    [Fact]
    public async Task Rejects_an_unknown_source_instrument_with_404() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/ledger/expenses", new {
            amountMinorUnits = 100_00,
            sourceInstrumentId = Guid.NewGuid(),
            categoryName = "Groceries",
            purchaseDate = "2026-03-10",
            description = "Shop"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("Ledger.AccountNotFound", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Debit_expenses_route_is_advertised_in_openapi_under_the_ledger_tag_with_201() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var post = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/ledger/expenses")
            .GetProperty("post");
        Assert.Equal("Ledger", post.GetProperty("tags").EnumerateArray().Single().GetString());
        Assert.True(post.GetProperty("responses").TryGetProperty("201", out _));
    }

    private static async Task<Guid> RegisterDebitAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/instruments", new { type = "debit", name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreatePartyAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/parties", new { name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task PostExpenseAsync(HttpClient client, Guid sourceInstrumentId, string categoryName, string purchaseDate, long amountMinorUnits, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/ledger/expenses", new {
            amountMinorUnits,
            sourceInstrumentId,
            categoryName,
            purchaseDate,
            description = "expense"
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
