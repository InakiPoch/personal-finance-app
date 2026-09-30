using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Reporting.Reports;
using Xunit;

namespace PersonalFinance.Reporting.Tests;

/// <summary>
/// Drives <see cref="TransactionFeedQuery"/> / <see cref="GetTransactionFeedRowQuery"/> against the seeded throwaway database:
/// real plan, card, subscription and category names show up in the descriptions.
/// </summary>
public sealed class TransactionFeedTests(ReportingIntegrationFixture fixture) : IClassFixture<ReportingIntegrationFixture>, IAsyncLifetime {
    public ValueTask InitializeAsync() {
        return new ValueTask(fixture.EnsureTransactionFeedSeedAsync());
    }

    public ValueTask DisposeAsync() {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Card_installments_carry_the_plan_and_card_names_and_both_paid_and_unpaid_wording() {
        var feed = await AskAsync(new TransactionFeedQuery(null, null, null));
        var installments = feed.Rows.Where(row => row.Kind == "Card installment" && row.Description.StartsWith("Feed Notebook — installment", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(installments);
        Assert.All(installments, row => Assert.EndsWith("of 6 on Feed Visa", row.Description));
        Assert.Contains(installments, row => row.ImpactLines.Any(line => line.EndsWith("comes back as a credit on your next Feed Visa bill.", StringComparison.Ordinal)));
        Assert.Contains(installments, row => row.ImpactLines.Any(line => line.StartsWith("Your Feed Visa bill goes down by ARS", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_subscription_charge_shows_the_subscription_name() {
        var feed = await AskAsync(new TransactionFeedQuery(null, null, null));
        var row = Assert.Single(feed.Rows, row => row.Kind == "Subscription");
        Assert.Equal("Feed Netflix", row.Description);
        Assert.Contains("Feed Bank", row.ToAccounts);
    }

    [Fact]
    public async Task Income_shared_expense_and_manual_rows_use_their_own_descriptions() {
        var feed = await AskAsync(new TransactionFeedQuery(null, null, null));
        Assert.Equal("Income", Assert.Single(feed.Rows, row => row.Description == "May salary").Kind);
        Assert.Equal("Shared expense", Assert.Single(feed.Rows, row => row.Description == "Alice dinner").Kind);
        Assert.Equal("Expense", Assert.Single(feed.Rows, row => row.Description == "June groceries").Kind);
        Assert.Equal("Other", Assert.Single(feed.Rows, row => row.Id == fixture.FeedManualTransactionId).Kind);
    }

    [Fact]
    public async Task An_undone_transaction_and_its_undo_entry_are_flagged() {
        var feed = await AskAsync(new TransactionFeedQuery(null, null, null));
        var original = Assert.Single(feed.Rows, row => row.Description == "July gift");
        Assert.True(original.IsUndone);
        Assert.False(original.IsUndoEntry);
        Assert.Equal(["Already undone."], original.ImpactLines);
        var undo = Assert.Single(feed.Rows, row => row.Description == "Undid: July gift");
        Assert.True(undo.IsUndoEntry);
        Assert.Equal("Undo entry", undo.Kind);
        Assert.Empty(undo.ImpactLines);
    }

    [Fact]
    public async Task The_feed_is_newest_first_and_never_uses_developer_words() {
        var feed = await AskAsync(new TransactionFeedQuery(null, null, null));
        var dates = feed.Rows.Select(row => row.PostedOnUtc).ToList();
        Assert.Equal(dates.OrderByDescending(date => date), dates);
        foreach(var row in feed.Rows) {
            var texts = row.ImpactLines.Concat(row.FromAccounts).Concat(row.ToAccounts).Append(row.Description);
            foreach(var text in texts) {
                Assert.DoesNotContain("Liability", text, StringComparison.Ordinal);
                Assert.DoesNotContain("Receivable", text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task The_account_filter_keeps_only_transactions_touching_that_account() {
        var feed = await AskAsync(new TransactionFeedQuery(fixture.FeedBankId, null, null));
        Assert.Contains(feed.Rows, row => row.Id == fixture.FeedManualTransactionId);
        Assert.DoesNotContain(feed.Rows, row => row.Description == "May salary");
        Assert.All(feed.Rows, row => Assert.Contains("Feed Bank", row.FromAccounts.Concat(row.ToAccounts).ToList()));
    }

    [Fact]
    public async Task The_date_window_is_inclusive_on_both_ends() {
        var feed = await AskAsync(new TransactionFeedQuery(null, new DateOnly(2026, 6, 20), new DateOnly(2026, 6, 20)));
        Assert.Contains(feed.Rows, row => row.Id == fixture.FeedManualTransactionId);
        Assert.All(feed.Rows, row => Assert.Equal(new DateOnly(2026, 6, 20), DateOnly.FromDateTime(row.PostedOnUtc.UtcDateTime)));
        var before = await AskAsync(new TransactionFeedQuery(null, new DateOnly(2026, 6, 21), null));
        Assert.DoesNotContain(before.Rows, row => row.Id == fixture.FeedManualTransactionId);
        var after = await AskAsync(new TransactionFeedQuery(null, null, new DateOnly(2026, 6, 19)));
        Assert.DoesNotContain(after.Rows, row => row.Id == fixture.FeedManualTransactionId);
    }

    [Fact]
    public async Task Getting_one_transaction_returns_exactly_that_row() {
        var row = await AskAsync(new GetTransactionFeedRowQuery(fixture.FeedManualTransactionId));
        Assert.NotNull(row);
        Assert.Equal(fixture.FeedManualTransactionId, row.Id);
        Assert.Equal("Feed manual transfer", row.Description);
        Assert.Equal(1_500, row.AmountMinorUnits);
    }

    [Fact]
    public async Task Getting_an_unknown_transaction_returns_null() {
        Assert.Null(await AskAsync(new GetTransactionFeedRowQuery(Guid.NewGuid())));
    }

    private async Task<TResponse> AskAsync<TResponse>(PersonalFinance.Abstractions.Messaging.IQuery<TResponse> query) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();
        return await queryBus.AskAsync(query, TestContext.Current.CancellationToken);
    }
}
