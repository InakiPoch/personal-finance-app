using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Application.Queries.GetSubscriptionsByMonth;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Contracts.Queries;
using PersonalFinance.Subscriptions.Domain;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;
using Xunit;

namespace PersonalFinance.Subscriptions.Tests;

public sealed class GetSubscriptionsByMonthHandlerTests : IDisposable {
    private static readonly DateTimeOffset fixedNow = new(2026, 3, 16, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<SubscriptionsDbContext> options;
    private readonly FakeQueryBus queryBus = new();

    public GetSubscriptionsByMonthHandlerTests() {
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

    [Theory]
    [InlineData(2026, 4, 30)]
    [InlineData(2027, 2, 28)]
    public async Task A_future_month_is_all_upcoming_with_the_anchor_day_clamped_to_the_month_length(int year, int month, int expectedDay) {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Overdue today and paid this period: future months still show "upcoming".
        await SeedAsync("Late", 31, new DateOnly(2026, 2, 28), null, cancellationToken);
        await SeedAsync("Paid", 31, new DateOnly(2026, 3, 31), new DateOnly(2026, 3, 31), cancellationToken);
        var rows = await RunAsync(new DateOnly(year, month, 1), cancellationToken);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => {
            Assert.Equal("upcoming", row.Status);
            Assert.Equal(new DateOnly(year, month, expectedDay), row.DueDate);
        });
    }

    [Fact]
    public async Task The_current_month_reports_paid_overdue_and_upcoming_like_the_active_list() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync("Paid", 5, new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 5), cancellationToken);
        await SeedAsync("Overdue", 10, new DateOnly(2026, 3, 10), null, cancellationToken);
        await SeedAsync("Upcoming", 20, new DateOnly(2026, 3, 20), null, cancellationToken);
        var rows = await RunAsync(new DateOnly(2026, 3, 1), cancellationToken);
        Assert.Equal("paid", rows.Single(row => row.Name == "Paid").Status);
        Assert.Equal("overdue", rows.Single(row => row.Name == "Overdue").Status);
        Assert.Equal("upcoming", rows.Single(row => row.Name == "Upcoming").Status);
        Assert.Empty(queryBus.Asked);
    }

    [Fact]
    public async Task A_past_month_is_paid_when_the_ledger_reports_the_id_and_overdue_otherwise() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var paidId = await SeedAsync("Paid", 10, new DateOnly(2026, 3, 10), null, cancellationToken);
        await SeedAsync("Unpaid", 10, new DateOnly(2026, 3, 10), null, cancellationToken);
        queryBus.PaidIds = [paidId];
        var rows = await RunAsync(new DateOnly(2026, 1, 1), cancellationToken);
        Assert.Equal("paid", rows.Single(row => row.Name == "Paid").Status);
        Assert.Equal("overdue", rows.Single(row => row.Name == "Unpaid").Status);
        Assert.Equal(new DateOnly(2026, 1, 1), Assert.Single(queryBus.Asked).Month);
    }

    [Fact]
    public async Task Cancelled_templates_are_excluded() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync("Kept", 10, new DateOnly(2026, 3, 10), null, cancellationToken);
        await SeedAsync("Gone", 10, new DateOnly(2026, 3, 10), null, cancellationToken, cancel: true);
        var row = Assert.Single(await RunAsync(new DateOnly(2026, 4, 1), cancellationToken));
        Assert.Equal("Kept", row.Name);
    }

    [Fact]
    public async Task Rows_are_ordered_by_due_date_then_name() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync("Zeta", 5, new DateOnly(2026, 3, 5), null, cancellationToken);
        await SeedAsync("Beta", 20, new DateOnly(2026, 3, 20), null, cancellationToken);
        await SeedAsync("Alpha", 20, new DateOnly(2026, 3, 20), null, cancellationToken);
        var rows = await RunAsync(new DateOnly(2026, 4, 1), cancellationToken);
        Assert.Equal(["Zeta", "Alpha", "Beta"], rows.Select(row => row.Name));
    }

    private async Task<Guid> SeedAsync(string name, int anchorDay, DateOnly nextDueDate, DateOnly? markPaidOn, CancellationToken cancellationToken, bool cancel = false) {
        await using var context = NewContext();
        var template = SubscriptionTemplate.Create(
            name,
            Money.FromMinorUnits(1_000, Currency.Reference),
            "Streaming",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            RecurrenceFrequency.Monthly,
            anchorDay,
            nextDueDate
        ).Value;
        if(markPaidOn is { } paidOn) {
            template.MarkCurrentPeriodPaid(paidOn, Guid.CreateVersion7());
        }
        if(cancel) {
            template.Cancel();
        }
        context.SubscriptionTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);
        return template.Id;
    }

    private async Task<IReadOnlyList<SubscriptionByMonthRow>> RunAsync(DateOnly month, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var response = await new GetSubscriptionsByMonthHandler(context, queryBus, new FixedTimeProvider(fixedNow)).HandleAsync(new GetSubscriptionsByMonthQuery(month), cancellationToken);
        return response.Rows;
    }

    private SubscriptionsDbContext NewContext() {
        return new SubscriptionsDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private sealed class FakeQueryBus : IQueryBus {
        public IReadOnlyList<Guid> PaidIds { get; set; } = [];
        public List<FindPaidSubscriptionIdsQuery> Asked { get; } = [];

        public Task<TResult> AskAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) {
            var paid = (FindPaidSubscriptionIdsQuery)(object)query;
            Asked.Add(paid);
            var ids = PaidIds.Where(paid.SubscriptionIds.Contains).ToList();
            return Task.FromResult((TResult)(object)new PaidSubscriptionIdsResponse(ids));
        }
    }

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
}
