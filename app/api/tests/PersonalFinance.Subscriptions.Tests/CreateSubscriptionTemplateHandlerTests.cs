using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Application.Commands.CreateSubscriptionTemplate;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;
using Xunit;

namespace PersonalFinance.Subscriptions.Tests;

public sealed class CreateSubscriptionTemplateHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<SubscriptionsDbContext> options;

    public CreateSubscriptionTemplateHandlerTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<SubscriptionsDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() {
        connection.Dispose();
    }

    [Fact]
    public async Task Handle_posts_one_charge_and_marks_the_period_paid_when_the_anchor_already_passed_this_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixedNow = new DateTimeOffset(2026, 3, 16, 0, 0, 0, TimeSpan.Zero);
        var ledger = new FakeLedgerApi();
        var command = new CreateSubscriptionTemplateCommand("Netflix", 1_500, "Streaming", Guid.CreateVersion7(), RecurrenceFrequency.Monthly, 5);

        var result = await new CreateSubscriptionTemplateHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(command, cancellationToken);

        Assert.True(result.IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero), posted.PostedOnUtc);
        var template = await LoadTemplateAsync(result.Value, cancellationToken);
        Assert.Equal(new DateOnly(2026, 3, 5), template.LastPaidPeriod);
        Assert.Equal(new DateOnly(2026, 4, 5), template.NextDueDate);
    }

    [Fact]
    public async Task Handle_posts_one_charge_dated_today_when_the_anchor_is_today() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixedNow = new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero);
        var ledger = new FakeLedgerApi();
        var command = new CreateSubscriptionTemplateCommand("Netflix", 1_500, "Streaming", Guid.CreateVersion7(), RecurrenceFrequency.Monthly, 5);

        var result = await new CreateSubscriptionTemplateHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(command, cancellationToken);

        Assert.True(result.IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero), posted.PostedOnUtc);
        var template = await LoadTemplateAsync(result.Value, cancellationToken);
        Assert.Equal(new DateOnly(2026, 3, 5), template.LastPaidPeriod);
    }

    [Fact]
    public async Task Handle_posts_nothing_and_leaves_the_period_upcoming_when_the_anchor_is_still_ahead_this_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixedNow = new DateTimeOffset(2026, 3, 16, 0, 0, 0, TimeSpan.Zero);
        var ledger = new FakeLedgerApi();
        var command = new CreateSubscriptionTemplateCommand("Netflix", 1_500, "Streaming", Guid.CreateVersion7(), RecurrenceFrequency.Monthly, 20);

        var result = await new CreateSubscriptionTemplateHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(command, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(ledger.PostedTransactions);
        var template = await LoadTemplateAsync(result.Value, cancellationToken);
        Assert.Null(template.LastPaidPeriod);
        Assert.Equal(new DateOnly(2026, 3, 20), template.NextDueDate);
    }

    private SubscriptionsDbContext NewContext() {
        return new SubscriptionsDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private async Task<SubscriptionTemplateSnapshot> LoadTemplateAsync(Guid id, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var template = await context.SubscriptionTemplates.SingleAsync(candidate => candidate.Id == id, cancellationToken);
        return new SubscriptionTemplateSnapshot(template.LastPaidPeriod, template.NextDueDate);
    }

    private sealed record SubscriptionTemplateSnapshot(DateOnly? LastPaidPeriod, DateOnly NextDueDate);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() {
            return now;
        }
    }

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();

        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }

    private sealed class FakeLedgerApi : ILedgerApi {
        public List<PostTransactionCommand> PostedTransactions { get; } = [];

        public Task<Result<Guid>> CreateAccountAsync(CreateAccountCommand command, CancellationToken ct = default) {
            return Task.FromResult<Result<Guid>>(Guid.CreateVersion7());
        }

        public Task<Result<Guid>> PostTransactionAsync(PostTransactionCommand command, CancellationToken ct = default) {
            PostedTransactions.Add(command);
            return Task.FromResult<Result<Guid>>(Guid.CreateVersion7());
        }

        public Task<Result<Guid>> GetOrCreateExpenseCategoryAsync(GetOrCreateExpenseCategoryCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<ExpenseCategoriesResponse> ListExpenseCategoriesAsync(ListExpenseCategoriesQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Result<Guid>> PostReceivableAsync(PostReceivableCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Result<ReverseTransactionResult>> ReverseTransactionAsync(ReverseTransactionCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Money>> GetAccountBalanceAsync(GetAccountBalanceQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Money> GetCardLiabilityAsync(GetCardLiabilityQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<InstrumentAccountsResponse> ListInstrumentAccountsAsync(ListInstrumentAccountsQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<TransactionFeedResponse> GetTransactionsAsync(GetTransactionsQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<AccrualTransactionIdsResponse> FindAccrualTransactionIdsAsync(FindAccrualTransactionIdsQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }
    }
}
