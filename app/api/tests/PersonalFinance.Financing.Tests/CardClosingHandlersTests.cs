using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalFinance.Financing.Application.Commands.ChangeCardUsualClosingDay;
using PersonalFinance.Financing.Application.Commands.ClearCardClosingDay;
using PersonalFinance.Financing.Application.Commands.CreatePaymentPlan;
using PersonalFinance.Financing.Application.Commands.SetCardClosingDay;
using PersonalFinance.Financing.Application.Queries.GetCardClosingSchedule;
using PersonalFinance.Financing.Application.Queries.ListCreditCards;
using PersonalFinance.Financing.Application.Queries.Shared;
using PersonalFinance.Financing.Application.Scheduling;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Financing.Infrastructure.Persistence.Outbox;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class CardClosingHandlersTests : IDisposable {
    private static readonly BillingCycle october = new(2026, 10);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;
    private readonly FakeLedgerApi ledger = new();

    public CardClosingHandlersTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<FinancingDbContext>().UseSqlite(connection).Options;
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() {
        connection.Dispose();
    }

    [Fact]
    public async Task Earlier_override_rebuckets_the_open_plan_and_persists_the_override() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, planId) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);
        var result = await setAsync(cardId, 2026, 10, 24, cancellationToken);
        Assert.True(result.IsSuccess);
        await assertCyclesAsync(planId, cancellationToken, (2026, 11), (2026, 12), (2027, 1));
        Assert.Equal(24, (await LoadCardAsync(cardId, cancellationToken)).ClosingDayOf(october));
    }

    [Fact]
    public async Task Later_override_shifts_the_plan_back() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, planId) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);
        await setAsync(cardId, 2026, 10, 24, cancellationToken);
        var result = await setAsync(cardId, 2026, 10, 30, cancellationToken);
        Assert.True(result.IsSuccess);
        await assertCyclesAsync(planId, cancellationToken, (2026, 10), (2026, 11), (2026, 12));
    }

    [Fact]
    public async Task Clearing_an_override_falls_back_to_the_usual_day_and_rebuckets() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, planId) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);
        await setAsync(cardId, 2026, 10, 24, cancellationToken);
        await using(var context = NewContext()) {
            var result = await new ClearCardClosingDayHandler(context).HandleAsync(new ClearCardClosingDayCommand(cardId, 2026, 10), cancellationToken);
            Assert.True(result.IsSuccess);
        }
        await assertCyclesAsync(planId, cancellationToken, (2026, 10), (2026, 11), (2026, 12));
        var card = await LoadCardAsync(cardId, cancellationToken);
        Assert.False(card.HasOverrideFor(october));
        Assert.Equal(28, card.ClosingDayOf(october));
    }

    [Fact]
    public async Task Usual_day_change_rebuckets_plans_and_is_not_lock_checked() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, planId) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);
        await SeedStatementAsync(cardId, october, cancellationToken);
        await using(var context = NewContext()) {
            var result = await new ChangeCardUsualClosingDayHandler(context).HandleAsync(new ChangeCardUsualClosingDayCommand(cardId, 20), cancellationToken);
            Assert.True(result.IsSuccess);
        }
        Assert.Equal(20, (await LoadCardAsync(cardId, cancellationToken)).CutoffDay);
        await assertCyclesAsync(planId, cancellationToken, (2026, 11), (2026, 12), (2027, 1));
    }

    [Fact]
    public async Task Usual_day_change_rejects_an_invalid_day() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, _) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);
        await using var context = NewContext();
        var result = await new ChangeCardUsualClosingDayHandler(context).HandleAsync(new ChangeCardUsualClosingDayCommand(cardId, 0), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.InvalidCutoffDay", result.Error.Code);
    }

    [Fact]
    public async Task Set_rejects_a_day_outside_the_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, _) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);
        var result = await setAsync(cardId, 2026, 11, 31, cancellationToken);
        Assert.Equal("Financing.InvalidClosingDay", result.Error.Code);
    }

    [Fact]
    public async Task Set_rejects_an_out_of_range_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, _) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);
        var result = await setAsync(cardId, 2026, 13, 10, cancellationToken);
        Assert.Equal("Financing.InvalidClosingDay", result.Error.Code);
    }

    [Fact]
    public async Task Charged_purchase_blocks_the_change_and_nothing_is_persisted() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, planId) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);
        // Charge installment 1 against an unrelated (September) statement so October itself is not locked.
        await ChargeFirstInstallmentAsync(cardId, planId, new BillingCycle(2026, 9), cancellationToken);

        var result = await setAsync(cardId, 2026, 10, 24, cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Financing.ClosingChangeMovesChargedPurchase", result.Error.Code);
        await assertCyclesAsync(planId, cancellationToken, (2026, 10), (2026, 11), (2026, 12));
        await using var verify = NewContext();
        Assert.Empty(await verify.Set<ClosingOverride>().ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task Charged_purchase_blocks_a_usual_day_change_and_keeps_the_old_day() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, planId) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);
        await ChargeFirstInstallmentAsync(cardId, planId, new BillingCycle(2026, 9), cancellationToken);

        await using(var context = NewContext()) {
            var result = await new ChangeCardUsualClosingDayHandler(context).HandleAsync(new ChangeCardUsualClosingDayCommand(cardId, 20), cancellationToken);
            Assert.Equal("Financing.ClosingChangeMovesChargedPurchase", result.Error.Code);
        }

        Assert.Equal(28, (await LoadCardAsync(cardId, cancellationToken)).CutoffDay);
        await assertCyclesAsync(planId, cancellationToken, (2026, 10), (2026, 11), (2026, 12));
    }

    [Fact]
    public async Task Month_with_a_statement_is_locked_for_set_and_clear() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, planId) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);
        await SeedStatementAsync(cardId, october, cancellationToken);

        var set = await setAsync(cardId, 2026, 10, 24, cancellationToken);
        await using var context = NewContext();
        var clear = await new ClearCardClosingDayHandler(context).HandleAsync(new ClearCardClosingDayCommand(cardId, 2026, 10), cancellationToken);

        Assert.Equal("Financing.ClosingMonthLocked", set.Error.Code);
        Assert.Equal("Financing.ClosingMonthLocked", clear.Error.Code);
        await assertCyclesAsync(planId, cancellationToken, (2026, 10), (2026, 11), (2026, 12));
    }

    [Fact]
    public async Task Unknown_card_returns_CardNotFound_for_every_command_and_the_query() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = Guid.CreateVersion7();
        await using var context = NewContext();

        var set = await new SetCardClosingDayHandler(context).HandleAsync(new SetCardClosingDayCommand(cardId, 2026, 10, 20), cancellationToken);
        var clear = await new ClearCardClosingDayHandler(context).HandleAsync(new ClearCardClosingDayCommand(cardId, 2026, 10), cancellationToken);
        var usual = await new ChangeCardUsualClosingDayHandler(context).HandleAsync(new ChangeCardUsualClosingDayCommand(cardId, 20), cancellationToken);
        var schedule = await new GetCardClosingScheduleHandler(context, clockAt(2026, 10, 10)).HandleAsync(new GetCardClosingScheduleQuery(cardId), cancellationToken);

        Assert.Equal("Financing.CardNotFound", set.Error.Code);
        Assert.Equal("Financing.CardNotFound", clear.Error.Code);
        Assert.Equal("Financing.CardNotFound", usual.Error.Code);
        Assert.False(schedule.Found);
        Assert.Empty(schedule.Rows);
    }

    [Fact]
    public async Task Scheduler_charges_on_the_override_date_not_the_usual_day() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, _) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 10), cancellationToken);
        await setAsync(cardId, 2026, 10, 20, cancellationToken);

        await TickAsync(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        await using(var verify = NewContext()) {
            Assert.Empty(await verify.MonthlyStatements.ToListAsync(cancellationToken));
        }

        await TickAsync(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        await using(var verify = NewContext()) {
            var statement = await verify.MonthlyStatements.SingleAsync(cancellationToken);
            Assert.Equal(10, statement.CycleMonth);
        }
    }

    [Fact]
    public async Task Scheduler_without_an_override_still_waits_for_the_usual_day() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 10), cancellationToken);

        await TickAsync(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        await using(var verify = NewContext()) {
            Assert.Empty(await verify.MonthlyStatements.ToListAsync(cancellationToken));
        }

        await TickAsync(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        await using(var verify = NewContext()) {
            Assert.Single(await verify.MonthlyStatements.ToListAsync(cancellationToken));
        }
    }

    [Fact]
    public async Task Backdated_card_plan_creation_honours_the_closing_override() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = Guid.CreateVersion7();
        await using(var seed = NewContext()) {
            var card = CreditCard.Create(cardId, "Visa", 15, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            card.SetClosingDay(new BillingCycle(2026, 6), 14);
            seed.CreditCards.Add(card);
            await seed.SaveChangesAsync(cancellationToken);
        }
        var command = new CreatePaymentPlanCommand(
            300_000, cardId, 3, new DateOnly(2026, 6, 15), Description: "Back-dated laptop", BankAccountId: Guid.CreateVersion7());
        Guid planId;
        await using(var context = NewContext()) {
            var handler = new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context), clockAt(2026, 9, 7), ledger);
            var result = await handler.HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        // June closed on the 14th, so the 15th falls into July: July, August, September.
        await assertCyclesAsync(planId, cancellationToken, (2026, 7), (2026, 8), (2026, 9));
        await using var verify = NewContext();
        var installments = (await verify.PaymentPlans.Include(plan => plan.Installments).SingleAsync(plan => plan.Id == planId, cancellationToken))
            .Installments.OrderBy(installment => installment.Sequence).ToArray();
        Assert.True(installments[0].IsAccrued);
        Assert.True(installments[1].IsAccrued);
        Assert.False(installments[2].IsAccrued);
    }

    [Fact]
    public async Task Schedule_lists_rows_from_the_first_open_month_with_override_and_lock_flags() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = Guid.CreateVersion7();
        await using(var seed = NewContext()) {
            var card = CreditCard.Create(cardId, "Visa", 28, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            card.SetClosingDay(new BillingCycle(2026, 12), 20);
            seed.CreditCards.Add(card);
            seed.MonthlyStatements.Add(MonthlyStatement.Open(cardId, october, Currency.Reference));
            seed.MonthlyStatements.Add(MonthlyStatement.Open(cardId, new BillingCycle(2026, 12), Currency.Reference));
            await seed.SaveChangesAsync(cancellationToken);
        }
        await using var context = NewContext();
        var response = await new GetCardClosingScheduleHandler(context, clockAt(2026, 10, 10)).HandleAsync(new GetCardClosingScheduleQuery(cardId, 3), cancellationToken);
        Assert.True(response.Found);
        Assert.Equal(
            [
                new CardClosingScheduleRow(2026, 11, new DateOnly(2026, 11, 28), false, false),
                new CardClosingScheduleRow(2026, 12, new DateOnly(2026, 12, 20), true, true),
                new CardClosingScheduleRow(2027, 1, new DateOnly(2027, 1, 28), false, false)
            ],
            response.Rows
        );
    }

    [Fact]
    public async Task Schedule_honours_the_months_limit() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, _) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);
        await using var context = NewContext();
        var handler = new GetCardClosingScheduleHandler(context, clockAt(2026, 10, 10));
        Assert.Equal(6, (await handler.HandleAsync(new GetCardClosingScheduleQuery(cardId), cancellationToken)).Rows.Count);
        Assert.Equal(2, (await handler.HandleAsync(new GetCardClosingScheduleQuery(cardId, 2), cancellationToken)).Rows.Count);
    }

    [Fact]
    public void FirstOpenCycle_skips_months_that_have_a_statement() {
        var today = new DateOnly(2026, 10, 10);
        Assert.Equal(october, CardClosingScheduleHelper.FirstOpenCycle(today, new HashSet<(int, int)>()));
        Assert.Equal(new BillingCycle(2026, 12), CardClosingScheduleHelper.FirstOpenCycle(today, new HashSet<(int, int)> { (2026, 10), (2026, 11) }));
        Assert.Equal(october, CardClosingScheduleHelper.FirstOpenCycle(today, new HashSet<(int, int)> { (2026, 9) }));
    }

    [Fact]
    public async Task ListCreditCards_reports_the_next_closing_date_using_overrides_and_locks() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (cardId, _) = await SeedCardWithPlanAsync(28, new DateOnly(2026, 10, 25), cancellationToken);

        Assert.Equal(new DateOnly(2026, 10, 28), await nextClosingAsync(cardId, cancellationToken));

        await setAsync(cardId, 2026, 10, 20, cancellationToken);
        Assert.Equal(new DateOnly(2026, 10, 20), await nextClosingAsync(cardId, cancellationToken));

        await SeedStatementAsync(cardId, october, cancellationToken);
        Assert.Equal(new DateOnly(2026, 11, 28), await nextClosingAsync(cardId, cancellationToken));
    }

    private async Task<DateOnly> nextClosingAsync(Guid cardId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var response = await new ListCreditCardsHandler(context, clockAt(2026, 10, 10)).HandleAsync(new ListCreditCardsQuery(), cancellationToken);
        return response.Rows.Single(row => row.CardId == cardId).NextClosingDate;
    }

    private async Task<Result> setAsync(Guid cardId, int year, int month, int day, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new SetCardClosingDayHandler(context).HandleAsync(new SetCardClosingDayCommand(cardId, year, month, day), cancellationToken);
    }

    private async Task assertCyclesAsync(Guid planId, CancellationToken cancellationToken, params (int Year, int Month)[] expected) {
        await using var context = NewContext();
        var plan = await context.PaymentPlans.Include(candidate => candidate.Installments).SingleAsync(candidate => candidate.Id == planId, cancellationToken);
        var actual = plan.Installments.OrderBy(installment => installment.Sequence).Select(installment => (installment.CycleYear, installment.CycleMonth)).ToArray();
        Assert.Equal(expected, actual);
    }

    private async Task<(Guid CardId, Guid PlanId)> SeedCardWithPlanAsync(int cutoffDay, DateOnly purchaseDate, CancellationToken cancellationToken) {
        var cardId = Guid.CreateVersion7();
        await using var context = NewContext();
        context.CreditCards.Add(CreditCard.Create(cardId, "Visa", cutoffDay, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()).Value);
        var plan = PaymentPlan.Create(cardId, Money.FromMinorUnits(9_000, Currency.Reference), 3, purchaseDate, "Purchase", cutoffDay, new PhantomPennyAllocator()).Value;
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return (cardId, plan.Id);
    }

    private async Task SeedStatementAsync(Guid cardId, BillingCycle cycle, CancellationToken cancellationToken) {
        await using var context = NewContext();
        context.MonthlyStatements.Add(MonthlyStatement.Open(cardId, cycle, Currency.Reference));
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ChargeFirstInstallmentAsync(Guid cardId, Guid planId, BillingCycle statementCycle, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var statement = MonthlyStatement.Open(cardId, statementCycle, Currency.Reference);
        context.MonthlyStatements.Add(statement);
        var plan = await context.PaymentPlans.Include(candidate => candidate.Installments).SingleAsync(candidate => candidate.Id == planId, cancellationToken);
        plan.Installments.OrderBy(installment => installment.Sequence).First().MarkAccrued(DateTimeOffset.UtcNow, statement);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<CreditCard> LoadCardAsync(Guid cardId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await context.CreditCards.Include(card => card.ClosingOverrides).SingleAsync(card => card.Id == cardId, cancellationToken);
    }

    private async Task TickAsync(DateTimeOffset now) {
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddScoped(serviceProvider => new FinancingDbContext(serviceProvider.GetRequiredService<DbContextOptions<FinancingDbContext>>(), ThrowingConnectionFactory.Instance));
        services.AddSingleton<ILedgerApi>(ledger);
        services.AddSingleton<IPartiesApi>(new FakePartiesApi());
        services.AddSingleton<IIntegrationEventDispatcher>(new NoOpIntegrationEventDispatcher());
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        var scheduler = new AccrueInstallments(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<AccrueInstallments>>(),
            new FixedTimeProvider(now)
        );
        var tickAsync = typeof(AccrueInstallments).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)tickAsync.Invoke(scheduler, [CancellationToken.None])!;
    }

    private static TimeProvider clockAt(int year, int month, int day) {
        return new FixedTimeProvider(new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero));
    }

    private FinancingDbContext NewContext() {
        return new FinancingDbContext(options, ThrowingConnectionFactory.Instance);
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
