using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Reporting.Dashboards;
using PersonalFinance.Reporting.Reports;
using Xunit;

namespace PersonalFinance.Reporting.Tests;

/// <summary>
/// Drives <see cref="MoneyFlowQuery"/> through the shared <see cref="IQueryBus"/> against the
/// seeded throwaway database — the accounting-style "money in / money out" table behind Slice 2
/// of <c>docs/incomes-support/</c>.
/// </summary>
public sealed class MoneyFlowQueryTests(ReportingIntegrationFixture fixture) : IClassFixture<ReportingIntegrationFixture> {
    [Fact]
    public async Task Outcome_total_reconciles_with_monthly_expenses_for_may() {
        var moneyFlow = await AskAsync(new MoneyFlowQuery("2026-05"));
        var monthlyExpenses = await AskAsync(new MonthlyExpensesQuery("2026-05"));
        var outcomeTotal = moneyFlow.Rows.Where(row => row.Kind == "Outcome").Sum(row => row.AmountMinorUnits);
        var expensesTotal = monthlyExpenses.Rows.Sum(row => row.AmountMinorUnits);
        Assert.Equal(expensesTotal, outcomeTotal);
    }

    [Fact]
    public async Task A_split_debit_expense_appears_as_one_outcome_row_at_the_holders_share() {
        var response = await AskAsync(new MoneyFlowQuery("2026-05"));
        var row = Assert.Single(response.Rows, row => row.Description == "Alice dinner");
        Assert.Equal("Outcome", row.Kind);
        Assert.Equal(5_000, row.AmountMinorUnits);
    }

    [Fact]
    public async Task An_income_row_carries_the_funding_accounts_name_and_description() {
        var response = await AskAsync(new MoneyFlowQuery("2026-05"));
        var row = Assert.Single(response.Rows, row => row.Description == "May salary");
        Assert.Equal("Income", row.Kind);
        Assert.Equal("Bank", row.AccountName);
        Assert.Equal(15_000, row.AmountMinorUnits);
        Assert.Equal("ARS", row.CurrencyCode);
    }

    [Fact]
    public async Task An_income_and_its_reversal_are_both_hidden() {
        var response = await AskAsync(new MoneyFlowQuery("2026-07"));
        Assert.Empty(response.Rows);
    }

    [Fact]
    public async Task An_expense_and_its_reversal_are_both_hidden() {
        var response = await AskAsync(new MoneyFlowQuery("2026-06"));
        Assert.DoesNotContain(response.Rows, row => row.Description == "June reversible expense");
    }

    [Fact]
    public async Task Card_purchase_accruals_and_statement_payments_produce_no_rows() {
        var response = await AskAsync(new MoneyFlowQuery("2026-06"));
        Assert.DoesNotContain(response.Rows, row => row.Description is "June card accrual" or "June statement payment");
        Assert.Equal(4, response.Rows.Count);
    }

    [Fact]
    public async Task A_description_less_transaction_falls_back_to_its_category_name() {
        var response = await AskAsync(new MoneyFlowQuery("2026-06"));
        var row = Assert.Single(response.Rows, row => row.AmountMinorUnits == 700);
        Assert.Equal("Groceries", row.Description);
        Assert.Equal("Outcome", row.Kind);
    }

    [Fact]
    public async Task Rows_are_scoped_to_the_requested_month_and_ordered_newest_first() {
        var response = await AskAsync(new MoneyFlowQuery("2026-06"));
        Assert.All(response.Rows, row => Assert.Equal(6, row.Date.Month));
        var dates = response.Rows.Select(row => row.Date).ToList();
        Assert.Equal(dates.OrderByDescending(date => date), dates);
    }

    [Fact]
    public async Task Outcome_total_reconciles_with_monthly_expenses_per_currency_for_june() {
        var moneyFlow = await AskAsync(new MoneyFlowQuery("2026-06"));
        var monthlyExpenses = await AskAsync(new MonthlyExpensesQuery("2026-06"));
        var arsOutcome = moneyFlow.Rows.Where(row => row.Kind == "Outcome" && row.CurrencyCode == "ARS").Sum(row => row.AmountMinorUnits);
        var usdOutcome = moneyFlow.Rows.Where(row => row.Kind == "Outcome" && row.CurrencyCode == "USD").Sum(row => row.AmountMinorUnits);
        var arsExpenses = monthlyExpenses.Rows.Where(row => row.CurrencyCode == "ARS").Sum(row => row.AmountMinorUnits);
        var usdExpenses = monthlyExpenses.Rows.Where(row => row.CurrencyCode == "USD").Sum(row => row.AmountMinorUnits);
        Assert.Equal(arsExpenses, arsOutcome);
        Assert.Equal(usdExpenses, usdOutcome);
        Assert.Equal(4_000, usdOutcome);
    }

    private async Task<TResponse> AskAsync<TResponse>(IQuery<TResponse> query) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();
        return await queryBus.AskAsync(query, TestContext.Current.CancellationToken);
    }
}
