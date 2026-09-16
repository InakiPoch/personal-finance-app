using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Application.Queries.GetActiveSubscriptions;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Contracts.Queries;
using PersonalFinance.Subscriptions.Domain;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;
using Xunit;

namespace PersonalFinance.Subscriptions.Tests;

public sealed class GetActiveSubscriptionsHandlerTests : IDisposable {
    private static readonly DateTimeOffset fixedNow = new(2026, 3, 16, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<SubscriptionsDbContext> options;

    public GetActiveSubscriptionsHandlerTests() {
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
    public async Task Handle_reports_overdue_for_an_unpaid_template_whose_next_due_date_is_in_the_past() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedTemplateAsync("Overdue Co", anchorDay: 10, nextDueDate: new DateOnly(2026, 2, 10), markPaidOn: null, cancellationToken);

        var response = await new GetActiveSubscriptionsHandler(NewContext(), new FixedTimeProvider(fixedNow)).HandleAsync(new GetActiveSubscriptionsQuery(), cancellationToken);

        var row = Assert.Single(response.Rows);
        Assert.Equal("overdue", row.Status);
    }

    [Fact]
    public async Task Handle_reports_paid_for_a_template_paid_this_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedTemplateAsync("Paid Co", anchorDay: 5, nextDueDate: new DateOnly(2026, 3, 5), markPaidOn: new DateOnly(2026, 3, 5), cancellationToken);

        var response = await new GetActiveSubscriptionsHandler(NewContext(), new FixedTimeProvider(fixedNow)).HandleAsync(new GetActiveSubscriptionsQuery(), cancellationToken);

        var row = Assert.Single(response.Rows);
        Assert.Equal("paid", row.Status);
    }

    [Fact]
    public async Task Handle_reports_upcoming_for_an_unpaid_template_due_later_this_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedTemplateAsync("Upcoming Co", anchorDay: 20, nextDueDate: new DateOnly(2026, 3, 20), markPaidOn: null, cancellationToken);

        var response = await new GetActiveSubscriptionsHandler(NewContext(), new FixedTimeProvider(fixedNow)).HandleAsync(new GetActiveSubscriptionsQuery(), cancellationToken);

        var row = Assert.Single(response.Rows);
        Assert.Equal("upcoming", row.Status);
    }

    [Fact]
    public async Task Handle_keeps_the_flat_amount_untouched_regardless_of_status() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedTemplateAsync("Flat Co", anchorDay: 10, nextDueDate: new DateOnly(2026, 2, 10), markPaidOn: null, cancellationToken, amountMinorUnits: 1_500);

        var response = await new GetActiveSubscriptionsHandler(NewContext(), new FixedTimeProvider(fixedNow)).HandleAsync(new GetActiveSubscriptionsQuery(), cancellationToken);

        var row = Assert.Single(response.Rows);
        Assert.Equal(1_500, row.AmountMinorUnits);
    }

    private async Task SeedTemplateAsync(string name, int anchorDay, DateOnly nextDueDate, DateOnly? markPaidOn, CancellationToken cancellationToken, long amountMinorUnits = 1_000) {
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
        if(markPaidOn is { } paidOn) {
            template.MarkCurrentPeriodPaid(paidOn);
        }
        context.SubscriptionTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);
    }

    private SubscriptionsDbContext NewContext() {
        return new SubscriptionsDbContext(options, ThrowingConnectionFactory.Instance);
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
