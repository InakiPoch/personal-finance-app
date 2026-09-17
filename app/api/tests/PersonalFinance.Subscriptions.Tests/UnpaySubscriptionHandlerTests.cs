using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Application.Commands.PaySubscription;
using PersonalFinance.Subscriptions.Application.Commands.UnpaySubscription;
using PersonalFinance.Subscriptions.Application.Queries.GetActiveSubscriptions;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Contracts.Queries;
using PersonalFinance.Subscriptions.Domain;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;
using Xunit;

namespace PersonalFinance.Subscriptions.Tests;

public sealed class UnpaySubscriptionHandlerTests : IDisposable {
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<SubscriptionsDbContext> options;

    public UnpaySubscriptionHandlerTests() {
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
    public async Task Handle_undoes_a_pay_by_reversing_the_ledger_charge_and_restoring_the_prior_period() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedTemplateAsync("Netflix", anchorDay: 15, nextDueDate: new DateOnly(2026, 6, 15), lastPaidPeriod: null, cancellationToken);
        var ledger = new FakeLedgerApi();
        var paid = await new PaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new PaySubscriptionCommand(id), cancellationToken);
        Assert.True(paid.IsSuccess);
        var paidTransactionId = Assert.Single(ledger.PostedTransactionIds);

        var result = await new UnpaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new UnpaySubscriptionCommand(id), cancellationToken);

        Assert.True(result.IsSuccess);
        var reversedId = Assert.Single(ledger.ReversedTransactionIds);
        Assert.Equal(paidTransactionId, reversedId);
        var template = await LoadTemplateAsync(id, cancellationToken);
        Assert.Equal(new DateOnly(2026, 6, 15), template.NextDueDate);
        Assert.Null(template.LastPaidTransactionId);
        Assert.NotEqual("paid", await StatusAsync(cancellationToken));
    }

    [Fact]
    public async Task Handle_rejects_undo_when_the_current_period_was_never_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedTemplateAsync("Netflix", anchorDay: 15, nextDueDate: new DateOnly(2026, 6, 15), lastPaidPeriod: null, cancellationToken);
        var ledger = new FakeLedgerApi();

        var result = await new UnpaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new UnpaySubscriptionCommand(id), cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.SubscriptionNotPaid.Code, result.Error.Code);
        Assert.Empty(ledger.ReversedTransactionIds);
    }

    [Fact]
    public async Task Handle_rejects_undo_when_the_last_paid_period_is_a_prior_calendar_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedTemplateAsync("Netflix", anchorDay: 10, nextDueDate: new DateOnly(2026, 6, 10), lastPaidPeriod: new DateOnly(2026, 5, 10), cancellationToken);
        var ledger = new FakeLedgerApi();

        var result = await new UnpaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new UnpaySubscriptionCommand(id), cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.SubscriptionNotPaid.Code, result.Error.Code);
        Assert.Empty(ledger.ReversedTransactionIds);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_an_unknown_subscription() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var ledger = new FakeLedgerApi();

        var result = await new UnpaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new UnpaySubscriptionCommand(Guid.CreateVersion7()), cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.SubscriptionNotFound.Code, result.Error.Code);
        Assert.Empty(ledger.ReversedTransactionIds);
    }

    [Fact]
    public async Task Handle_rejects_undoing_a_cancelled_subscription() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedTemplateAsync("Netflix", anchorDay: 15, nextDueDate: new DateOnly(2026, 7, 15), lastPaidPeriod: new DateOnly(2026, 6, 15), cancellationToken);
        await CancelAsync(id, cancellationToken);
        var ledger = new FakeLedgerApi();

        var result = await new UnpaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new UnpaySubscriptionCommand(id), cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.SubscriptionNotActive.Code, result.Error.Code);
        Assert.Empty(ledger.ReversedTransactionIds);
    }

    [Fact]
    public async Task Handle_leaves_the_aggregate_untouched_when_the_ledger_reversal_fails() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedTemplateAsync("Netflix", anchorDay: 15, nextDueDate: new DateOnly(2026, 6, 15), lastPaidPeriod: null, cancellationToken);
        var ledger = new FakeLedgerApi();
        var paid = await new PaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new PaySubscriptionCommand(id), cancellationToken);
        Assert.True(paid.IsSuccess);
        var before = await LoadTemplateAsync(id, cancellationToken);
        ledger.FailNextReverse = true;

        var result = await new UnpaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new UnpaySubscriptionCommand(id), cancellationToken);

        Assert.True(result.IsFailure);
        var after = await LoadTemplateAsync(id, cancellationToken);
        Assert.Equal(before.LastPaidPeriod, after.LastPaidPeriod);
        Assert.Equal(before.LastPaidTransactionId, after.LastPaidTransactionId);
        Assert.Equal(before.NextDueDate, after.NextDueDate);
    }

    private async Task<Guid> SeedTemplateAsync(string name, int anchorDay, DateOnly nextDueDate, DateOnly? lastPaidPeriod, CancellationToken cancellationToken, long amountMinorUnits = 1_500) {
        await using var context = NewContext();
        var template = SubscriptionTemplate.Create(
            name,
            Money.FromMinorUnits(amountMinorUnits, Currency.Reference),
            "Streaming",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            RecurrenceFrequency.Monthly,
            anchorDay,
            nextDueDate
        ).Value;
        if(lastPaidPeriod is { } paidOn) {
            template.MarkCurrentPeriodPaid(paidOn, Guid.CreateVersion7());
        }
        context.SubscriptionTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);
        return template.Id;
    }

    private async Task CancelAsync(Guid id, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var template = await context.SubscriptionTemplates.SingleAsync(candidate => candidate.Id == id, cancellationToken);
        template.Cancel();
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> StatusAsync(CancellationToken cancellationToken) {
        var response = await new GetActiveSubscriptionsHandler(NewContext(), new FixedTimeProvider(fixedNow)).HandleAsync(new GetActiveSubscriptionsQuery(), cancellationToken);
        return Assert.Single(response.Rows).Status;
    }

    private SubscriptionsDbContext NewContext() {
        return new SubscriptionsDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private async Task<SubscriptionTemplateSnapshot> LoadTemplateAsync(Guid id, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var template = await context.SubscriptionTemplates.SingleAsync(candidate => candidate.Id == id, cancellationToken);
        return new SubscriptionTemplateSnapshot(template.LastPaidPeriod, template.LastPaidTransactionId, template.NextDueDate);
    }

    private sealed record SubscriptionTemplateSnapshot(DateOnly? LastPaidPeriod, Guid? LastPaidTransactionId, DateOnly NextDueDate);

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
        public List<Guid> PostedTransactionIds { get; } = [];
        public List<Guid> ReversedTransactionIds { get; } = [];
        public bool FailNextReverse { get; set; }

        public Task<Result<Guid>> CreateAccountAsync(CreateAccountCommand command, CancellationToken ct = default) {
            return Task.FromResult<Result<Guid>>(Guid.CreateVersion7());
        }

        public Task<Result<Guid>> PostTransactionAsync(PostTransactionCommand command, CancellationToken ct = default) {
            PostedTransactions.Add(command);
            var id = Guid.CreateVersion7();
            PostedTransactionIds.Add(id);
            return Task.FromResult<Result<Guid>>(id);
        }

        public Task<Result<ReverseTransactionResult>> ReverseTransactionAsync(ReverseTransactionCommand command, CancellationToken ct = default) {
            if(FailNextReverse) {
                return Task.FromResult(Result.Failure<ReverseTransactionResult>(new Error("Ledger.ReversalFailed", "Simulated ledger failure.")));
            }
            ReversedTransactionIds.Add(command.OriginalTransactionId);
            return Task.FromResult<Result<ReverseTransactionResult>>(new ReverseTransactionResult(Guid.CreateVersion7(), false));
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

        public Task<Money> GetAccountBalanceAsync(GetAccountBalanceQuery query, CancellationToken ct = default) {
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
