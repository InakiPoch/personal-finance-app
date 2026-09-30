using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Application.Commands.PaySubscription;
using PersonalFinance.Subscriptions.Application.Queries.GetActiveSubscriptions;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Contracts.Queries;
using PersonalFinance.Subscriptions.Domain;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;
using Xunit;

namespace PersonalFinance.Subscriptions.Tests;

public sealed class PaySubscriptionHandlerTests : IDisposable {
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<SubscriptionsDbContext> options;

    public PaySubscriptionHandlerTests() {
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
    public async Task Handle_pays_a_period_overdue_this_month_and_settles_it() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedTemplateAsync("Netflix", anchorDay: 15, nextDueDate: new DateOnly(2026, 6, 15), lastPaidPeriod: null, cancellationToken);
        var ledger = new FakeLedgerApi();

        var result = await new PaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new PaySubscriptionCommand(id), cancellationToken);

        Assert.True(result.IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(fixedNow, posted.PostedOnUtc);
        var template = await LoadTemplateAsync(id, cancellationToken);
        Assert.Equal(new DateOnly(2026, 6, 15), template.LastPaidPeriod);
        Assert.Equal(new DateOnly(2026, 7, 15), template.NextDueDate);
        Assert.Equal("paid", await StatusAsync(cancellationToken));
    }

    [Fact]
    public async Task Handle_settles_one_overdue_period_at_a_time_when_several_are_owed() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedTemplateAsync("Netflix", anchorDay: 15, nextDueDate: new DateOnly(2026, 4, 15), lastPaidPeriod: null, cancellationToken);
        var ledger = new FakeLedgerApi();

        var first = await new PaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new PaySubscriptionCommand(id), cancellationToken);
        Assert.True(first.IsSuccess);
        Assert.Single(ledger.PostedTransactions);
        Assert.Equal(new DateOnly(2026, 5, 15), (await LoadTemplateAsync(id, cancellationToken)).NextDueDate);
        Assert.Equal("overdue", await StatusAsync(cancellationToken));

        var second = await new PaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new PaySubscriptionCommand(id), cancellationToken);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, ledger.PostedTransactions.Count);
        Assert.Equal(new DateOnly(2026, 6, 15), (await LoadTemplateAsync(id, cancellationToken)).NextDueDate);
        Assert.Equal("overdue", await StatusAsync(cancellationToken));

        var third = await new PaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new PaySubscriptionCommand(id), cancellationToken);
        Assert.True(third.IsSuccess);
        Assert.Equal(3, ledger.PostedTransactions.Count);
        var template = await LoadTemplateAsync(id, cancellationToken);
        Assert.Equal(new DateOnly(2026, 6, 15), template.LastPaidPeriod);
        Assert.Equal(new DateOnly(2026, 7, 15), template.NextDueDate);
        Assert.Equal("paid", await StatusAsync(cancellationToken));
    }

    [Fact]
    public async Task Handle_posts_the_charge_in_the_templates_currency_when_it_is_usd() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedTemplateAsync("Netflix", anchorDay: 15, nextDueDate: new DateOnly(2026, 6, 15), lastPaidPeriod: null, cancellationToken, currency: Currency.Usd);
        var ledger = new FakeLedgerApi();

        var result = await new PaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new PaySubscriptionCommand(id), cancellationToken);

        Assert.True(result.IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.All(posted.Lines, line => Assert.Equal(Currency.Usd, line.Amount.Currency));
    }

    [Fact]
    public async Task Handle_rejects_a_second_pay_for_a_period_already_paid_this_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedTemplateAsync("Netflix", anchorDay: 10, nextDueDate: new DateOnly(2026, 7, 10), lastPaidPeriod: new DateOnly(2026, 6, 10), cancellationToken);
        var ledger = new FakeLedgerApi();

        var result = await new PaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new PaySubscriptionCommand(id), cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.SubscriptionAlreadyPaid.Code, result.Error.Code);
        Assert.Empty(ledger.PostedTransactions);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_an_unknown_subscription() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var ledger = new FakeLedgerApi();

        var result = await new PaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new PaySubscriptionCommand(Guid.CreateVersion7()), cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.SubscriptionNotFound.Code, result.Error.Code);
        Assert.Empty(ledger.PostedTransactions);
    }

    [Fact]
    public async Task Handle_rejects_paying_a_cancelled_subscription() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedTemplateAsync("Netflix", anchorDay: 15, nextDueDate: new DateOnly(2026, 6, 15), lastPaidPeriod: null, cancellationToken);
        await CancelAsync(id, cancellationToken);
        var ledger = new FakeLedgerApi();

        var result = await new PaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new PaySubscriptionCommand(id), cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.SubscriptionNotActive.Code, result.Error.Code);
        Assert.Empty(ledger.PostedTransactions);
    }

    [Fact]
    public async Task Handle_leaves_the_aggregate_untouched_when_the_ledger_post_fails() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedTemplateAsync("Netflix", anchorDay: 15, nextDueDate: new DateOnly(2026, 6, 15), lastPaidPeriod: null, cancellationToken);
        var before = await LoadTemplateAsync(id, cancellationToken);
        var ledger = new FakeLedgerApi { FailNextPost = true };

        var result = await new PaySubscriptionHandler(NewContext(), ledger, new FixedTimeProvider(fixedNow)).HandleAsync(new PaySubscriptionCommand(id), cancellationToken);

        Assert.True(result.IsFailure);
        var after = await LoadTemplateAsync(id, cancellationToken);
        Assert.Equal(before.LastPaidPeriod, after.LastPaidPeriod);
        Assert.Equal(before.NextDueDate, after.NextDueDate);
    }

    private async Task<Guid> SeedTemplateAsync(string name, int anchorDay, DateOnly nextDueDate, DateOnly? lastPaidPeriod, CancellationToken cancellationToken, long amountMinorUnits = 1_500, Currency? currency = null) {
        await using var context = NewContext();
        var template = SubscriptionTemplate.Create(
            name,
            Money.FromMinorUnits(amountMinorUnits, currency ?? Currency.Reference),
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
        public bool FailNextPost { get; set; }

        public Task<Result<Guid>> CreateAccountAsync(CreateAccountCommand command, CancellationToken ct = default) {
            return Task.FromResult<Result<Guid>>(Guid.CreateVersion7());
        }

        public Task<Result<Guid>> PostTransactionAsync(PostTransactionCommand command, CancellationToken ct = default) {
            if(FailNextPost) {
                return Task.FromResult(Result.Failure<Guid>(new Error("Ledger.PostFailed", "Simulated ledger failure.")));
            }
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

        public Task<IReadOnlyList<Money>> GetCardLiabilityAsync(GetCardLiabilityQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<InstrumentAccountsResponse> ListInstrumentAccountsAsync(ListInstrumentAccountsQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<AccrualTransactionIdsResponse> FindAccrualTransactionIdsAsync(FindAccrualTransactionIdsQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }
    }
}
