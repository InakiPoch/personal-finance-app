using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class SpentBankCashTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task A_debit_split_counts_its_full_amount_under_the_purchase_category_and_is_flagged_shared_with() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterDebitAsync(client, "Spent Bank 1", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Ines", cancellationToken);
        var response = await client.PostAsJsonAsync("/v1/ledger/expenses", new {
            amountMinorUnits = 100_00,
            sourceInstrumentId = bankId,
            categoryName = "Spent Dinner",
            purchaseDate = "2025-01-10",
            description = "Dinner with Ines",
            split = new[] { new { partyId, weight = 1 } }
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var spent = await SpentAsync(client, "2025-01", "Spent Dinner", cancellationToken);
        var row = Assert.Single(await MoneyFlowAsync(client, "2025-01", cancellationToken));
        Assert.Equal(100_00, spent);
        Assert.Equal(100_00, row.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("Outcome", row.GetProperty("kind").GetString());
        Assert.Equal("SharedWith", row.GetProperty("flag").GetString());
        Assert.Equal("Ines", row.GetProperty("partyName").GetString());
    }

    [Fact]
    public async Task A_loan_counts_under_lent_to_parties_and_is_flagged_lent_to() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterDebitAsync(client, "Spent Bank 2", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Tomas", cancellationToken);
        await PostLoanAsync(client, partyId, bankId, 30_00, "2025-02-05", cancellationToken);
        var spent = await SpentAsync(client, "2025-02", "Lent to parties", cancellationToken);
        var row = Assert.Single(await MoneyFlowAsync(client, "2025-02", cancellationToken));
        Assert.Equal(30_00, spent);
        Assert.Equal(30_00, row.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("LentTo", row.GetProperty("flag").GetString());
        Assert.Equal("Tomas", row.GetProperty("partyName").GetString());
    }

    [Fact]
    public async Task Undoing_a_loan_removes_it_from_spent_and_from_the_movements() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterDebitAsync(client, "Spent Bank 3", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Vera", cancellationToken);
        var loan = await PostLoanAsync(client, partyId, bankId, 45_00, "2025-03-05", cancellationToken);
        var row = Assert.Single(await MoneyFlowAsync(client, "2025-03", cancellationToken));
        Assert.Equal(loan, row.GetProperty("transactionId").GetGuid());
        var reverse = await client.PostAsync($"/v1/ledger/transactions/{loan}/reversal", null, cancellationToken);
        reverse.EnsureSuccessStatusCode();
        Assert.Equal(0, await SpentAsync(client, "2025-03", "Lent to parties", cancellationToken));
        Assert.Empty(await MoneyFlowAsync(client, "2025-03", cancellationToken));
    }

    [Fact]
    public async Task A_plain_expense_carries_no_flag() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterDebitAsync(client, "Spent Bank 4", cancellationToken);
        var response = await client.PostAsJsonAsync("/v1/ledger/expenses", new {
            amountMinorUnits = 12_00,
            sourceInstrumentId = bankId,
            categoryName = "Spent Coffee",
            purchaseDate = "2025-04-02",
            description = "Coffee"
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var row = Assert.Single(await MoneyFlowAsync(client, "2025-04", cancellationToken));
        Assert.Equal(JsonValueKind.Null, row.GetProperty("flag").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("partyName").ValueKind);
    }

    private static async Task<long> SpentAsync(HttpClient client, string month, string category, CancellationToken cancellationToken) {
        var monthly = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/monthly-expenses?month={month}", cancellationToken);
        return monthly.GetProperty("rows").EnumerateArray()
            .Where(row => row.GetProperty("category").GetString() == category)
        .Sum(row => row.GetProperty("amountMinorUnits").GetInt64());
    }

    private static async Task<List<JsonElement>> MoneyFlowAsync(HttpClient client, string month, CancellationToken cancellationToken) {
        var flow = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/money-flow?month={month}", cancellationToken);
        return flow.GetProperty("rows").EnumerateArray().ToList();
    }

    private static async Task<Guid> PostLoanAsync(HttpClient client, Guid partyId, Guid sourceAccountId, long amount, string lentOn, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync($"/v1/parties/{partyId}/loans", new {
            amountMinorUnits = amount,
            sourceAccountId,
            lentOn,
            description = "Loan",
            currencyCode = "ARS"
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("ledgerTransactionId").GetGuid();
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
}
