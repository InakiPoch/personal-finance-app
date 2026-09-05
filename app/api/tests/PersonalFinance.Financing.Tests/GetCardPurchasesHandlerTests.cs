using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Queries.GetCardPurchases;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class GetCardPurchasesHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public GetCardPurchasesHandlerTests() {
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
    public async Task Handle_excludes_a_plan_whose_sole_installment_is_accrued_and_the_statement_is_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(15, cancellationToken);
        await using(var context = NewContext()) {
            var plan = CreatePlan(cardId, 15, new DateOnly(2026, 1, 10), 1);
            var statement = MonthlyStatement.Open(cardId, BillingCycleCalculator.ResolveCycle(plan.PurchaseDate, 15));
            plan.Installments[0].MarkAccrued(DateTimeOffset.UtcNow, statement);
            statement.MarkPaid(DateTimeOffset.UtcNow);
            context.PaymentPlans.Add(plan);
            context.MonthlyStatements.Add(statement);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCardPurchasesHandler(readContext).HandleAsync(new GetCardPurchasesQuery(cardId), cancellationToken);
        Assert.Empty(response.Rows);
    }

    [Fact]
    public async Task Handle_includes_a_plan_whose_installment_has_not_yet_accrued() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(15, cancellationToken);
        Guid planId;
        await using(var context = NewContext()) {
            var plan = CreatePlan(cardId, 15, new DateOnly(2026, 1, 10), 1);
            planId = plan.Id;
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCardPurchasesHandler(readContext).HandleAsync(new GetCardPurchasesQuery(cardId), cancellationToken);
        Assert.Contains(response.Rows, row => row.PlanId == planId);
    }

    [Fact]
    public async Task Handle_includes_a_plan_whose_installment_is_accrued_but_the_statement_is_unpaid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(15, cancellationToken);
        Guid planId;
        await using(var context = NewContext()) {
            var plan = CreatePlan(cardId, 15, new DateOnly(2026, 1, 10), 1);
            planId = plan.Id;
            var statement = MonthlyStatement.Open(cardId, BillingCycleCalculator.ResolveCycle(plan.PurchaseDate, 15));
            plan.Installments[0].MarkAccrued(DateTimeOffset.UtcNow, statement);
            context.PaymentPlans.Add(plan);
            context.MonthlyStatements.Add(statement);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCardPurchasesHandler(readContext).HandleAsync(new GetCardPurchasesQuery(cardId), cancellationToken);
        Assert.Contains(response.Rows, row => row.PlanId == planId);
    }

    [Fact]
    public async Task Handle_groups_every_outstanding_installment_of_a_plan_into_a_single_row() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(15, cancellationToken);
        Guid planId;
        await using(var context = NewContext()) {
            var plan = CreatePlan(cardId, 15, new DateOnly(2026, 1, 10), 3);
            planId = plan.Id;
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCardPurchasesHandler(readContext).HandleAsync(new GetCardPurchasesQuery(cardId), cancellationToken);
        var row = Assert.Single(response.Rows, row => row.PlanId == planId);
        Assert.Equal(3, row.OutstandingCount);
        Assert.Equal(3, row.InstallmentCount);
    }

    [Fact]
    public async Task Handle_orders_the_current_cycle_plan_before_a_plan_from_another_cycle() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const int cutoffDay = 15;
        var cardId = await SeedCardAsync(cutoffDay, cancellationToken);
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        Guid currentCyclePlanId;
        Guid otherCyclePlanId;
        await using(var context = NewContext()) {
            var currentCyclePlan = CreatePlan(cardId, cutoffDay, today, 1);
            var otherCyclePlan = CreatePlan(cardId, cutoffDay, today.AddYears(-2), 1);
            currentCyclePlanId = currentCyclePlan.Id;
            otherCyclePlanId = otherCyclePlan.Id;
            context.PaymentPlans.AddRange(currentCyclePlan, otherCyclePlan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCardPurchasesHandler(readContext).HandleAsync(new GetCardPurchasesQuery(cardId), cancellationToken);
        var currentCycleIndex = response.Rows.ToList().FindIndex(row => row.PlanId == currentCyclePlanId);
        var otherCycleIndex = response.Rows.ToList().FindIndex(row => row.PlanId == otherCyclePlanId);
        Assert.True(currentCycleIndex >= 0 && otherCycleIndex >= 0);
        Assert.True(currentCycleIndex < otherCycleIndex);
    }

    [Fact]
    public async Task Handle_returns_no_rows_for_an_unknown_card() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = NewContext();
        var response = await new GetCardPurchasesHandler(context).HandleAsync(new GetCardPurchasesQuery(Guid.NewGuid()), cancellationToken);
        Assert.Empty(response.Rows);
    }

    private static PaymentPlan CreatePlan(Guid cardId, int cutoffDay, DateOnly purchaseDate, int installmentCount) {
        var total = Money.FromMinorUnits(10_000 * installmentCount, Currency.Reference);
        return PaymentPlan.Create(
            cardId, total, installmentCount, purchaseDate, "Test purchase", cutoffDay, new PhantomPennyAllocator()
        ).Value;
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
