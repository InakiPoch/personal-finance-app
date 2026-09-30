using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Application.Queries.FindPaidSubscriptionIds;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public sealed class FindPaidSubscriptionIdsHandlerTests : IDisposable {
    private static readonly DateOnly march = new(2026, 3, 1);
    private static readonly DateTimeOffset inMarch = new(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset inApril = new(2026, 4, 10, 0, 0, 0, TimeSpan.Zero);

    private static readonly Account expense = Account.Create("Subscriptions", AccountType.Expense, AccountKind.Expense).Value;
    private static readonly Account bank = Account.Create("Checking", AccountType.Asset, AccountKind.Bank).Value;

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<LedgerDbContext> options;

    public FindPaidSubscriptionIdsHandlerTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = NewContext();
        context.Database.EnsureCreated();
        context.Accounts.AddRange(expense, bank);
        context.SaveChanges();
    }

    public void Dispose() {
        connection.Dispose();
    }

    [Fact]
    public async Task A_charge_posted_in_the_month_is_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        await SeedAsync(id, inMarch, reverse: false, cancellationToken);
        Assert.Equal([id], await RunAsync([id], cancellationToken));
    }

    [Fact]
    public async Task A_charge_posted_in_another_month_is_not_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        await SeedAsync(id, inApril, reverse: false, cancellationToken);
        Assert.Empty(await RunAsync([id], cancellationToken));
    }

    [Fact]
    public async Task A_charge_with_a_storno_is_not_paid_and_the_storno_never_counts_by_itself() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        // Both the original and its reversal are posted in March: neither may report the subscription as paid.
        await SeedAsync(id, inMarch, reverse: true, cancellationToken);
        Assert.Empty(await RunAsync([id], cancellationToken));
    }

    [Fact]
    public async Task A_reversal_in_the_month_does_not_count_when_the_original_is_in_another_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        await using(var context = NewContext()) {
            var original = Post(id, new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero));
            var storno = Transaction.Reverse(original, inMarch).Value;
            context.Transactions.AddRange(original, storno);
            await context.SaveChangesAsync(cancellationToken);
        }
        Assert.Empty(await RunAsync([id], cancellationToken));
    }

    [Fact]
    public async Task A_transaction_without_a_subscription_reference_is_ignored() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using(var context = NewContext()) {
            context.Transactions.Add(Post(null, inMarch));
            await context.SaveChangesAsync(cancellationToken);
        }
        Assert.Empty(await RunAsync([Guid.CreateVersion7()], cancellationToken));
    }

    [Fact]
    public async Task Ids_outside_the_requested_list_are_not_returned() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var asked = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await SeedAsync(asked, inMarch, reverse: false, cancellationToken);
        await SeedAsync(other, inMarch, reverse: false, cancellationToken);
        Assert.Equal([asked], await RunAsync([asked], cancellationToken));
    }

    private static Transaction Post(Guid? subscriptionId, DateTimeOffset postedOn) {
        var amount = Money.FromMinorUnits(1_000, Currency.Reference);
        return Transaction.Post(
            [
                new EntryDraft(expense.Id, DebitOrCredit.Debit, amount),
                new EntryDraft(bank.Id, DebitOrCredit.Credit, amount)
            ],
            postedOn,
            subscriptionReference: subscriptionId is { } value ? new SubscriptionReference(value) : null).Value;
    }

    private async Task SeedAsync(Guid subscriptionId, DateTimeOffset postedOn, bool reverse, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var original = Post(subscriptionId, postedOn);
        context.Transactions.Add(original);
        if(reverse) {
            context.Transactions.Add(Transaction.Reverse(original, postedOn.AddDays(1)).Value);
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> RunAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var response = await new FindPaidSubscriptionIdsHandler(context).HandleAsync(new FindPaidSubscriptionIdsQuery(ids, march), cancellationToken);
        return response.PaidSubscriptionIds;
    }

    private LedgerDbContext NewContext() {
        return new LedgerDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();

        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }
}
