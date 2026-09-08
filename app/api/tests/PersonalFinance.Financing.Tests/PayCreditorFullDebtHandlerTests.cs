using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.CreateCreditor;
using PersonalFinance.Financing.Application.Commands.PayCreditorFullDebt;
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

public sealed class PayCreditorFullDebtHandlerTests : IDisposable {
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public PayCreditorFullDebtHandlerTests() {
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
    public async Task Handle_stamps_every_unpaid_non_reversed_installment_across_purchases() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Nora", ["Nora Bank"], cancellationToken);
        var planA = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 3, 30_000, cancellationToken);
        var planB = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 2, 10), 2, 20_000, cancellationToken);

        var result = await PayFullDebtAsync(creditor.CreditorId, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
        foreach(var installmentId in planA.Concat(planB)) {
            var installment = await LoadInstallmentAsync(installmentId, cancellationToken);
            Assert.True(installment.IsPaid);
            Assert.Equal(fixedNow, installment.PaidOnUtc);
        }
        Assert.Equal((0L, 0L), await ReadPayablesFiguresAsync(creditor.CreditorId, cancellationToken));
    }

    [Fact]
    public async Task Handle_skips_reversed_and_already_paid_and_counts_only_newly_settled() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Omar", ["Omar Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(
            creditor,
            new DateOnly(2026, 1, 10),
            4,
            40_000,
            cancellationToken,
            markPaidSequence: 1,
            markReversedSequence: 2
        );
        var alreadyPaidOn = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await PayFullDebtAsync(creditor.CreditorId, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
        var alreadyPaid = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        Assert.Equal(alreadyPaidOn, alreadyPaid.PaidOnUtc);
        var reversed = await LoadInstallmentAsync(installmentIds[1], cancellationToken);
        Assert.True(reversed.IsReversed);
        Assert.False(reversed.IsPaid);
        foreach(var installmentId in installmentIds.Skip(2)) {
            var installment = await LoadInstallmentAsync(installmentId, cancellationToken);
            Assert.True(installment.IsPaid);
            Assert.Equal(fixedNow, installment.PaidOnUtc);
        }
    }

    [Fact]
    public async Task Handle_is_idempotent_second_run_settles_zero() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Paula", ["Paula Bank"], cancellationToken);
        await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 3, 30_000, cancellationToken);

        var first = await PayFullDebtAsync(creditor.CreditorId, cancellationToken);
        var second = await PayFullDebtAsync(creditor.CreditorId, cancellationToken);

        Assert.True(first.IsSuccess);
        Assert.Equal(3, first.Value);
        Assert.True(second.IsSuccess);
        Assert.Equal(0, second.Value);
        Assert.Equal((0L, 0L), await ReadPayablesFiguresAsync(creditor.CreditorId, cancellationToken));
    }

    [Fact]
    public async Task Handle_settles_zero_for_a_creditor_with_no_purchases() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Quinn", ["Quinn Bank"], cancellationToken);

        var result = await PayFullDebtAsync(creditor.CreditorId, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public async Task Handle_does_not_touch_another_creditors_installments() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var target = await SeedCreditorAsync("Rosa", ["Rosa Bank"], cancellationToken);
        var other = await SeedCreditorAsync("Sami", ["Sami Bank"], cancellationToken);
        await SeedCreditorPlanAsync(target, new DateOnly(2026, 1, 10), 2, 20_000, cancellationToken);
        var otherInstallments = await SeedCreditorPlanAsync(other, new DateOnly(2026, 1, 10), 2, 20_000, cancellationToken);

        var result = await PayFullDebtAsync(target.CreditorId, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
        foreach(var installmentId in otherInstallments) {
            var installment = await LoadInstallmentAsync(installmentId, cancellationToken);
            Assert.False(installment.IsPaid);
        }
        Assert.Equal((20_000L, 20_000L), await ReadPayablesFiguresAsync(other.CreditorId, cancellationToken));
    }

    [Fact]
    public async Task Handle_returns_not_found_for_an_unknown_creditor() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await PayFullDebtAsync(Guid.NewGuid(), cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.CreditorNotFound, result.Error);
    }

    private async Task<Result<int>> PayFullDebtAsync(Guid creditorId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new PayCreditorFullDebtHandler(context, new FixedTimeProvider(fixedNow))
            .HandleAsync(new PayCreditorFullDebtCommand(creditorId), cancellationToken);
    }

    private async Task<(long DueNow, long TotalOwed)> ReadPayablesFiguresAsync(Guid creditorId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var response = await new GetCreditorPayablesHandler(context, new FixedTimeProvider(fixedNow))
            .HandleAsync(new GetCreditorPayablesQuery(), cancellationToken);
        var row = response.Rows.Single(candidate => candidate.CreditorId == creditorId);
        return (row.DueNowMinorUnits, row.TotalOwedMinorUnits);
    }

    private async Task<Installment> LoadInstallmentAsync(Guid installmentId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await context.Set<Installment>().FirstAsync(candidate => candidate.Id == installmentId, cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> SeedCreditorPlanAsync(
        (Guid CreditorId, IReadOnlyList<Guid> AccountIds, CreditorRow Row) creditor,
        DateOnly purchaseDate,
        int installmentCount,
        long totalMinorUnits,
        CancellationToken cancellationToken,
        int? markPaidSequence = null,
        int? markReversedSequence = null) {
        await using var context = NewContext();
        var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], purchaseDate, installmentCount, totalMinorUnits);
        var ordered = plan.Installments.OrderBy(installment => installment.Sequence).ToList();
        if(markPaidSequence is not null) {
            ordered[markPaidSequence.Value - 1].MarkPaid(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        }
        if(markReversedSequence is not null) {
            ordered[markReversedSequence.Value - 1].MarkReversed();
        }
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return ordered.Select(installment => installment.Id).ToList();
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
