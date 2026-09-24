using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Queries.GetFuturePartyShares;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class GetFuturePartySharesHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public GetFuturePartySharesHandlerTests() {
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
    public async Task Handle_returns_one_row_per_unaccrued_installment_for_a_card_split() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var cardId = await SeedCardAsync(15, cancellationToken);
        await PersistAsync(
            CardSplitPlan(cardId, 15, new DateOnly(2026, 1, 10), 3, 9_000, (partyId, 1L)),
            cancellationToken
        );
        var response = await HandleAsync(partyId, cancellationToken);
        var expected = await ExpectedSharesAsync(partyId, [1L, 1L], cancellationToken);
        Assert.Equal(3, response.Rows.Count);
        Assert.Equal(
            expected,
            response.Rows.Select(row => (row.CycleYear, row.CycleMonth, row.ShareMinorUnits)).ToList()
        );
        Assert.All(response.Rows, row => Assert.Equal("Visa — Split purchase", row.SourceLabel));
        Assert.All(response.Rows, row => Assert.Equal(Currency.Reference.Code, row.CurrencyCode));
    }

    [Fact]
    public async Task Handle_projects_odd_cent_shares_exactly_as_accrual_would() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var cardId = await SeedCardAsync(15, cancellationToken);
        await PersistAsync(
            CardSplitPlan(cardId, 15, new DateOnly(2026, 1, 10), 3, 10_001, (partyId, 1L)),
            cancellationToken
        );
        var response = await HandleAsync(partyId, cancellationToken);
        var expected = await ExpectedSharesAsync(partyId, [1L, 1L], cancellationToken);
        Assert.Equal(
            expected,
            response.Rows.Select(row => (row.CycleYear, row.CycleMonth, row.ShareMinorUnits)).ToList()
        );
    }

    [Fact]
    public async Task Handle_dates_the_scheduled_shares_by_the_due_month_for_a_purchase_before_the_cutoff() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var cardId = await SeedCardAsync(15, cancellationToken);
        await PersistAsync(
            CardSplitPlan(cardId, 15, new DateOnly(2026, 9, 6), 3, 9_000, (partyId, 1L)),
            cancellationToken
        );
        var response = await HandleAsync(partyId, cancellationToken);
        Assert.Equal(
            new[] { (2026, 10), (2026, 11), (2026, 12) },
            response.Rows.Select(row => (row.CycleYear, row.CycleMonth)).ToList()
        );
    }

    [Fact]
    public async Task Handle_dates_the_scheduled_shares_two_billing_months_out_for_a_purchase_after_the_cutoff() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var cardId = await SeedCardAsync(15, cancellationToken);
        await PersistAsync(
            CardSplitPlan(cardId, 15, new DateOnly(2026, 9, 20), 3, 9_000, (partyId, 1L)),
            cancellationToken
        );
        var response = await HandleAsync(partyId, cancellationToken);
        Assert.Equal(
            new[] { (2026, 11), (2026, 12), (2027, 1) },
            response.Rows.Select(row => (row.CycleYear, row.CycleMonth)).ToList()
        );
    }

    [Fact]
    public async Task Handle_excludes_split_accrued_and_reversed_but_keeps_accrued_not_yet_due_installments() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var cardId = await SeedCardAsync(15, cancellationToken);
        var plan = CardSplitPlan(cardId, 15, new DateOnly(2026, 1, 10), 3, 9_000, (partyId, 1L));
        var statement = MonthlyStatement.Open(cardId, plan.Installments[0].Cycle, Currency.Reference);
        plan.Installments[0].MarkAccrued(DateTimeOffset.UtcNow, statement);
        plan.Installments[0].MarkSplitAccrued(DateTimeOffset.UtcNow);
        plan.Installments[1].MarkReversed();
        plan.Installments[2].MarkAccrued(DateTimeOffset.UtcNow, statement);
        await using(var context = NewContext()) {
            context.PaymentPlans.Add(plan);
            context.MonthlyStatements.Add(statement);
            await context.SaveChangesAsync(cancellationToken);
        }
        var response = await HandleAsync(partyId, cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(plan.Installments[2].DueCycle.Year, row.CycleYear);
        Assert.Equal(plan.Installments[2].DueCycle.Month, row.CycleMonth);
    }

    [Fact]
    public async Task Handle_includes_a_card_less_creditor_financed_split_dated_by_the_due_month() {
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
        var response = await HandleAsync(partyId, cancellationToken);
        Assert.Equal(
            new[] { (2026, 2), (2026, 3), (2026, 4) },
            response.Rows.Select(row => (row.CycleYear, row.CycleMonth)).ToList()
        );
        Assert.All(response.Rows, row => Assert.Equal("MercadoPago — Creditor purchase", row.SourceLabel));
        Assert.All(response.Rows, row => Assert.Equal(1_500, row.ShareMinorUnits));
    }

    [Fact]
    public async Task Handle_excludes_a_card_plan_with_no_split_participants() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
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
        var response = await HandleAsync(partyId, cancellationToken);
        Assert.Empty(response.Rows);
    }

    [Fact]
    public async Task Handle_returns_only_the_requested_partys_share_for_a_multi_party_split() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyA = Guid.CreateVersion7();
        var partyB = Guid.CreateVersion7();
        var cardId = await SeedCardAsync(15, cancellationToken);
        await PersistAsync(
            CardSplitPlan(cardId, 15, new DateOnly(2026, 1, 10), 1, 9_000, (partyA, 1L), (partyB, 3L)),
            cancellationToken
        );
        var orderedWeights = new[] { partyA, partyB }.OrderBy(id => id).ToList();
        long[] weights = [1L, .. orderedWeights.Select(id => id == partyA ? 1L : 3L)];
        var indexA = orderedWeights.IndexOf(partyA);
        var response = await HandleAsync(partyA, cancellationToken);
        var installmentAmount = await SingleInstallmentAmountAsync(cancellationToken);
        var expectedA = new PhantomPennyAllocator().Allocate(installmentAmount, weights)[indexA + 1].MinorUnits;
        var expectedB = new PhantomPennyAllocator().Allocate(installmentAmount, weights)[orderedWeights.IndexOf(partyB) + 1].MinorUnits;
        var row = Assert.Single(response.Rows);
        Assert.Equal(expectedA, row.ShareMinorUnits);
        Assert.NotEqual(expectedB, row.ShareMinorUnits);
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

    private async Task<IReadOnlyList<(int CycleYear, int CycleMonth, long ShareMinorUnits)>> ExpectedSharesAsync(
        Guid partyId, long[] weights, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var installments = await context.Set<Installment>()
            .Where(installment => installment.AccruedOnUtc == null && installment.IsReversed == false)
            .ToListAsync(cancellationToken);
        return installments
            .OrderBy(installment => installment.CycleYear)
            .ThenBy(installment => installment.CycleMonth)
            .Select(installment => (
                installment.DueCycle.Year,
                installment.DueCycle.Month,
                new PhantomPennyAllocator().Allocate(installment.Amount, weights)[1].MinorUnits
            ))
        .ToList();
    }

    private async Task<Money> SingleInstallmentAmountAsync(CancellationToken cancellationToken) {
        await using var context = NewContext();
        var installment = await context.Set<Installment>().SingleAsync(cancellationToken);
        return installment.Amount;
    }

    private async Task<GetFuturePartySharesResponse> HandleAsync(Guid partyId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new GetFuturePartySharesHandler(context).HandleAsync(new GetFuturePartySharesQuery(partyId), cancellationToken);
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
