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
        Guid creditorPlanId;
        Guid plainPlanId;
        await using(var context = NewContext()) {
            var creditorPlan = CreateCreditorPlan(new DateOnly(2026, 1, 10), "Rent");
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

    [Fact]
    public async Task Handle_projects_the_paid_count_and_the_earliest_unpaid_non_reversed_due_cycle() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", 15, cancellationToken);
        Guid planId;
        await using(var context = NewContext()) {
            var plan = CreateMultiInstallmentPlan(cardId, 15, new DateOnly(2026, 1, 10), "Laptop", 4);
            planId = plan.Id;
            var ordered = plan.Installments.OrderBy(installment => installment.Sequence).ToList();
            ordered[0].MarkPaid(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
            ordered[1].MarkReversed();
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new ListRecentPurchasesHandler(readContext).HandleAsync(new ListRecentPurchasesQuery(), cancellationToken);
        var row = response.Rows.Single(row => row.PlanId == planId);
        Assert.Equal(1, row.PaidInstallmentCount);
        // Cuota 1 paid, cuota 2 reversed -> cuota 3 is the next: cycle Mar 2026, DueCycle Apr 2026.
        Assert.Equal(2026, row.NextDueYear);
        Assert.Equal(4, row.NextDueMonth);
    }

    [Fact]
    public async Task Handle_skips_a_reversed_earliest_installment_when_picking_the_next_due_cycle() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", 15, cancellationToken);
        Guid planId;
        await using(var context = NewContext()) {
            var plan = CreateMultiInstallmentPlan(cardId, 15, new DateOnly(2026, 1, 10), "Phone", 3);
            planId = plan.Id;
            plan.Installments.OrderBy(installment => installment.Sequence).First().MarkReversed();
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new ListRecentPurchasesHandler(readContext).HandleAsync(new ListRecentPurchasesQuery(), cancellationToken);
        var row = response.Rows.Single(row => row.PlanId == planId);
        Assert.Equal(0, row.PaidInstallmentCount);
        // Cuota 1 reversed -> cuota 2 is the next: cycle Feb 2026, DueCycle Mar 2026.
        Assert.Equal(2026, row.NextDueYear);
        Assert.Equal(3, row.NextDueMonth);
    }

    [Fact]
    public async Task Handle_returns_a_null_next_due_cycle_when_every_installment_is_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", 15, cancellationToken);
        Guid planId;
        await using(var context = NewContext()) {
            var plan = CreateMultiInstallmentPlan(cardId, 15, new DateOnly(2026, 1, 10), "Sofa", 3);
            planId = plan.Id;
            foreach(var installment in plan.Installments) {
                installment.MarkPaid(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
            }
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new ListRecentPurchasesHandler(readContext).HandleAsync(new ListRecentPurchasesQuery(), cancellationToken);
        var row = response.Rows.Single(row => row.PlanId == planId);
        Assert.Equal(3, row.PaidInstallmentCount);
        Assert.Null(row.NextDueYear);
        Assert.Null(row.NextDueMonth);
    }

    private static PaymentPlan CreatePlan(Guid cardId, int cutoffDay, DateOnly purchaseDate, string description) {
        var total = Money.FromMinorUnits(10_000, Currency.Reference);
        return PaymentPlan.Create(
            cardId, total, 1, purchaseDate, description, cutoffDay, new PhantomPennyAllocator()
        ).Value;
    }

    private static PaymentPlan CreateMultiInstallmentPlan(Guid cardId, int cutoffDay, DateOnly purchaseDate, string description, int installmentCount) {
        var total = Money.FromMinorUnits(12_000, Currency.Reference);
        return PaymentPlan.Create(
            cardId, total, installmentCount, purchaseDate, description, cutoffDay, new PhantomPennyAllocator()
        ).Value;
    }

    private static PaymentPlan CreateCreditorPlan(DateOnly purchaseDate, string description) {
        var total = Money.FromMinorUnits(10_000, Currency.Reference);
        return PaymentPlan.Create(
            null, total, 1, purchaseDate, description, null, new PhantomPennyAllocator(),
            creditorId: Guid.CreateVersion7(), creditorAccountId: Guid.CreateVersion7()
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
