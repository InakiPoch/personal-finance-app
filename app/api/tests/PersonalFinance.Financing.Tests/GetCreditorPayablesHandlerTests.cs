using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.CreateCreditor;
using PersonalFinance.Financing.Application.Queries.GetCreditorPayables;
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

public sealed class GetCreditorPayablesHandlerTests : IDisposable {
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public GetCreditorPayablesHandlerTests() {
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
    public async Task Handle_groups_by_creditor_and_sums_outstanding_across_two_creditors() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var alpha = await SeedCreditorAsync("Alpha", ["Alpha Bank"], cancellationToken);
        var beta = await SeedCreditorAsync("Beta", ["Beta Bank"], cancellationToken);
        await using(var context = NewContext()) {
            context.PaymentPlans.Add(CreateCreditorPlan(alpha.CreditorId, alpha.AccountIds[0], new DateOnly(2026, 1, 10), 3, 30_000));
            context.PaymentPlans.Add(CreateCreditorPlan(alpha.CreditorId, alpha.AccountIds[0], new DateOnly(2026, 1, 10), 2, 12_000));
            context.PaymentPlans.Add(CreateCreditorPlan(beta.CreditorId, beta.AccountIds[0], new DateOnly(2026, 1, 10), 3, 9_000));
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCreditorPayablesHandler(readContext, new FixedTimeProvider(fixedNow)).HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        Assert.Equal(2, response.Rows.Count);
        Assert.Equal("Alpha", response.Rows[0].CreditorName);
        Assert.Equal(42_000, response.Rows[0].TotalOwedMinorUnits);
        Assert.Equal("Beta", response.Rows[1].CreditorName);
        Assert.Equal(9_000, response.Rows[1].TotalOwedMinorUnits);
    }

    [Fact]
    public async Task Handle_excludes_plans_without_a_creditor() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var solo = await SeedCreditorAsync("Solo", ["Solo Bank"], cancellationToken);
        await using(var context = NewContext()) {
            context.PaymentPlans.Add(CreateCreditorPlan(solo.CreditorId, solo.AccountIds[0], new DateOnly(2026, 1, 10), 3, 15_000));
            context.PaymentPlans.Add(CreateCardPlan(new DateOnly(2026, 1, 10), 1, 77_777));
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCreditorPayablesHandler(readContext, new FixedTimeProvider(fixedNow)).HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal("Solo", row.CreditorName);
        Assert.Equal(15_000, row.TotalOwedMinorUnits);
        Assert.DoesNotContain(response.Rows, candidate => candidate.TotalOwedMinorUnits == 77_777);
    }

    [Fact]
    public async Task Handle_reports_the_earliest_installment_owed_date_as_next_due_date() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Cronos", ["Cronos Bank"], cancellationToken);
        await using(var context = NewContext()) {
            context.PaymentPlans.Add(CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 1, 10), 3, 30_000));
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCreditorPayablesHandler(readContext, new FixedTimeProvider(fixedNow)).HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(new DateOnly(2026, 2, 10), row.NextDueDate);
    }

    [Fact]
    public async Task Handle_breaks_the_outstanding_total_down_by_creditor_account() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Multi", ["AA Bank", "ZZ Bank"], cancellationToken);
        var aaAccountId = creditor.Row.Accounts.Single(account => account.Label == "AA Bank").Id;
        var zzAccountId = creditor.Row.Accounts.Single(account => account.Label == "ZZ Bank").Id;
        await using(var context = NewContext()) {
            context.PaymentPlans.Add(CreateCreditorPlan(creditor.CreditorId, aaAccountId, new DateOnly(2026, 1, 10), 2, 20_000));
            context.PaymentPlans.Add(CreateCreditorPlan(creditor.CreditorId, zzAccountId, new DateOnly(2026, 1, 10), 3, 30_000));
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCreditorPayablesHandler(readContext, new FixedTimeProvider(fixedNow)).HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(50_000, row.TotalOwedMinorUnits);
        Assert.Equal(2, row.Accounts.Count);
        Assert.Equal("AA Bank", row.Accounts[0].Label);
        Assert.Equal(20_000, row.Accounts[0].OutstandingMinorUnits);
        Assert.Equal("ZZ Bank", row.Accounts[1].Label);
        Assert.Equal(30_000, row.Accounts[1].OutstandingMinorUnits);
    }

    [Fact]
    public async Task Handle_returns_no_rows_when_there_are_no_creditor_plans() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using(var context = NewContext()) {
            context.PaymentPlans.Add(CreateCardPlan(new DateOnly(2026, 1, 10), 3, 30_000));
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCreditorPayablesHandler(readContext, new FixedTimeProvider(fixedNow)).HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        Assert.Empty(response.Rows);
    }

    [Fact]
    public async Task Handle_folds_overdue_unpaid_installments_into_due_now() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Carlos", ["Carlos Bank"], cancellationToken);
        await using(var context = NewContext()) {
            // Purchase Jan 10 -> stored cycles Jan/Feb/Mar/Apr 2026 -> DueCycles Feb/Mar/Apr/May 2026.
            context.PaymentPlans.Add(CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 1, 10), 4, 40_000));
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        // Clock Mar 15 -> current creditor DueCycle is Apr 2026: cuotas due Feb/Mar (overdue) + Apr (current) all fold in; May is future.
        var handler = new GetCreditorPayablesHandler(readContext, new FixedTimeProvider(new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)));
        var response = await handler.HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(30_000, row.DueNowMinorUnits);
        Assert.Equal(40_000, row.TotalOwedMinorUnits);
    }

    [Fact]
    public async Task Handle_excludes_future_installments_from_due_now_but_counts_them_in_total_owed() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Diana", ["Diana Bank"], cancellationToken);
        await using(var context = NewContext()) {
            // Purchase Jan 10 -> stored cycles Jan/Feb 2026 -> DueCycles Feb/Mar 2026.
            context.PaymentPlans.Add(CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 1, 10), 2, 20_000));
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        // Clock Jan 15 -> current creditor DueCycle is Feb 2026: only cuota 1 (DueCycle Feb) is due now; cuota 2 (DueCycle Mar) is future.
        var handler = new GetCreditorPayablesHandler(readContext, new FixedTimeProvider(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero)));
        var response = await handler.HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(10_000, row.DueNowMinorUnits);
        Assert.Equal(20_000, row.TotalOwedMinorUnits);
    }

    [Fact]
    public async Task Handle_excludes_paid_installments_from_both_figures() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Elena", ["Elena Bank"], cancellationToken);
        await using(var context = NewContext()) {
            var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 1, 10), 3, 30_000);
            var paidInstallment = plan.Installments.OrderBy(installment => installment.Sequence).First();
            paidInstallment.ApplyPayment(paidInstallment.Amount.MinorUnits, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        // fixedNow (Jun 15 2026) puts every DueCycle in the past, so only the paid stamp can drop cuota 1 out.
        var response = await new GetCreditorPayablesHandler(readContext, new FixedTimeProvider(fixedNow)).HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(20_000, row.DueNowMinorUnits);
        Assert.Equal(20_000, row.TotalOwedMinorUnits);
    }

    [Fact]
    public async Task Handle_reflects_a_partial_payment_in_due_now_and_total_owed() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Felipe", ["Felipe Bank"], cancellationToken);
        await using(var context = NewContext()) {
            var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 1, 10), 2, 20_000);
            var partiallyPaid = plan.Installments.OrderBy(installment => installment.Sequence).First();
            partiallyPaid.ApplyPayment(4_000, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        // fixedNow (Jun 15 2026) puts every DueCycle in the past, so both cuotas are due now.
        var response = await new GetCreditorPayablesHandler(readContext, new FixedTimeProvider(fixedNow)).HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(16_000, row.DueNowMinorUnits);
        Assert.Equal(16_000, row.TotalOwedMinorUnits);
    }

    [Fact]
    public async Task Handle_excludes_paid_amounts_from_the_per_account_breakdown() {
        // D14 regression: a partly paid account must not report its paid portion as still outstanding.
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Greta", ["AA Bank", "ZZ Bank"], cancellationToken);
        var aaAccountId = creditor.Row.Accounts.Single(account => account.Label == "AA Bank").Id;
        var zzAccountId = creditor.Row.Accounts.Single(account => account.Label == "ZZ Bank").Id;
        await using(var context = NewContext()) {
            var aaPlan = CreateCreditorPlan(creditor.CreditorId, aaAccountId, new DateOnly(2026, 1, 10), 2, 20_000);
            var paidInstallment = aaPlan.Installments.OrderBy(installment => installment.Sequence).First();
            paidInstallment.ApplyPayment(paidInstallment.Amount.MinorUnits, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
            context.PaymentPlans.Add(aaPlan);
            context.PaymentPlans.Add(CreateCreditorPlan(creditor.CreditorId, zzAccountId, new DateOnly(2026, 1, 10), 1, 30_000));
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCreditorPayablesHandler(readContext, new FixedTimeProvider(fixedNow)).HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        var row = Assert.Single(response.Rows);
        var aaBreakdown = row.Accounts.Single(account => account.Label == "AA Bank");
        Assert.Equal(10_000, aaBreakdown.OutstandingMinorUnits);
        var zzBreakdown = row.Accounts.Single(account => account.Label == "ZZ Bank");
        Assert.Equal(30_000, zzBreakdown.OutstandingMinorUnits);
    }

    [Fact]
    public async Task Handle_reports_next_due_date_skipping_a_fully_paid_earliest_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Hector", ["Hector Bank"], cancellationToken);
        await using(var context = NewContext()) {
            var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 1, 10), 3, 30_000);
            var earliest = plan.Installments.OrderBy(installment => installment.Sequence).First();
            earliest.ApplyPayment(earliest.Amount.MinorUnits, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new GetCreditorPayablesHandler(readContext, new FixedTimeProvider(fixedNow)).HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        var row = Assert.Single(response.Rows);
        // Cuota 1 (owed Feb 10) is fully paid, so the next due date is cuota 2's, Mar 10.
        Assert.Equal(new DateOnly(2026, 3, 10), row.NextDueDate);
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

    private async Task<(Guid CreditorId, IReadOnlyList<Guid> AccountIds, CreditorRow Row)> SeedCreditorAsync(string name, string[] accountLabels, CancellationToken cancellationToken) {
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
