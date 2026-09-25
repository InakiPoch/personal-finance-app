using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Queries.GetPendingSharesByParty;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class GetPendingSharesByPartyHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public GetPendingSharesByPartyHandlerTests() {
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
    public async Task Handle_aggregates_a_card_splits_unaccrued_shares_into_one_row_for_the_party() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var cardId = await SeedCardAsync(15, cancellationToken);
        await PersistAsync(
            CardSplitPlan(cardId, 15, new DateOnly(2026, 1, 10), 3, 9_000, (partyId, 1L)),
            cancellationToken
        );
        var response = await HandleAsync(cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(partyId, row.PartyId);
        Assert.Equal(3, row.ScheduledCount);
        Assert.Equal(4_500, row.ScheduledTotalMinorUnits);
        Assert.Equal(Currency.Reference.Code, row.CurrencyCode);
    }

    [Fact]
    public async Task Handle_includes_a_card_less_creditor_financed_split() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var (creditorId, creditorAccountId) = await SeedCreditorAsync("MercadoPago", cancellationToken);
        var plan = PaymentPlan.Create(
            cardId: null,
            Money.FromMinorUnits(9_000, Currency.Reference),
            installmentCount: 3,
            purchaseDate: new DateOnly(2026, 1, 10),
            description: "Creditor purchase",
            cutoffDay: null,
            allocator: new PhantomPennyAllocator(),
            splitParticipants: [(partyId, 1L)],
            creditorId: creditorId,
            creditorAccountId: creditorAccountId
        ).Value;
        await PersistAsync(plan, cancellationToken);
        var response = await HandleAsync(cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(partyId, row.PartyId);
        Assert.Equal(3, row.ScheduledCount);
        Assert.Equal(4_500, row.ScheduledTotalMinorUnits);
    }

    [Fact]
    public async Task Handle_counts_only_the_still_pending_installments_excluding_split_accrued_and_reversed() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var cardId = await SeedCardAsync(15, cancellationToken);
        var plan = CardSplitPlan(cardId, 15, new DateOnly(2026, 1, 10), 3, 9_000, (partyId, 1L));
        var statement = MonthlyStatement.Open(cardId, plan.Installments[0].Cycle, Currency.Reference);
        plan.Installments[0].MarkAccrued(DateTimeOffset.UtcNow, statement);
        plan.Installments[0].MarkSplitAccrued(DateTimeOffset.UtcNow);
        plan.Installments[1].MarkReversed();
        await using(var context = NewContext()) {
            context.PaymentPlans.Add(plan);
            context.MonthlyStatements.Add(statement);
            await context.SaveChangesAsync(cancellationToken);
        }
        var response = await HandleAsync(cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(1, row.ScheduledCount);
        Assert.Equal(1_500, row.ScheduledTotalMinorUnits);
    }

    [Fact]
    public async Task Handle_returns_a_row_per_party_for_a_multi_party_split() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyA = Guid.CreateVersion7();
        var partyB = Guid.CreateVersion7();
        var cardId = await SeedCardAsync(15, cancellationToken);
        await PersistAsync(
            CardSplitPlan(cardId, 15, new DateOnly(2026, 1, 10), 1, 9_000, (partyA, 1L), (partyB, 3L)),
            cancellationToken
        );
        var installmentAmount = await SingleInstallmentAmountAsync(cancellationToken);
        var orderedParties = new[] { partyA, partyB }.OrderBy(id => id).ToList();
        long[] weights = [1L, .. orderedParties.Select(id => id == partyA ? 1L : 3L)];
        var shares = new PhantomPennyAllocator().Allocate(installmentAmount, weights);
        var expectedA = shares[orderedParties.IndexOf(partyA) + 1].MinorUnits;
        var expectedB = shares[orderedParties.IndexOf(partyB) + 1].MinorUnits;
        var response = await HandleAsync(cancellationToken);
        Assert.Equal(2, response.Rows.Count);
        Assert.Equal(expectedA, Assert.Single(response.Rows, row => row.PartyId == partyA).ScheduledTotalMinorUnits);
        Assert.Equal(expectedB, Assert.Single(response.Rows, row => row.PartyId == partyB).ScheduledTotalMinorUnits);
        Assert.All(response.Rows, row => Assert.Equal(1, row.ScheduledCount));
    }

    [Fact]
    public async Task Handle_sums_a_partys_shares_across_every_split_plan_into_one_row() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var cardId = await SeedCardAsync(15, cancellationToken);
        await PersistAsync(
            CardSplitPlan(cardId, 15, new DateOnly(2026, 1, 10), 2, 6_000, (partyId, 1L)),
            cancellationToken
        );
        await PersistAsync(
            CardSplitPlan(cardId, 15, new DateOnly(2026, 2, 10), 3, 9_000, (partyId, 1L)),
            cancellationToken
        );
        var response = await HandleAsync(cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(partyId, row.PartyId);
        Assert.Equal(5, row.ScheduledCount);
        Assert.Equal(7_500, row.ScheduledTotalMinorUnits);
    }

    [Fact]
    public async Task Handle_omits_a_card_plan_with_no_split_participants() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(15, cancellationToken);
        var plan = PaymentPlan.Create(
            cardId,
            Money.FromMinorUnits(9_000, Currency.Reference),
            installmentCount: 3,
            purchaseDate: new DateOnly(2026, 1, 10),
            description: "Solo purchase",
            cutoffDay: 15,
            allocator: new PhantomPennyAllocator()
        ).Value;
        await PersistAsync(plan, cancellationToken);
        var response = await HandleAsync(cancellationToken);
        Assert.Empty(response.Rows);
    }

    private static PaymentPlan CardSplitPlan(
        Guid cardId, int cutoffDay, DateOnly purchaseDate, int installmentCount, long totalMinorUnits,
        params (Guid PartyId, long Weight)[] participants) {
        return PaymentPlan.Create(
            cardId,
            Money.FromMinorUnits(totalMinorUnits, Currency.Reference),
            installmentCount,
            purchaseDate,
            "Split purchase",
            cutoffDay,
            new PhantomPennyAllocator(),
            participants
        ).Value;
    }

    private async Task<Money> SingleInstallmentAmountAsync(CancellationToken cancellationToken) {
        await using var context = NewContext();
        var installment = await context.Set<Installment>().SingleAsync(cancellationToken);
        return installment.Amount;
    }

    private async Task<GetPendingSharesByPartyResponse> HandleAsync(CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new GetPendingSharesByPartyHandler(context).HandleAsync(new GetPendingSharesByPartyQuery(), cancellationToken);
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

    private async Task<(Guid CreditorId, Guid AccountId)> SeedCreditorAsync(string name, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var creditor = Creditor.Create(Guid.CreateVersion7(), name, [("Main", "alias.pay")]).Value;
        context.Creditors.Add(creditor);
        await context.SaveChangesAsync(cancellationToken);
        return (creditor.Id, creditor.Accounts[0].Id);
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
