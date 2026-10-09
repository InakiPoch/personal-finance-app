using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PartyDebtsMoneyFlowTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task A_borrowing_counts_as_received_and_is_flagged_borrowed_from_with_the_full_amount() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterDebitAsync(client, "Debts Bank 1", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Lucio", cancellationToken);
        var borrowing = await BorrowAsync(client, partyId, bankId, 40_00, "2023-03-10", cancellationToken);
        Assert.Equal(40_00, await ReceivedAsync(client, "2023-03", cancellationToken));
        var row = Assert.Single(await MoneyFlowAsync(client, "2023-03", cancellationToken), r => r.GetProperty("transactionId").GetGuid() == borrowing);
        Assert.Equal("Income", row.GetProperty("kind").GetString());
        Assert.Equal(40_00, row.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("BorrowedFrom", row.GetProperty("flag").GetString());
        Assert.Equal("Lucio", row.GetProperty("partyName").GetString());
        Assert.Equal("Debts Bank 1", row.GetProperty("accountName").GetString());
        Assert.Equal(0, await SpentAsync(client, "2023-03", cancellationToken));
    }

    [Fact]
    public async Task A_repayment_counts_as_out_of_pocket_and_is_flagged_paid_back_to() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterDebitAsync(client, "Debts Bank 2", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Mara", cancellationToken);
        await BorrowAsync(client, partyId, bankId, 50_00, "2023-04-02", cancellationToken);
        var repayment = await RepayAsync(client, partyId, bankId, 20_00, "2023-04-20", cancellationToken);
        Assert.Equal(20_00, await SpentAsync(client, "2023-04", cancellationToken));
        var row = Assert.Single(await MoneyFlowAsync(client, "2023-04", cancellationToken), r => r.GetProperty("transactionId").GetGuid() == repayment);
        Assert.Equal("Outcome", row.GetProperty("kind").GetString());
        Assert.Equal(20_00, row.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("PaidBackTo", row.GetProperty("flag").GetString());
        Assert.Equal("Mara", row.GetProperty("partyName").GetString());
        Assert.Equal("Debts Bank 2", row.GetProperty("accountName").GetString());
        Assert.Equal(50_00, await ReceivedAsync(client, "2023-04", cancellationToken));
    }

    [Fact]
    public async Task Undoing_a_borrowing_or_a_repayment_removes_it_from_every_report() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterDebitAsync(client, "Debts Bank 3", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Nora", cancellationToken);
        var borrowing = await BorrowAsync(client, partyId, bankId, 30_00, "2023-05-03", cancellationToken);
        var repayment = await RepayAsync(client, partyId, bankId, 10_00, "2023-05-04", cancellationToken);
        (await client.PostAsync($"/v1/ledger/transactions/{repayment}/reversal", null, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(0, await SpentAsync(client, "2023-05", cancellationToken));
        Assert.DoesNotContain(await MoneyFlowAsync(client, "2023-05", cancellationToken), r => r.GetProperty("transactionId").GetGuid() == repayment);
        (await client.PostAsync($"/v1/ledger/transactions/{borrowing}/reversal", null, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(0, await ReceivedAsync(client, "2023-05", cancellationToken));
        Assert.Empty(await MoneyFlowAsync(client, "2023-05", cancellationToken));
    }

    [Fact]
    public async Task A_party_purchase_never_shows_as_received_or_out_of_pocket_or_in_money_movements() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Pilar", cancellationToken);
        var response = await client.PostAsJsonAsync($"/v1/parties/{partyId}/purchases", new {
            shareMinorUnits = 12_00,
            currencyCode = "ARS",
            description = "Dinner",
            categoryName = "Debts Dining",
            purchaseDate = "2023-06-15",
            kind = "debit"
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal(0, await ReceivedAsync(client, "2023-06", cancellationToken));
        Assert.Equal(0, await SpentAsync(client, "2023-06", cancellationToken));
        Assert.Empty(await MoneyFlowAsync(client, "2023-06", cancellationToken));
    }

    [Fact]
    public async Task The_transaction_feed_explains_borrowings_repayments_and_party_purchases() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterDebitAsync(client, "Debts Bank 4", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Quique", cancellationToken);
        await BorrowAsync(client, partyId, bankId, 30_00, "2023-07-03", cancellationToken);
        await RepayAsync(client, partyId, bankId, 10_00, "2023-07-04", cancellationToken);
        (await client.PostAsJsonAsync($"/v1/parties/{partyId}/purchases", new {
            shareMinorUnits = 5_00,
            currencyCode = "ARS",
            description = "Taxi",
            categoryName = "Debts Transport",
            purchaseDate = "2023-07-05",
            kind = "debit"
        }, cancellationToken)).EnsureSuccessStatusCode();
        var feed = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/transactions?accountId={bankId}&from=2023-07-01&to=2023-07-31", cancellationToken);
        var kinds = feed.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("kind").GetString()).ToList();
        Assert.Contains("Borrowing", kinds);
        Assert.Contains("Repayment", kinds);
        var all = await client.GetFromJsonAsync<JsonElement>("/v1/reports/transactions?from=2023-07-01&to=2023-07-31", cancellationToken);
        Assert.Contains("Party purchase", all.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("kind").GetString()));
    }

    private static async Task<long> ReceivedAsync(HttpClient client, string month, CancellationToken cancellationToken) {
        var monthly = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/monthly-incomes?month={month}", cancellationToken);
        return monthly.GetProperty("rows").EnumerateArray().Sum(row => row.GetProperty("amountMinorUnits").GetInt64());
    }

    private static async Task<long> SpentAsync(HttpClient client, string month, CancellationToken cancellationToken) {
        var monthly = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/monthly-expenses?month={month}", cancellationToken);
        return monthly.GetProperty("rows").EnumerateArray().Sum(row => row.GetProperty("amountMinorUnits").GetInt64());
    }

    private static async Task<List<JsonElement>> MoneyFlowAsync(HttpClient client, string month, CancellationToken cancellationToken) {
        var flow = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/money-flow?month={month}", cancellationToken);
        return flow.GetProperty("rows").EnumerateArray().ToList();
    }

    private static async Task<Guid> BorrowAsync(HttpClient client, Guid partyId, Guid bankId, long amount, string borrowedOn, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync($"/v1/parties/{partyId}/borrowings", new {
            amountMinorUnits = amount,
            destinationAccountId = bankId,
            borrowedOn,
            description = "Borrowed",
            currencyCode = "ARS"
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("ledgerTransactionId").GetGuid();
    }

    private static async Task<Guid> RepayAsync(HttpClient client, Guid partyId, Guid bankId, long amount, string paidOn, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync($"/v1/parties/{partyId}/repayments", new {
            amountMinorUnits = amount,
            sourceAccountId = bankId,
            paidOn,
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
