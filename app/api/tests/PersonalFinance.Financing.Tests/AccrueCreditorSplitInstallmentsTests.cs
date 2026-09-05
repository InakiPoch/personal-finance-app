using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Financing.Application.Scheduling;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class AccrueCreditorSplitInstallmentsTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;
    private readonly FakeLedgerApi ledger = new();
    private readonly FakePartiesApi parties = new();
    private readonly ServiceProvider provider;

    public AccrueCreditorSplitInstallmentsTests() {
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
        services.AddLogging();
        provider = services.BuildServiceProvider();
    }

    public void Dispose() {
        provider.Dispose();
        connection.Dispose();
    }

    [Fact]
    public async Task Tick_accrues_the_due_installments_of_a_card_less_split_plan() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var splitReferenceId = Guid.CreateVersion7();
        var payableAccountId = Guid.CreateVersion7();
        var receivableA = Guid.CreateVersion7();
        var receivableB = Guid.CreateVersion7();
        var plan = pastDueSplitPlan(splitReferenceId, payableAccountId, receivableA, receivableB);
        await Persist(plan, cancellationToken);

        await Tick();

        Assert.Equal(3, ledger.PostedTransactions.Count);
        Assert.All(ledger.PostedTransactions, posted => {
            Assert.Equal(splitReferenceId, posted.SplitReferenceId);
            Assert.NotNull(posted.InstallmentReferenceId);
            Assert.Equal("Creditor-financed split accrual", posted.Description);
            var debits = posted.Lines.Where(line => line.Direction == DebitOrCredit.Debit).ToList();
            Assert.Equal(
                new[] { receivableA, receivableB }.OrderBy(id => id),
                debits.Select(line => line.AccountId).OrderBy(id => id)
            );
            Assert.All(debits, line => Assert.Equal(1000, line.Amount.MinorUnits));
            var credit = Assert.Single(posted.Lines, line => line.Direction == DebitOrCredit.Credit);
            Assert.Equal(payableAccountId, credit.AccountId);
            Assert.Equal(2000, credit.Amount.MinorUnits);
        });
        Assert.Equal(3, parties.RecordedAccruals.Count);
        Assert.All(parties.RecordedAccruals, accrual => {
            Assert.Equal(splitReferenceId, accrual.SplitReferenceId);
            Assert.Equal(2000, accrual.AccruedReceivableMinorUnits);
        });
        await using var verifyContext = NewContext();
        var accrued = await verifyContext.Set<Installment>().CountAsync(installment => installment.AccruedOnUtc != null, cancellationToken);
        Assert.Equal(3, accrued);
    }

    [Fact]
    public async Task Tick_is_idempotent_on_a_second_run() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var plan = pastDueSplitPlan(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        await Persist(plan, cancellationToken);

        await Tick();
        await Tick();

        Assert.Equal(3, ledger.PostedTransactions.Count);
        Assert.Equal(3, parties.RecordedAccruals.Count);
    }

    [Fact]
    public async Task Tick_ignores_a_non_split_creditor_plan_and_a_not_yet_due_split_plan() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var plainCreditorPlan = PaymentPlan.Create(
            cardId: null,
            total: money(9000),
            installmentCount: 3,
            purchaseDate: new DateOnly(2026, 1, 10),
            description: "Plain creditor plan",
            cutoffDay: null,
            allocator: new PhantomPennyAllocator(),
            creditorId: Guid.CreateVersion7(),
            creditorAccountId: Guid.CreateVersion7()
        ).Value;
        var futurePartyId = Guid.CreateVersion7();
        var futureSplitPlan = PaymentPlan.Create(
            cardId: null,
            total: money(9000),
            installmentCount: 3,
            purchaseDate: new DateOnly(2999, 1, 10),
            description: "Future split plan",
            cutoffDay: null,
            allocator: new PhantomPennyAllocator(),
            splitParticipants: [(futurePartyId, 1L)],
            creditorId: Guid.CreateVersion7(),
            creditorAccountId: Guid.CreateVersion7()
        ).Value;
        futureSplitPlan.LinkSplit(Guid.CreateVersion7(), new Dictionary<Guid, Guid> { [futurePartyId] = Guid.CreateVersion7() });
        futureSplitPlan.AssignCreditorPayableAccount(Guid.CreateVersion7());
        await Persist(plainCreditorPlan, cancellationToken);
        await Persist(futureSplitPlan, cancellationToken);

        await Tick();

        Assert.Empty(ledger.PostedTransactions);
        Assert.Empty(parties.RecordedAccruals);
        await using var verifyContext = NewContext();
        var accrued = await verifyContext.Set<Installment>().CountAsync(installment => installment.AccruedOnUtc != null, cancellationToken);
        Assert.Equal(0, accrued);
    }

    private static PaymentPlan pastDueSplitPlan(Guid splitReferenceId, Guid payableAccountId, Guid receivableA, Guid receivableB) {
        var partyA = Guid.CreateVersion7();
        var partyB = Guid.CreateVersion7();
        var plan = PaymentPlan.Create(
            cardId: null,
            total: money(9000),
            installmentCount: 3,
            purchaseDate: new DateOnly(2026, 1, 10),
            description: "Shared laptop",
            cutoffDay: null,
            allocator: new PhantomPennyAllocator(),
            splitParticipants: [(partyA, 1L), (partyB, 1L)],
            creditorId: Guid.CreateVersion7(),
            creditorAccountId: Guid.CreateVersion7()
        ).Value;
        plan.LinkSplit(splitReferenceId, new Dictionary<Guid, Guid> { [partyA] = receivableA, [partyB] = receivableB });
        plan.AssignCreditorPayableAccount(payableAccountId);
        return plan;
    }

    private static Money money(long minorUnits) {
        return Money.FromMinorUnits(minorUnits, Currency.Reference);
    }

    private async Task Persist(PaymentPlan plan, CancellationToken cancellationToken) {
        await using var context = NewContext();
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task Tick() {
        var scheduler = new AccrueCreditorSplitInstallments(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AccrueCreditorSplitInstallments>>()
        );
        var tickAsync = typeof(AccrueCreditorSplitInstallments)
            .GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)tickAsync.Invoke(scheduler, [CancellationToken.None])!;
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
