using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalFinance.Financing.Application.Scheduling;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class AccrueInstallmentsTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;
    private readonly FakeLedgerApi ledger = new();
    private readonly FakePartiesApi parties = new();
    private readonly ServiceProvider provider;

    public AccrueInstallmentsTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<FinancingDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = NewContext();
        context.Database.EnsureCreated();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddScoped(serviceProvider => new FinancingDbContext(
            serviceProvider.GetRequiredService<DbContextOptions<FinancingDbContext>>(), ThrowingConnectionFactory.Instance));
        services.AddSingleton<ILedgerApi>(ledger);
        services.AddSingleton<IPartiesApi>(parties);
        services.AddSingleton<IIntegrationEventDispatcher>(new NoOpIntegrationEventDispatcher());
        services.AddLogging();
        provider = services.BuildServiceProvider();
    }

    public void Dispose() {
        provider.Dispose();
        connection.Dispose();
    }

    [Fact]
    public async Task Gate1_posts_the_full_card_legs_for_a_split_installment_and_waits_on_the_split_receivable() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var receivableAccountId = Guid.CreateVersion7();
        var (cardId, planId) = await SeedCardSplitPlanAsync(15, new DateOnly(2026, 1, 10), 3, 9_000, partyId, receivableAccountId, cancellationToken);
        // January cycle has closed (day 20 > cutoff 15); its due cycle (February) has not begun.
        await TickAsync(new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero));
        var posted = Assert.Single(ledger.PostedTransactions);
        var card = await LoadCardAsync(cardId, cancellationToken);
        var debit = Assert.Single(posted.Lines, line => line.Direction == DebitOrCredit.Debit);
        var credit = Assert.Single(posted.Lines, line => line.Direction == DebitOrCredit.Credit);
        Assert.Equal(card.ExpenseAccountId, debit.AccountId);
        Assert.Equal(3_000, debit.Amount.MinorUnits);
        Assert.Equal(card.LiabilityAccountId, credit.AccountId);
        Assert.Equal(3_000, credit.Amount.MinorUnits);
        Assert.DoesNotContain(posted.Lines, line => line.AccountId == receivableAccountId);
        Assert.Empty(parties.RecordedAccruals);

        await using var verify = NewContext();
        var first = await FirstInstallmentAsync(verify, planId, cancellationToken);
        Assert.NotNull(first.AccruedOnUtc);
        Assert.Null(first.SplitAccruedOnUtc);
        var statement = await verify.MonthlyStatements.SingleAsync(cancellationToken);
        Assert.Equal(1, statement.CycleMonth);
        Assert.Equal(3_000, statement.AmountDue.MinorUnits);
    }

    [Fact]
    public async Task Gate2_reclassifies_the_split_share_into_the_receivable_once_the_due_cycle_begins() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var receivableAccountId = Guid.CreateVersion7();
        var (cardId, planId) = await SeedCardSplitPlanAsync(15, new DateOnly(2026, 1, 10), 3, 9_000, partyId, receivableAccountId, cancellationToken);

        await TickAsync(new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero));
        await TickAsync(new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, ledger.PostedTransactions.Count);
        var reclassification = ledger.PostedTransactions[1];
        var card = await LoadCardAsync(cardId, cancellationToken);
        var debit = Assert.Single(reclassification.Lines, line => line.Direction == DebitOrCredit.Debit);
        var credit = Assert.Single(reclassification.Lines, line => line.Direction == DebitOrCredit.Credit);
        Assert.Equal(receivableAccountId, debit.AccountId);
        Assert.Equal(1_500, debit.Amount.MinorUnits);
        Assert.Equal(card.ExpenseAccountId, credit.AccountId);
        Assert.Equal(1_500, credit.Amount.MinorUnits);
        Assert.Null(reclassification.InstallmentReferenceId);
        Assert.Equal("Split receivable — due month", reclassification.Description);

        var recorded = Assert.Single(parties.RecordedAccruals);
        Assert.Equal(1_500, recorded.AccruedReceivableMinorUnits);

        await using var verify = NewContext();
        var first = await FirstInstallmentAsync(verify, planId, cancellationToken);
        Assert.NotNull(first.SplitAccruedOnUtc);
    }

    [Fact]
    public async Task Gate2_is_idempotent_once_the_split_receivable_has_posted() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        await SeedCardSplitPlanAsync(15, new DateOnly(2026, 1, 10), 3, 9_000, partyId, Guid.CreateVersion7(), cancellationToken);

        await TickAsync(new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero));
        await TickAsync(new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero));
        await TickAsync(new DateTimeOffset(2026, 2, 6, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, ledger.PostedTransactions.Count);
        Assert.Single(parties.RecordedAccruals);
    }

    [Fact]
    public async Task Gate1_accrues_a_cycle_that_closed_weeks_ago_without_stranding_the_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var (_, planId) = await SeedCardSplitPlanAsync(15, new DateOnly(2026, 1, 10), 3, 9_000, partyId, Guid.CreateVersion7(), cancellationToken);

        await TickAsync(new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero));

        Assert.Single(ledger.PostedTransactions);
        await using var verify = NewContext();
        var statement = await verify.MonthlyStatements.SingleAsync(cancellationToken);
        Assert.Equal(1, statement.CycleMonth);
        Assert.Equal(3_000, statement.AmountDue.MinorUnits);
        var first = await FirstInstallmentAsync(verify, planId, cancellationToken);
        Assert.NotNull(first.AccruedOnUtc);
    }

    [Fact]
    public async Task Creditor_split_posts_nothing_before_the_due_cycle_begins() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var (planId, _, _) = await SeedCreditorSplitPlanAsync(new DateOnly(2026, 1, 10), 3, 9_000, partyId, cancellationToken);

        // Installment 1 is stored on the January cycle; its due cycle (February) has not begun.
        await TickAsync(new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero));

        Assert.Empty(ledger.PostedTransactions);
        Assert.Empty(parties.RecordedAccruals);
        await using var verify = NewContext();
        var first = await FirstInstallmentAsync(verify, planId, cancellationToken);
        Assert.Null(first.AccruedOnUtc);
        Assert.Null(first.SplitAccruedOnUtc);
    }

    [Fact]
    public async Task Creditor_split_accrues_the_co_borrower_share_against_the_creditor_payable_when_the_due_cycle_begins() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var (planId, receivableAccountId, payableAccountId) =
            await SeedCreditorSplitPlanAsync(new DateOnly(2026, 1, 10), 3, 9_000, partyId, cancellationToken);

        await TickAsync(new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero));
        await TickAsync(new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero));

        var posted = Assert.Single(ledger.PostedTransactions);
        var debit = Assert.Single(posted.Lines, line => line.Direction == DebitOrCredit.Debit);
        var credit = Assert.Single(posted.Lines, line => line.Direction == DebitOrCredit.Credit);
        Assert.Equal(receivableAccountId, debit.AccountId);
        Assert.Equal(1_500, debit.Amount.MinorUnits);
        Assert.Equal(payableAccountId, credit.AccountId);
        Assert.Equal(1_500, credit.Amount.MinorUnits);
        Assert.Equal("Creditor-financed split accrual", posted.Description);
        Assert.Null(posted.InstallmentReferenceId);

        var recorded = Assert.Single(parties.RecordedAccruals);
        Assert.Equal(1_500, recorded.AccruedReceivableMinorUnits);

        await using var verify = NewContext();
        var first = await FirstInstallmentAsync(verify, planId, cancellationToken);
        Assert.Null(first.AccruedOnUtc);
        Assert.NotNull(first.SplitAccruedOnUtc);
    }

    [Fact]
    public async Task Creditor_split_is_idempotent_once_the_receivable_has_posted() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        await SeedCreditorSplitPlanAsync(new DateOnly(2026, 1, 10), 3, 9_000, partyId, cancellationToken);

        await TickAsync(new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero));
        await TickAsync(new DateTimeOffset(2026, 2, 6, 0, 0, 0, TimeSpan.Zero));

        Assert.Single(ledger.PostedTransactions);
        Assert.Single(parties.RecordedAccruals);
    }

    [Fact]
    public async Task Creditor_split_accrues_each_installment_as_its_own_due_cycle_arrives() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var (planId, _, _) = await SeedCreditorSplitPlanAsync(new DateOnly(2026, 1, 10), 3, 9_000, partyId, cancellationToken);

        await TickAsync(new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero));
        await TickAsync(new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero));
        await TickAsync(new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(3, ledger.PostedTransactions.Count);
        Assert.Equal(3, parties.RecordedAccruals.Count);
        Assert.Equal(4_500, parties.RecordedAccruals.Sum(accrual => accrual.AccruedReceivableMinorUnits));
        await using var verify = NewContext();
        var installments = await verify.Set<Installment>()
            .Where(installment => installment.PaymentPlanId == planId)
            .ToListAsync(cancellationToken);
        Assert.All(installments, installment => Assert.NotNull(installment.SplitAccruedOnUtc));
    }

    private async Task<(Guid PlanId, Guid ReceivableAccountId, Guid PayableAccountId)> SeedCreditorSplitPlanAsync(
        DateOnly purchaseDate, int installmentCount, long totalMinorUnits, Guid partyId, CancellationToken cancellationToken) {
        var receivableAccountId = Guid.CreateVersion7();
        var payableAccountId = Guid.CreateVersion7();
        var splitReferenceId = Guid.CreateVersion7();
        await using var context = NewContext();
        var plan = PaymentPlan.Create(
            cardId: null,
            Money.FromMinorUnits(totalMinorUnits, Currency.Reference),
            installmentCount,
            purchaseDate,
            "Creditor split purchase",
            cutoffDay: null,
            new PhantomPennyAllocator(),
            [(partyId, 1L)],
            creditorId: Guid.CreateVersion7(),
            creditorAccountId: Guid.CreateVersion7()
        ).Value;
        plan.LinkSplit(splitReferenceId, new Dictionary<Guid, Guid> { [partyId] = receivableAccountId });
        plan.AssignCreditorPayableAccount(payableAccountId);
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return (plan.Id, receivableAccountId, payableAccountId);
    }

    private async Task<(Guid CardId, Guid PlanId)> SeedCardSplitPlanAsync(
        int cutoffDay, DateOnly purchaseDate, int installmentCount, long totalMinorUnits,
        Guid partyId, Guid receivableAccountId, CancellationToken cancellationToken) {
        var cardId = Guid.CreateVersion7();
        var splitReferenceId = Guid.CreateVersion7();
        await using var context = NewContext();
        var card = CreditCard.Create(cardId, "Visa", cutoffDay, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()).Value;
        context.CreditCards.Add(card);
        var plan = PaymentPlan.Create(
            cardId,
            Money.FromMinorUnits(totalMinorUnits, Currency.Reference),
            installmentCount,
            purchaseDate,
            "Split purchase",
            cutoffDay,
            new PhantomPennyAllocator(),
            [(partyId, 1L)]
        ).Value;
        plan.LinkSplit(splitReferenceId, new Dictionary<Guid, Guid> { [partyId] = receivableAccountId });
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return (cardId, plan.Id);
    }

    private async Task<CreditCard> LoadCardAsync(Guid cardId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await context.CreditCards.SingleAsync(card => card.Id == cardId, cancellationToken);
    }

    private static async Task<Installment> FirstInstallmentAsync(FinancingDbContext context, Guid planId, CancellationToken cancellationToken) {
        return await context.Set<Installment>()
            .Where(installment => installment.PaymentPlanId == planId)
            .OrderBy(installment => installment.Sequence)
            .FirstAsync(cancellationToken);
    }

    private async Task TickAsync(DateTimeOffset now) {
        var scheduler = new AccrueInstallments(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<AccrueInstallments>>(),
            new FixedTimeProvider(now)
        );
        var tickAsync = typeof(AccrueInstallments)
            .GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)tickAsync.Invoke(scheduler, [CancellationToken.None])!;
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
