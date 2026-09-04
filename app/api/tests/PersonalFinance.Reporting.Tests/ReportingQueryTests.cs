using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Reporting.Dashboards;
using PersonalFinance.Reporting.Reports;
using Xunit;

namespace PersonalFinance.Reporting.Tests;

/// <summary>
/// Drives every Reporting query through the shared <see cref="IQueryBus"/> against the seeded
/// throwaway database and checks the numbers survived the round-trip through the <c>vw_*</c> views.
/// </summary>
public sealed class ReportingQueryTests(ReportingIntegrationFixture fixture) : IClassFixture<ReportingIntegrationFixture> {
    [Fact]
    public async Task MonthlyExpenses_includes_debit_and_cash_spend_but_excludes_card_purchases() {
        var response = await AskAsync(new MonthlyExpensesQuery("2026-05"));
        Assert.Equal(5_000, response.Rows.Where(row => row.Category == "Groceries").Sum(row => row.AmountMinorUnits));
        Assert.Equal(30_000, response.Rows.Where(row => row.Category == "Rent").Sum(row => row.AmountMinorUnits));
        Assert.Equal(2_000, response.Rows.Where(row => row.Category == "Snacks").Sum(row => row.AmountMinorUnits));
        Assert.DoesNotContain(response.Rows, row => row.Category == "Card Purchases");
        Assert.Equal(46_000, response.Rows.Sum(row => row.AmountMinorUnits));
        Assert.All(response.Rows, row => Assert.Equal("2026-05", row.Month));
        Assert.All(response.Rows, row => Assert.Equal("ARS", row.CurrencyCode));
    }

    [Fact]
    public async Task CardDueByMonth_separates_accrued_liability_from_future_installments() {
        var response = await AskAsync(new CardDueByMonthQuery());
        var accrued = response.Rows.Where(row => row.Bucket == "Accrued").ToList();
        var future = response.Rows.Where(row => row.Bucket == "Future").ToList();
        Assert.NotEmpty(accrued);
        Assert.NotEmpty(future);
        Assert.Equal(12_000, accrued.Sum(row => row.AmountMinorUnits));
        Assert.Equal(300_000, future.Sum(row => row.AmountMinorUnits));
        Assert.All(accrued, row => Assert.Null(row.CycleYear));
        Assert.All(future, row => Assert.NotNull(row.CycleYear));
    }

    [Fact]
    public async Task CardDueByMonth_accrued_and_future_rows_share_a_per_card_key() {
        var response = await AskAsync(new CardDueByMonthQuery());
        var cardKey = fixture.ReportingCardId.ToString();
        var future = response.Rows.Where(row => row.Bucket == "Future").ToList();
        var accrued = response.Rows.Where(row => row.Bucket == "Accrued").ToList();
        Assert.NotEmpty(future);
        Assert.All(future, row => Assert.Equal(cardKey, row.CardId));
        Assert.Contains(accrued, row => row.CardId == cardKey);
    }

    [Fact]
    public async Task PartyTimeline_reflects_the_seeded_shared_expense_and_settlement() {
        var response = await AskAsync(new GetPartyTimelineQuery(fixture.AliceId));
        Assert.Equal(2, response.Rows.Count);
        Assert.Contains(response.Rows, row => row.Description == "Shared expense");
        Assert.Contains(response.Rows, row => row.Description == "Settlement");
        Assert.Equal(fixture.AliceOwed, response.Rows[0].RunningBalanceMinorUnits);
        Assert.Equal(0, response.Rows[^1].RunningBalanceMinorUnits);
    }

    [Fact]
    public async Task PartyTimeline_rows_carry_the_ledger_transaction_behind_each_movement() {
        var response = await AskAsync(new GetPartyTimelineQuery(fixture.AliceId));
        Assert.NotEmpty(response.Rows);
        Assert.All(response.Rows, row => Assert.NotEqual(Guid.Empty, row.TransactionId));
    }

    [Fact]
    public async Task DebtByParty_nets_each_parties_movements_into_a_single_row() {
        var response = await AskAsync(new GetDebtByPartyQuery());
        Assert.Equal(2, response.Rows.Count);
        var alice = Assert.Single(response.Rows, row => row.PartyId == fixture.AliceId);
        Assert.Equal("Alice Reporting", alice.PartyName);
        Assert.Equal(0, alice.NetBalanceMinorUnits);
        var bob = Assert.Single(response.Rows, row => row.PartyId == fixture.BobId);
        Assert.Equal(fixture.BobOwed, bob.NetBalanceMinorUnits);
    }

    private async Task<TResponse> AskAsync<TResponse>(IQuery<TResponse> query) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();
        return await queryBus.AskAsync(query, TestContext.Current.CancellationToken);
    }
}
