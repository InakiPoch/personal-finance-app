using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.CreateCreditor;
using PersonalFinance.Financing.Application.Queries.GetCreditorDetail;
using PersonalFinance.Financing.Application.Queries.ListCreditors;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class GetCreditorDetailHandlerTests : IDisposable {
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public GetCreditorDetailHandlerTests() {
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
    public async Task Handle_returns_not_found_for_an_unknown_creditor() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var unknownId = Guid.NewGuid();
        await using var readContext = NewContext();
        var response = await new GetCreditorDetailHandler(readContext, new FixedTimeProvider(fixedNow))
            .HandleAsync(new GetCreditorDetailQuery(unknownId), cancellationToken);
        Assert.False(response.Found);
        Assert.Equal(unknownId, response.CreditorId);
        Assert.Empty(response.Purchases);
    }

    [Fact]
    public async Task Handle_groups_by_purchase_newest_first_with_total_and_outstanding_per_group() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Nora", ["Nora Bank"], cancellationToken);
        await using(var context = NewContext()) {
            // Older purchase: 3 cuotas / 30_000, one cuota paid -> Total 30_000, Outstanding 20_000.
            var older = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 1, 10), 3, 30_000);
            var paidInstallment = older.Installments.OrderBy(installment => installment.Sequence).First();
            paidInstallment.ApplyPayment(paidInstallment.Amount.MinorUnits, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
            context.PaymentPlans.Add(older);
            // Newer purchase: 2 cuotas / 12_000, untouched -> Total 12_000, Outstanding 12_000.
            context.PaymentPlans.Add(CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 3, 5), 2, 12_000));
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCreditorDetailHandler(readContext, new FixedTimeProvider(fixedNow))
            .HandleAsync(new GetCreditorDetailQuery(creditor.CreditorId), cancellationToken);
        Assert.True(response.Found);
        Assert.Equal("Nora", response.CreditorName);
        Assert.Equal(2, response.Purchases.Count);
        Assert.Equal(new DateOnly(2026, 3, 5), response.Purchases[0].PurchaseDate);
        Assert.Equal(12_000, response.Purchases[0].TotalMinorUnits);
        Assert.Equal(12_000, response.Purchases[0].OutstandingMinorUnits);
        Assert.Equal(2, response.Purchases[0].Installments.Count);
        Assert.Equal(new DateOnly(2026, 1, 10), response.Purchases[1].PurchaseDate);
        Assert.Equal(30_000, response.Purchases[1].TotalMinorUnits);
        Assert.Equal(20_000, response.Purchases[1].OutstandingMinorUnits);
        Assert.Equal(3, response.Purchases[1].Installments.Count);
    }

    [Fact]
    public async Task Handle_orders_installments_by_sequence_and_derives_each_status() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Omar", ["Omar Bank"], cancellationToken);
        await using(var context = NewContext()) {
            // Purchase Jan 10 -> stored cycles Jan..May 2026 -> DueCycles Feb/Mar/Apr/May/Jun 2026.
            var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 1, 10), 5, 50_000);
            var ordered = plan.Installments.OrderBy(installment => installment.Sequence).ToList();
            ordered[0].ApplyPayment(ordered[0].Amount.MinorUnits, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
            ordered[1].MarkReversed();
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        // Clock Apr 15 2026 -> ResolveCycle(Apr 15, 26) = Apr 2026 -> current creditor DueCycle is May 2026.
        var handler = new GetCreditorDetailHandler(readContext, new FixedTimeProvider(new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero)));
        var response = await handler.HandleAsync(new GetCreditorDetailQuery(creditor.CreditorId), cancellationToken);
        var purchase = Assert.Single(response.Purchases);
        var installments = purchase.Installments;
        Assert.Equal(5, installments.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, installments.Select(row => row.Sequence).ToArray());
        Assert.All(installments, row => Assert.Equal(5, row.InstallmentCount));
        Assert.Equal("paid", installments[0].Status);
        Assert.True(installments[0].IsPaid);
        Assert.Equal("reversed", installments[1].Status);
        Assert.True(installments[1].IsReversed);
        Assert.Equal("overdue", installments[2].Status);
        Assert.Equal("due", installments[3].Status);
        Assert.Equal("future", installments[4].Status);
        // DueYear/DueMonth carry the payment month (stored cycle + 1): cuota 3 -> Apr, 4 -> May, 5 -> Jun 2026.
        Assert.Equal(2026, installments[2].DueYear);
        Assert.Equal(4, installments[2].DueMonth);
        Assert.Equal(5, installments[3].DueMonth);
        Assert.Equal(6, installments[4].DueMonth);
        // Total excludes the reversed cuota; Outstanding also excludes the paid one.
        Assert.Equal(40_000, purchase.TotalMinorUnits);
        Assert.Equal(30_000, purchase.OutstandingMinorUnits);
    }

    [Fact]
    public async Task Handle_reflects_a_partial_payment_in_paid_remaining_and_outstanding() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Sasha", ["Sasha Bank"], cancellationToken);
        await using(var context = NewContext()) {
            var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 1, 10), 2, 20_000);
            var partiallyPaid = plan.Installments.OrderBy(installment => installment.Sequence).First();
            partiallyPaid.ApplyPayment(4_000, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCreditorDetailHandler(readContext, new FixedTimeProvider(fixedNow))
            .HandleAsync(new GetCreditorDetailQuery(creditor.CreditorId), cancellationToken);
        var purchase = Assert.Single(response.Purchases);
        var row = purchase.Installments.Single(candidate => candidate.Sequence == 1);
        Assert.False(row.IsPaid);
        Assert.True(row.HasPayments);
        Assert.Equal(4_000, row.PaidMinorUnits);
        Assert.Equal(6_000, row.RemainingMinorUnits);
        Assert.Equal(20_000, purchase.TotalMinorUnits);
        Assert.Equal(16_000, purchase.OutstandingMinorUnits);
    }

    [Fact]
    public async Task Handle_excludes_other_creditors_and_card_backed_plans() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var target = await SeedCreditorAsync("Paula", ["Paula Bank"], cancellationToken);
        var other = await SeedCreditorAsync("Quinn", ["Quinn Bank"], cancellationToken);
        await using(var context = NewContext()) {
            context.PaymentPlans.Add(CreateCreditorPlan(target.CreditorId, target.AccountIds[0], new DateOnly(2026, 1, 10), 2, 20_000));
            context.PaymentPlans.Add(CreateCreditorPlan(other.CreditorId, other.AccountIds[0], new DateOnly(2026, 1, 10), 3, 99_000));
            context.PaymentPlans.Add(CreateCardPlan(new DateOnly(2026, 1, 10), 1, 77_777));
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCreditorDetailHandler(readContext, new FixedTimeProvider(fixedNow))
            .HandleAsync(new GetCreditorDetailQuery(target.CreditorId), cancellationToken);
        var purchase = Assert.Single(response.Purchases);
        Assert.Equal(20_000, purchase.TotalMinorUnits);
        Assert.Equal(2, purchase.Installments.Count);
    }

    private static PaymentPlan CreateCreditorPlan(Guid creditorId, Guid creditorAccountId, DateOnly purchaseDate, int installmentCount, long totalMinorUnits) {
        var total = Money.FromMinorUnits(totalMinorUnits, Currency.Reference);
        return PaymentPlan.Create(
            cardId: null,
            total,
            installmentCount,
            purchaseDate,
            "Creditor purchase",
            cutoffDay: null,
            new PhantomPennyAllocator(),
            splitParticipants: null,
            creditorId: creditorId,
            creditorAccountId: creditorAccountId
        ).Value;
    }

    private static PaymentPlan CreateCardPlan(DateOnly purchaseDate, int installmentCount, long totalMinorUnits) {
        var total = Money.FromMinorUnits(totalMinorUnits, Currency.Reference);
        return PaymentPlan.Create(
            Guid.CreateVersion7(),
            total,
            installmentCount,
            purchaseDate,
            "Card purchase",
            15,
            new PhantomPennyAllocator()
        ).Value;
    }

    private async Task<(Guid CreditorId, IReadOnlyList<Guid> AccountIds, CreditorRow Row)> SeedCreditorAsync(
        string name, string[] accountLabels, CancellationToken cancellationToken) {
        await using(var context = NewContext()) {
            var accounts = accountLabels.Select(label => new CreditorAccountPayload(label, null)).ToList();
            await new CreateCreditorHandler(context).HandleAsync(new CreateCreditorCommand(name, accounts), cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new ListCreditorsHandler(readContext).HandleAsync(new ListCreditorsQuery(), cancellationToken);
        var row = response.Rows.Single(candidate => candidate.Name == name);
        return (row.Id, row.Accounts.Select(account => account.Id).ToList(), row);
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
