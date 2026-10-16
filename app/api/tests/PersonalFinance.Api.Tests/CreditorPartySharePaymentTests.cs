using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalFinance.Infrastructure.Outbox;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class CreditorPartySharePaymentTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task A_party_share_nets_the_bank_to_zero_and_shows_income_and_outcome_until_undone() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var bankId = await RegisterDebitAsync(client, "Party Share Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Pia", cancellationToken);
        var installmentId = await SeedSplitPlanAsync(client, partyId, "Pia", cancellationToken);
        await DrainOutboxAsync();
        await AccrueAsync();
        var balanceBefore = await BalanceAsync(client, bankId, cancellationToken);
        var spentBefore = await TotalAsync(client, "monthly-expenses", month, cancellationToken);
        var receivedBefore = await TotalAsync(client, "monthly-incomes", month, cancellationToken);
        var paid = await client.PostAsJsonAsync($"/v1/financing/creditor-installments/{installmentId}/pay-party", new { partyId, bankAccountId = bankId }, cancellationToken);
        Assert.True(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(balanceBefore, await BalanceAsync(client, bankId, cancellationToken));
        Assert.Equal(spentBefore + 50_00, await TotalAsync(client, "monthly-expenses", month, cancellationToken));
        Assert.Equal(receivedBefore + 50_00, await TotalAsync(client, "monthly-incomes", month, cancellationToken));
        var rows = await MoneyFlowAsync(client, month, cancellationToken);
        var income = Assert.Single(rows, r => r.GetProperty("description").GetString() == "Settlement from Pia");
        Assert.Equal("Income", income.GetProperty("kind").GetString());
        Assert.Equal(50_00, income.GetProperty("amountMinorUnits").GetInt64());
        var outcome = Assert.Single(rows, r => r.GetProperty("description").GetString() == "Installment 1/1 · Shared sofa · Pia Creditor");
        Assert.Equal("Outcome", outcome.GetProperty("kind").GetString());
        Assert.Equal(50_00, outcome.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("Party Share Bank", outcome.GetProperty("accountName").GetString());
        (await client.PostAsync($"/v1/financing/creditor-installments/{installmentId}/unpay", null, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(balanceBefore, await BalanceAsync(client, bankId, cancellationToken));
        rows = await MoneyFlowAsync(client, month, cancellationToken);
        Assert.DoesNotContain(rows, r => r.GetProperty("description").GetString() == "Settlement from Pia");
        Assert.DoesNotContain(rows, r => r.GetProperty("description").GetString() == "Installment 1/1 · Shared sofa · Pia Creditor");
    }

    [Fact]
    public async Task Paying_a_split_installment_in_full_is_one_outcome_and_the_party_still_owes_me() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var bankId = await RegisterDebitAsync(client, "Full Split Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Quino", cancellationToken);
        var installmentId = await SeedSplitPlanAsync(client, partyId, "Quino", cancellationToken);
        await DrainOutboxAsync();
        var balanceBefore = await BalanceAsync(client, bankId, cancellationToken);
        var spentBefore = await TotalAsync(client, "monthly-expenses", month, cancellationToken);
        var receivedBefore = await TotalAsync(client, "monthly-incomes", month, cancellationToken);
        var paid = await client.PostAsJsonAsync($"/v1/financing/creditor-installments/{installmentId}/pay", new { bankAccountId = bankId }, cancellationToken);
        Assert.True(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync(cancellationToken));
        await AccrueAsync();
        Assert.Equal(balanceBefore - 100_00, await BalanceAsync(client, bankId, cancellationToken));
        Assert.Equal(spentBefore + 100_00, await TotalAsync(client, "monthly-expenses", month, cancellationToken));
        Assert.Equal(receivedBefore, await TotalAsync(client, "monthly-incomes", month, cancellationToken));
        var row = Assert.Single(await MoneyFlowAsync(client, month, cancellationToken), r => r.GetProperty("description").GetString() == "Installment 1/1 · Shared sofa · Quino Creditor");
        Assert.Equal("Outcome", row.GetProperty("kind").GetString());
        Assert.Equal(100_00, row.GetProperty("amountMinorUnits").GetInt64());
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/parties/{partyId}/balance", cancellationToken);
        Assert.Equal(50_00, balance.GetProperty("balances").EnumerateArray().Single(r => r.GetProperty("currencyCode").GetString() == "ARS").GetProperty("balanceMinorUnits").GetInt64());
    }

    [Fact]
    public async Task Paying_the_party_share_first_then_my_part_nets_to_my_share_only() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var bankId = await RegisterDebitAsync(client, "Share First Bank", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Rita", cancellationToken);
        var installmentId = await SeedSplitPlanAsync(client, partyId, "Rita", cancellationToken);
        await DrainOutboxAsync();
        await AccrueAsync();
        var balanceBefore = await BalanceAsync(client, bankId, cancellationToken);
        (await client.PostAsJsonAsync($"/v1/financing/creditor-installments/{installmentId}/pay-party", new { partyId, bankAccountId = bankId }, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(balanceBefore, await BalanceAsync(client, bankId, cancellationToken));
        var mine = await client.PostAsJsonAsync($"/v1/financing/creditor-installments/{installmentId}/pay", new { bankAccountId = bankId }, cancellationToken);
        Assert.True(mine.IsSuccessStatusCode, await mine.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(balanceBefore - 50_00, await BalanceAsync(client, bankId, cancellationToken));
        var rows = await MoneyFlowAsync(client, month, cancellationToken);
        var outcomes = rows.Where(r => r.GetProperty("description").GetString() == "Installment 1/1 · Shared sofa · Rita Creditor").ToList();
        Assert.Equal(2, outcomes.Count);
        Assert.All(outcomes, r => Assert.Equal(50_00, r.GetProperty("amountMinorUnits").GetInt64()));
        Assert.Single(rows, r => r.GetProperty("description").GetString() == "Settlement from Rita");
    }

    private async Task DrainOutboxAsync() {
        var worker = ActivatorUtilities.CreateInstance<OutboxWorker>(factory.Services);
        var drain = typeof(OutboxWorker).GetMethod("DrainAllAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)drain.Invoke(worker, [CancellationToken.None])!;
    }

    private async Task AccrueAsync() {
        var type = typeof(Financing.FinancingModule).Assembly.GetType("PersonalFinance.Financing.Application.Scheduling.AccrueInstallments")!;
        var scheduler = Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            [
                factory.Services.GetRequiredService<IServiceScopeFactory>(),
                factory.Services.GetRequiredService(typeof(ILogger<>).MakeGenericType(type)),
                new FixedTimeProvider(DateTimeOffset.UtcNow.AddMonths(2))
            ],
            null
        )!;
        var tick = type.GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)tick.Invoke(scheduler, [CancellationToken.None])!;
    }

    private static async Task<Guid> SeedSplitPlanAsync(HttpClient client, Guid partyId, string name, CancellationToken cancellationToken) {
        var created = await client.PostAsJsonAsync("/v1/creditors", new { name = $"{name} Creditor", accounts = new[] { new { label = $"{name} Creditor Bank" } } }, cancellationToken);
        created.EnsureSuccessStatusCode();
        var creditorId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("creditorId").GetGuid();
        var list = await client.GetFromJsonAsync<JsonElement>("/v1/creditors", cancellationToken);
        var accountId = list.GetProperty("rows").EnumerateArray().Single(row => row.GetProperty("id").GetGuid() == creditorId).GetProperty("accounts")[0].GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync("/v1/financing/payment-plans", new {
            amountMinorUnits = 100_00,
            installmentCount = 1,
            purchaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            description = "Shared sofa",
            creditorId,
            creditorAccountId = accountId,
            currencyCode = "ARS",
            split = new[] { new { partyId, weight = 1L } }
        }, cancellationToken)).EnsureSuccessStatusCode();
        var detail = await client.GetFromJsonAsync<JsonElement>($"/v1/financing/creditor-payables/{creditorId}", cancellationToken);
        return detail.GetProperty("purchases")[0].GetProperty("installments")[0].GetProperty("installmentId").GetGuid();
    }

    private static async Task<long> BalanceAsync(HttpClient client, Guid accountId, CancellationToken cancellationToken) {
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/ledger/accounts/{accountId}/balance", cancellationToken);
        return balance.GetProperty("rows").EnumerateArray().Where(row => row.GetProperty("currencyCode").GetString() == "ARS")
            .Select(row => row.GetProperty("balanceMinorUnits").GetInt64())
        .FirstOrDefault();
    }

    private static async Task<long> TotalAsync(HttpClient client, string report, string month, CancellationToken cancellationToken) {
        var monthly = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/{report}?month={month}", cancellationToken);
        return monthly.GetProperty("rows").EnumerateArray().Sum(row => row.GetProperty("amountMinorUnits").GetInt64());
    }

    private static async Task<List<JsonElement>> MoneyFlowAsync(HttpClient client, string month, CancellationToken cancellationToken) {
        var flow = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/money-flow?month={month}", cancellationToken);
        return flow.GetProperty("rows").EnumerateArray().ToList();
    }

    private static async Task<Guid> CreatePartyAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/parties", new { name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> RegisterDebitAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/instruments", new { type = "debit", name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() {
            return now;
        }
    }
}
