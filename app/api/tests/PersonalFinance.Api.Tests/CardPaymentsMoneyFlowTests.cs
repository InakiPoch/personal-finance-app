using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class CardPaymentsMoneyFlowTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Paying_a_card_bill_shows_one_outcome_row_but_does_not_change_spent_or_received() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterAsync(client, new { type = "debit", name = "Bill Bank" }, cancellationToken);
        var cardId = await RegisterAsync(client, new { type = "credit", name = "Bill Visa", cutoffDate = 15 }, cancellationToken);
        await CreateBackdatedPlanAsync(client, cardId, 60_00, 1, "Bill laptop", cancellationToken);
        var statement = (await client.GetFromJsonAsync<JsonElement>($"/v1/financing/cards/{cardId}/statements", cancellationToken))
            .GetProperty("rows").EnumerateArray().Single();
        var billingMonth = new DateTime(statement.GetProperty("cycleYear").GetInt32(), statement.GetProperty("cycleMonth").GetInt32(), 1)
            .ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var thisMonth = DateTime.UtcNow.ToString("yyyy-MM");
        var spentBefore = await SumAsync(client, "monthly-expenses", thisMonth, cancellationToken);
        var receivedBefore = await SumAsync(client, "monthly-incomes", thisMonth, cancellationToken);
        var pay = await client.PostAsJsonAsync($"/v1/financing/statements/{statement.GetProperty("statementId").GetGuid()}/pay", new { bankAccountId = bankId, paidOnUtc = DateTimeOffset.UtcNow }, cancellationToken);
        pay.EnsureSuccessStatusCode();
        var row = Assert.Single(await MoneyFlowAsync(client, thisMonth, cancellationToken), r => r.GetProperty("description").GetString() == $"Card bill · Bill Visa · {billingMonth}");
        Assert.Equal("Outcome", row.GetProperty("kind").GetString());
        Assert.Equal(60_00, row.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("Bill Bank", row.GetProperty("accountName").GetString());
        Assert.Equal(spentBefore, await SumAsync(client, "monthly-expenses", thisMonth, cancellationToken));
        Assert.Equal(receivedBefore, await SumAsync(client, "monthly-incomes", thisMonth, cancellationToken));
    }

    [Fact]
    public async Task Paying_a_single_installment_shows_one_outcome_row_describing_the_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterAsync(client, new { type = "debit", name = "Inst Bank" }, cancellationToken);
        var cardId = await RegisterAsync(client, new { type = "credit", name = "Inst Visa", cutoffDate = 15 }, cancellationToken);
        await CreateBackdatedPlanAsync(client, cardId, 90_00, 3, "Inst phone", cancellationToken);
        var statementId = (await client.GetFromJsonAsync<JsonElement>($"/v1/financing/cards/{cardId}/statements", cancellationToken))
            .GetProperty("rows").EnumerateArray().First().GetProperty("statementId").GetGuid();
        var installmentId = (await client.GetFromJsonAsync<JsonElement>($"/v1/financing/statements/{statementId}", cancellationToken))
            .GetProperty("installments").EnumerateArray().Single(i => i.GetProperty("sequence").GetInt32() == 1).GetProperty("installmentId").GetGuid();
        var thisMonth = DateTime.UtcNow.ToString("yyyy-MM");
        var spentBefore = await SumAsync(client, "monthly-expenses", thisMonth, cancellationToken);
        var pay = await client.PostAsJsonAsync($"/v1/financing/installments/{installmentId}/pay", new { bankAccountId = bankId, paidOnUtc = DateTimeOffset.UtcNow }, cancellationToken);
        pay.EnsureSuccessStatusCode();
        var row = Assert.Single(await MoneyFlowAsync(client, thisMonth, cancellationToken), r => r.GetProperty("description").GetString() == "Installment 1/3 · Inst phone · Inst Visa");
        Assert.Equal("Outcome", row.GetProperty("kind").GetString());
        Assert.Equal(30_00, row.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal("Inst Bank", row.GetProperty("accountName").GetString());
        Assert.Equal(spentBefore, await SumAsync(client, "monthly-expenses", thisMonth, cancellationToken));
    }

    [Fact]
    public async Task A_card_payment_without_a_description_falls_back_to_the_card_name_and_hides_once_reversed() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await RegisterAsync(client, new { type = "debit", name = "Old Bank" }, cancellationToken);
        await using var scope = factory.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var liability = await ledger.CreateAccountAsync(new CreateAccountCommand("Old Visa Liability", AccountType.Liability, AccountKind.CardLiability), cancellationToken);
        Assert.True(liability.IsSuccess, $"{liability.Error}");
        var amount = new Money(25_00, Currency.Reference);
        var posting = await ledger.PostTransactionAsync(new PostTransactionCommand([
            new PostTransactionLine(liability.Value, DebitOrCredit.Debit, amount),
            new PostTransactionLine(bankId, DebitOrCredit.Credit, amount)
        ], new DateTimeOffset(2022, 8, 5, 12, 0, 0, TimeSpan.Zero)), cancellationToken);
        Assert.True(posting.IsSuccess, $"{posting.Error}");
        var row = Assert.Single(await MoneyFlowAsync(client, "2022-08", cancellationToken));
        Assert.Equal("Card bill · Old Visa", row.GetProperty("description").GetString());
        Assert.Equal(25_00, row.GetProperty("amountMinorUnits").GetInt64());
        Assert.Equal(0, await SumAsync(client, "monthly-expenses", "2022-08", cancellationToken));
        (await client.PostAsync($"/v1/ledger/transactions/{posting.Value}/reversal", null, cancellationToken)).EnsureSuccessStatusCode();
        Assert.Empty(await MoneyFlowAsync(client, "2022-08", cancellationToken));
    }

    private static async Task CreateBackdatedPlanAsync(HttpClient client, Guid cardId, long amount, int installments, string description, CancellationToken cancellationToken) {
        var previousMonth = DateTime.UtcNow.AddMonths(-1);
        var response = await client.PostAsJsonAsync("/v1/financing/payment-plans", new {
            amountMinorUnits = amount,
            cardId,
            installmentCount = installments,
            purchaseDate = new DateOnly(previousMonth.Year, previousMonth.Month, 10).ToString("yyyy-MM-dd"),
            description
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<long> SumAsync(HttpClient client, string report, string month, CancellationToken cancellationToken) {
        var monthly = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/{report}?month={month}", cancellationToken);
        return monthly.GetProperty("rows").EnumerateArray().Sum(row => row.GetProperty("amountMinorUnits").GetInt64());
    }

    private static async Task<List<JsonElement>> MoneyFlowAsync(HttpClient client, string month, CancellationToken cancellationToken) {
        var flow = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/money-flow?month={month}", cancellationToken);
        return flow.GetProperty("rows").EnumerateArray().ToList();
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, object body, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/instruments", body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
    }
}
