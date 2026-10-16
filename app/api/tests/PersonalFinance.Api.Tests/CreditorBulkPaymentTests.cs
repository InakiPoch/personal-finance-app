using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class CreditorBulkPaymentTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Paying_an_expense_across_installments_posts_one_movement_for_the_total() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var bankId = await RegisterDebitAsync(client, "Bulk Pay Bank 1", cancellationToken);
        var (_, planId, firstInstallmentId) = await SeedCreditorPlanAsync(client, "Fabio", "Oven", 3, 300_00, cancellationToken);
        var balanceBefore = await BalanceAsync(client, bankId, cancellationToken);
        var response = await client.PostAsJsonAsync($"/v1/financing/creditor-purchases/{planId}/pay", new { bankAccountId = bankId, amountMinorUnits = 250_00 }, cancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal(balanceBefore - 250_00, await BalanceAsync(client, bankId, cancellationToken));
        var row = Assert.Single(await MoneyFlowAsync(client, month, cancellationToken), r => r.GetProperty("description").GetString() == "Oven · Fabio");
        Assert.Equal(250_00, row.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("Bulk Pay Bank 1", row.GetProperty("accountName").GetString());
        (await client.PostAsync($"/v1/financing/creditor-installments/{firstInstallmentId}/unpay", null, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(balanceBefore, await BalanceAsync(client, bankId, cancellationToken));
        Assert.DoesNotContain(await MoneyFlowAsync(client, month, cancellationToken), r => r.GetProperty("description").GetString() == "Oven · Fabio");
    }

    [Fact]
    public async Task Undoing_one_installment_of_an_action_undoes_every_installment_it_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var bankId = await RegisterDebitAsync(client, "Bulk Pay Bank 2", cancellationToken);
        var (creditorId, planId, firstInstallmentId) = await SeedCreditorPlanAsync(client, "Gaby", "Bike", 2, 200_00, cancellationToken);
        var balanceBefore = await BalanceAsync(client, bankId, cancellationToken);
        (await client.PostAsJsonAsync($"/v1/financing/creditor-purchases/{planId}/pay", new { bankAccountId = bankId }, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(balanceBefore - 200_00, await BalanceAsync(client, bankId, cancellationToken));
        (await client.PostAsync($"/v1/financing/creditor-installments/{firstInstallmentId}/unpay", null, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(balanceBefore, await BalanceAsync(client, bankId, cancellationToken));
        Assert.DoesNotContain(await MoneyFlowAsync(client, month, cancellationToken), r => r.GetProperty("description").GetString() == "Bike · Gaby");
        var detail = await client.GetFromJsonAsync<JsonElement>($"/v1/financing/creditor-payables/{creditorId}", cancellationToken);
        var installments = detail.GetProperty("purchases")[0].GetProperty("installments").EnumerateArray().ToList();
        Assert.All(installments, row => {
            Assert.False(row.GetProperty("isPaid").GetBoolean());
            Assert.False(row.GetProperty("hasPayments").GetBoolean());
            Assert.Equal(100_00, row.GetProperty("remainingMinorUnits").GetInt64());
        });
    }

    [Fact]
    public async Task Paying_the_full_debt_posts_one_movement_and_requires_a_paid_from_account() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var bankId = await RegisterDebitAsync(client, "Bulk Pay Bank 3", cancellationToken);
        var (creditorId, _, _) = await SeedCreditorPlanAsync(client, "Hugo", "Couch", 3, 300_00, cancellationToken);
        var balanceBefore = await BalanceAsync(client, bankId, cancellationToken);
        var rejected = await client.PostAsJsonAsync($"/v1/financing/creditor-payables/{creditorId}/pay-full", new { }, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Equal(balanceBefore, await BalanceAsync(client, bankId, cancellationToken));
        (await client.PostAsJsonAsync($"/v1/financing/creditor-payables/{creditorId}/pay-full", new { bankAccountId = bankId }, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(balanceBefore - 300_00, await BalanceAsync(client, bankId, cancellationToken));
        var row = Assert.Single(await MoneyFlowAsync(client, month, cancellationToken), r => r.GetProperty("description").GetString() == "Full debt · Hugo");
        Assert.Equal(300_00, row.GetProperty("amountMinorUnits").GetInt64());
    }

    private static async Task<Guid> CreditorIdAsync(HttpClient client, Guid planId, CancellationToken cancellationToken) {
        var payables = await client.GetFromJsonAsync<JsonElement>("/v1/financing/creditor-payables", cancellationToken);
        foreach(var payable in payables.GetProperty("rows").EnumerateArray()) {
            var id = payable.GetProperty("creditorId").GetGuid();
            var detail = await client.GetFromJsonAsync<JsonElement>($"/v1/financing/creditor-payables/{id}", cancellationToken);
            if(detail.GetProperty("purchases").EnumerateArray().Any(purchase => purchase.GetProperty("planId").GetGuid() == planId)) {
                return id;
            }
        }
        throw new InvalidOperationException("Creditor not found.");
    }

    private static async Task<(Guid CreditorId, Guid PlanId, Guid FirstInstallmentId)> SeedCreditorPlanAsync(HttpClient client, string creditorName, string description, int installments, long total, CancellationToken cancellationToken) {
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
        return (creditorId, detail.GetProperty("purchases")[0].GetProperty("planId").GetGuid(), first.GetProperty("installmentId").GetGuid());
    }

    private static async Task<long> BalanceAsync(HttpClient client, Guid accountId, CancellationToken cancellationToken) {
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/ledger/accounts/{accountId}/balance", cancellationToken);
        return balance.GetProperty("rows").EnumerateArray().Where(row => row.GetProperty("currencyCode").GetString() == "ARS")
            .Select(row => row.GetProperty("balanceMinorUnits").GetInt64())
        .FirstOrDefault();
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
