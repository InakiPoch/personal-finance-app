using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Queries.GetCardFutureSchedule;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class GetCardFutureScheduleHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public GetCardFutureScheduleHandlerTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<FinancingDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() {
        connection.Dispose();
    }

    [Fact]
    public async Task Handle_dates_each_installment_by_its_due_month_for_a_purchase_before_the_cutoff() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(15, cancellationToken);
        await PersistAsync(CardPlan(cardId, 15, new DateOnly(2026, 9, 6), 3, 9_000), cancellationToken);
        var response = await HandleAsync(cardId, cancellationToken);
        Assert.Equal(
            new[] { (2026, 10), (2026, 11), (2026, 12) },
            response.Rows.Select(row => (row.CycleYear, row.CycleMonth)).ToList()
        );
    }

    [Fact]
    public async Task Handle_dates_the_schedule_two_billing_months_out_for_a_purchase_after_the_cutoff() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(15, cancellationToken);
        await PersistAsync(CardPlan(cardId, 15, new DateOnly(2026, 9, 20), 3, 9_000), cancellationToken);
        var response = await HandleAsync(cardId, cancellationToken);
        Assert.Equal(
            new[] { (2026, 11), (2026, 12), (2027, 1) },
            response.Rows.Select(row => (row.CycleYear, row.CycleMonth)).ToList()
        );
    }

    [Fact]
    public async Task Handle_keeps_an_accrued_but_unpaid_installment_on_the_schedule_under_its_due_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(15, cancellationToken);
        var plan = CardPlan(cardId, 15, new DateOnly(2026, 9, 6), 3, 9_000);
        var statement = MonthlyStatement.Open(cardId, plan.Installments[0].Cycle);
        plan.Installments[0].MarkAccrued(DateTimeOffset.UtcNow, statement);
        await using(var context = NewContext()) {
            context.PaymentPlans.Add(plan);
            context.MonthlyStatements.Add(statement);
            await context.SaveChangesAsync(cancellationToken);
        }
        var response = await HandleAsync(cardId, cancellationToken);
        Assert.Equal(
            new[] { (2026, 10), (2026, 11), (2026, 12) },
            response.Rows.Select(row => (row.CycleYear, row.CycleMonth)).ToList()
        );
    }

    [Fact]
    public async Task Handle_drops_an_installment_once_its_statement_is_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(15, cancellationToken);
        var plan = CardPlan(cardId, 15, new DateOnly(2026, 9, 6), 3, 9_000);
        var statement = MonthlyStatement.Open(cardId, plan.Installments[0].Cycle);
        plan.Installments[0].MarkAccrued(DateTimeOffset.UtcNow, statement);
        statement.MarkPaid(DateTimeOffset.UtcNow);
        await using(var context = NewContext()) {
            context.PaymentPlans.Add(plan);
            context.MonthlyStatements.Add(statement);
            await context.SaveChangesAsync(cancellationToken);
        }
        var response = await HandleAsync(cardId, cancellationToken);
        Assert.Equal(
            new[] { (2026, 11), (2026, 12) },
            response.Rows.Select(row => (row.CycleYear, row.CycleMonth)).ToList()
        );
    }

    [Fact]
    public async Task Handle_excludes_a_reversed_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(15, cancellationToken);
        var plan = CardPlan(cardId, 15, new DateOnly(2026, 9, 6), 3, 9_000);
        plan.Installments[1].MarkReversed();
        await PersistAsync(plan, cancellationToken);
        var response = await HandleAsync(cardId, cancellationToken);
        Assert.Equal(
            new[] { (2026, 10), (2026, 12) },
            response.Rows.Select(row => (row.CycleYear, row.CycleMonth)).ToList()
        );
    }

    private static PaymentPlan CardPlan(
        Guid cardId, int cutoffDay, DateOnly purchaseDate, int installmentCount, long totalMinorUnits) {
        return PaymentPlan.Create(
            cardId,
            Money.FromMinorUnits(totalMinorUnits, Currency.Reference),
            installmentCount,
            purchaseDate,
            "Sofa",
            cutoffDay,
            new PhantomPennyAllocator()
        ).Value;
    }

    private async Task<CardFutureScheduleResponse> HandleAsync(Guid cardId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new GetCardFutureScheduleHandler(context).HandleAsync(new GetCardFutureScheduleQuery(cardId), cancellationToken);
    }

    private async Task PersistAsync(PaymentPlan plan, CancellationToken cancellationToken) {
        await using var context = NewContext();
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> SeedCardAsync(int cutoffDay, CancellationToken cancellationToken) {
        var cardId = Guid.CreateVersion7();
        await using var context = NewContext();
        var card = CreditCard.Create(cardId, "Visa", cutoffDay, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
        context.CreditCards.Add(card);
        await context.SaveChangesAsync(cancellationToken);
        return cardId;
    }

    private FinancingDbContext NewContext() {
        return new FinancingDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();

        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }
}
