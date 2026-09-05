using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Queries.ListRecentPurchases;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class ListRecentPurchasesHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public ListRecentPurchasesHandlerTests() {
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
    public async Task Handle_returns_rows_newest_first() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", 15, cancellationToken);
        Guid olderPlanId;
        Guid newerPlanId;
        await using(var context = NewContext()) {
            var older = CreatePlan(cardId, 15, new DateOnly(2026, 1, 10), "Older purchase");
            var newer = CreatePlan(cardId, 15, new DateOnly(2026, 3, 5), "Newer purchase");
            olderPlanId = older.Id;
            newerPlanId = newer.Id;
            context.PaymentPlans.AddRange(older, newer);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new ListRecentPurchasesHandler(readContext).HandleAsync(new ListRecentPurchasesQuery(), cancellationToken);
        var newerIndex = response.Rows.ToList().FindIndex(row => row.PlanId == newerPlanId);
        var olderIndex = response.Rows.ToList().FindIndex(row => row.PlanId == olderPlanId);
        Assert.True(newerIndex >= 0 && olderIndex >= 0);
        Assert.True(newerIndex < olderIndex);
    }

    [Fact]
    public async Task Handle_carries_the_plans_description_and_the_correct_card_name() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa Gold", 15, cancellationToken);
        Guid planId;
        await using(var context = NewContext()) {
            var plan = CreatePlan(cardId, 15, new DateOnly(2026, 1, 10), "Groceries");
            planId = plan.Id;
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new ListRecentPurchasesHandler(readContext).HandleAsync(new ListRecentPurchasesQuery(), cancellationToken);
        var row = response.Rows.Single(row => row.PlanId == planId);
        Assert.Equal("Groceries", row.Description);
        Assert.Equal("Visa Gold", row.CardName);
    }

    [Fact]
    public async Task Handle_flags_a_plan_as_a_creditor_payment_only_when_a_creditor_is_set() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", 15, cancellationToken);
        var creditorId = Guid.CreateVersion7();
        Guid creditorPlanId;
        Guid plainPlanId;
        await using(var context = NewContext()) {
            var creditorPlan = CreatePlan(cardId, 15, new DateOnly(2026, 1, 10), "Rent", creditorId: creditorId);
            var plainPlan = CreatePlan(cardId, 15, new DateOnly(2026, 1, 11), "Groceries");
            creditorPlanId = creditorPlan.Id;
            plainPlanId = plainPlan.Id;
            context.PaymentPlans.AddRange(creditorPlan, plainPlan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new ListRecentPurchasesHandler(readContext).HandleAsync(new ListRecentPurchasesQuery(), cancellationToken);
        Assert.True(response.Rows.Single(row => row.PlanId == creditorPlanId).IsCreditorPayment);
        Assert.False(response.Rows.Single(row => row.PlanId == plainPlanId).IsCreditorPayment);
    }

    [Fact]
    public async Task Handle_caps_the_row_count_at_the_given_limit() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", 15, cancellationToken);
        await using(var context = NewContext()) {
            context.PaymentPlans.AddRange(
                CreatePlan(cardId, 15, new DateOnly(2026, 1, 1), "First"),
                CreatePlan(cardId, 15, new DateOnly(2026, 1, 2), "Second"),
                CreatePlan(cardId, 15, new DateOnly(2026, 1, 3), "Third")
            );
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new ListRecentPurchasesHandler(readContext).HandleAsync(new ListRecentPurchasesQuery(Limit: 2), cancellationToken);
        Assert.Equal(2, response.Rows.Count);
    }

    private static PaymentPlan CreatePlan(Guid cardId, int cutoffDay, DateOnly purchaseDate, string description, Guid? creditorId = null) {
        var total = Money.FromMinorUnits(10_000, Currency.Reference);
        return PaymentPlan.Create(
            cardId, total, 1, purchaseDate, description, cutoffDay, new PhantomPennyAllocator(),
            creditorId: creditorId
        ).Value;
    }

    private async Task<Guid> SeedCardAsync(string name, int cutoffDay, CancellationToken cancellationToken) {
        var cardId = Guid.CreateVersion7();
        await using var context = NewContext();
        var card = CreditCard.Create(cardId, name, cutoffDay, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
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
