using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class CreditorInstallmentPaymentTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Paying_an_installment_moves_the_bank_and_shows_in_money_movements_and_spent() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var bankId = await RegisterDebitAsync(client, "Creditor Pay Bank 1", cancellationToken);
        var spentBefore = await SpentAsync(client, month, "Creditor payments", cancellationToken);
        var (creditorId, installmentId) = await SeedCreditorPlanAsync(client, "Cecilia", "Sofa", 3, 300_00, cancellationToken);
        var balanceBefore = await BalanceAsync(client, bankId, cancellationToken);
        var response = await client.PostAsJsonAsync($"/v1/financing/creditor-installments/{installmentId}/pay", new { bankAccountId = bankId, amountMinorUnits = 40_00 }, cancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal(balanceBefore - 40_00, await BalanceAsync(client, bankId, cancellationToken));
        Assert.Equal(spentBefore + 40_00, await SpentAsync(client, month, "Creditor payments", cancellationToken));
        var row = Assert.Single(await MoneyFlowAsync(client, month, cancellationToken), r => r.GetProperty("description").GetString() == "Installment 1/3 · Sofa · Cecilia");
        Assert.Equal("Outcome", row.GetProperty("kind").GetString());
        Assert.Equal(40_00, row.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("Creditor Pay Bank 1", row.GetProperty("accountName").GetString());
        Assert.NotEqual(Guid.Empty, creditorId);
    }

    [Fact]
    public async Task Undoing_the_payment_restores_the_bank_and_removes_the_movement() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var bankId = await RegisterDebitAsync(client, "Creditor Pay Bank 2", cancellationToken);
        var (_, installmentId) = await SeedCreditorPlanAsync(client, "Dario", "Fridge", 2, 200_00, cancellationToken);
        var balanceBefore = await BalanceAsync(client, bankId, cancellationToken);
        (await client.PostAsJsonAsync($"/v1/financing/creditor-installments/{installmentId}/pay", new { bankAccountId = bankId }, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(balanceBefore - 100_00, await BalanceAsync(client, bankId, cancellationToken));
        (await client.PostAsync($"/v1/financing/creditor-installments/{installmentId}/unpay", null, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(balanceBefore, await BalanceAsync(client, bankId, cancellationToken));
        Assert.DoesNotContain(await MoneyFlowAsync(client, month, cancellationToken), r => r.GetProperty("description").GetString() == "Installment 1/2 · Fridge · Dario");
    }

    [Fact]
    public async Task A_missing_or_unknown_account_is_rejected_with_422_and_nothing_is_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var (_, installmentId) = await SeedCreditorPlanAsync(client, "Elena", "Desk", 2, 200_00, cancellationToken);
        var attempts = new List<object> {
            new { amountMinorUnits = 10_00 },
            new { bankAccountId = Guid.NewGuid(), amountMinorUnits = 10_00 }
        };
        foreach(var attempt in attempts) {
            var response = await client.PostAsJsonAsync($"/v1/financing/creditor-installments/{installmentId}/pay", attempt, cancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            Assert.Equal("Financing.InvalidPaidFromAccount", document.RootElement.GetProperty("code").GetString());
        }
    }

    private static async Task<(Guid CreditorId, Guid FirstInstallmentId)> SeedCreditorPlanAsync(HttpClient client, string creditorName, string description, int installments, long total, CancellationToken cancellationToken) {
        var created = await client.PostAsJsonAsync("/v1/creditors", new { name = creditorName, accounts = new[] { new { label = $"{creditorName} Bank" } } }, cancellationToken);
        created.EnsureSuccessStatusCode();
        var creditorId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("creditorId").GetGuid();
        var list = await client.GetFromJsonAsync<JsonElement>("/v1/creditors", cancellationToken);
        var creditor = list.GetProperty("rows").EnumerateArray().Single(row => row.GetProperty("id").GetGuid() == creditorId);
        var accountId = creditor.GetProperty("accounts")[0].GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync("/v1/financing/payment-plans", new {
            amountMinorUnits = total,
            installmentCount = installments,
            purchaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            description,
            creditorId,
            creditorAccountId = accountId,
            currencyCode = "ARS"
        }, cancellationToken)).EnsureSuccessStatusCode();
        var detail = await client.GetFromJsonAsync<JsonElement>($"/v1/financing/creditor-payables/{creditorId}", cancellationToken);
        var first = detail.GetProperty("purchases")[0].GetProperty("installments").EnumerateArray().Single(row => row.GetProperty("sequence").GetInt32() == 1);
        return (creditorId, first.GetProperty("installmentId").GetGuid());
    }

    private static async Task<long> BalanceAsync(HttpClient client, Guid accountId, CancellationToken cancellationToken) {
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/ledger/accounts/{accountId}/balance", cancellationToken);
        return balance.GetProperty("rows").EnumerateArray().Where(row => row.GetProperty("currencyCode").GetString() == "ARS")
            .Select(row => row.GetProperty("balanceMinorUnits").GetInt64())
        .FirstOrDefault();
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

    private static async Task<Guid> RegisterDebitAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/instruments", new { type = "debit", name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}
