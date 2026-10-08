using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class SettlementsReceivedTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task A_settlement_counts_as_received_and_is_flagged_paid_back_by() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var (bankId, partyId) = await LoanedPartyAsync(client, "Recv Bank 1", "Rita", "2024-05-02", 50_00, cancellationToken);

        var settlement = await SettleAsync(client, partyId, bankId, 20_00, "2024-05-20", cancellationToken);

        Assert.Equal(20_00, await ReceivedAsync(client, "2024-05", cancellationToken));
        var row = Assert.Single(await MoneyFlowAsync(client, "2024-05", cancellationToken), r => r.GetProperty("transactionId").GetGuid() == settlement);
        Assert.Equal("Income", row.GetProperty("kind").GetString());
        Assert.Equal(20_00, row.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("PaidBackBy", row.GetProperty("flag").GetString());
        Assert.Equal("Rita", row.GetProperty("partyName").GetString());
    }

    [Fact]
    public async Task Undoing_a_settlement_removes_it_from_received_and_restores_the_balance() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var (bankId, partyId) = await LoanedPartyAsync(client, "Recv Bank 2", "Sol", "2024-06-02", 50_00, cancellationToken);
        var settlement = await SettleAsync(client, partyId, bankId, 20_00, "2024-06-20", cancellationToken);

        (await client.PostAsync($"/v1/ledger/transactions/{settlement}/reversal", null, cancellationToken)).EnsureSuccessStatusCode();

        Assert.Equal(0, await ReceivedAsync(client, "2024-06", cancellationToken));
        Assert.DoesNotContain(await MoneyFlowAsync(client, "2024-06", cancellationToken), r => r.GetProperty("transactionId").GetGuid() == settlement);
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/parties/{partyId}/balance", cancellationToken);
        Assert.Equal(50_00, balance.GetProperty("balances").EnumerateArray().Single().GetProperty("balanceMinorUnits").GetInt64());
    }

    [Fact]
    public async Task An_undone_loan_does_not_count_as_received() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterDebitAsync(client, "Recv Bank 3", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Teo", cancellationToken);
        var loan = await PostLoanAsync(client, partyId, bankId, 30_00, "2024-07-02", cancellationToken);

        (await client.PostAsync($"/v1/ledger/transactions/{loan}/reversal", null, cancellationToken)).EnsureSuccessStatusCode();

        Assert.Equal(0, await ReceivedAsync(client, "2024-07", cancellationToken));
        Assert.Empty(await MoneyFlowAsync(client, "2024-07", cancellationToken));
    }

    private static async Task<(Guid BankId, Guid PartyId)> LoanedPartyAsync(HttpClient client, string bank, string party, string lentOn, long amount, CancellationToken cancellationToken) {
        var bankId = await RegisterDebitAsync(client, bank, cancellationToken);
        var partyId = await CreatePartyAsync(client, party, cancellationToken);
        await PostLoanAsync(client, partyId, bankId, amount, lentOn, cancellationToken);
        return (bankId, partyId);
    }

    private static async Task<Guid> SettleAsync(HttpClient client, Guid partyId, Guid bankId, long amount, string settledOn, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync($"/v1/parties/{partyId}/settlements", new {
            amountMinorUnits = amount,
            bankAccountId = bankId,
            settledOnUtc = DateTimeOffset.Parse(settledOn + "T12:00:00Z"),
            currencyCode = "ARS"
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("ledgerTransactionId").GetGuid();
    }

    private static async Task<long> ReceivedAsync(HttpClient client, string month, CancellationToken cancellationToken) {
        var monthly = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/monthly-incomes?month={month}", cancellationToken);
        return monthly.GetProperty("rows").EnumerateArray().Sum(row => row.GetProperty("amountMinorUnits").GetInt64());
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
