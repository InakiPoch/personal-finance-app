using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.CreateCreditor;
using PersonalFinance.Financing.Application.Commands.PayCreditorExpense;
using PersonalFinance.Financing.Application.Commands.UnpayCreditorInstallment;
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

public sealed class PayCreditorExpenseHandlerTests : IDisposable {
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public PayCreditorExpenseHandlerTests() {
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
    public async Task Handle_a_partial_payment_fills_cuotas_in_sequence_order_leaving_the_last_one_partial() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Nora", ["Nora Bank"], cancellationToken);
        var (planId, installmentIds) = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 5, 200_000, cancellationToken);
        var result = await PayExpenseAsync(planId, 175_000, cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value);
        for(var i = 0; i < 4; i++) {
            var paid = await LoadInstallmentAsync(installmentIds[i], cancellationToken);
            Assert.True(paid.IsPaid);
            Assert.Equal(fixedNow, paid.PaidOnUtc);
        }
        var last = await LoadInstallmentAsync(installmentIds[4], cancellationToken);
        Assert.False(last.IsPaid);
        Assert.Equal(15_000, last.PaidMinorUnits);
        Assert.Equal(25_000, last.RemainingMinorUnits);
    }

    [Fact]
    public async Task Handle_with_a_null_amount_pays_everything_remaining() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Omar", ["Omar Bank"], cancellationToken);
        var (planId, installmentIds) = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 3, 30_000, cancellationToken);
        var result = await PayExpenseAsync(planId, null, cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value);
        foreach(var installmentId in installmentIds) {
            var installment = await LoadInstallmentAsync(installmentId, cancellationToken);
            Assert.True(installment.IsPaid);
        }
    }

    [Fact]
    public async Task Handle_over_the_remaining_total_rejects_with_payment_exceeds_remaining_and_persists_nothing() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Paula", ["Paula Bank"], cancellationToken);
        var (planId, installmentIds) = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 3, 30_000, cancellationToken);
        var result = await PayExpenseAsync(planId, 40_000, cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.PaymentExceedsRemaining, result.Error);
        foreach(var installmentId in installmentIds) {
            var installment = await LoadInstallmentAsync(installmentId, cancellationToken);
            Assert.False(installment.IsPaid);
            Assert.Equal(0, installment.PaidMinorUnits);
        }
    }

    [Fact]
    public async Task Handle_rejects_a_card_plan_with_not_a_creditor_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var planId = await SeedCardPlanAsync(new DateOnly(2026, 1, 10), 2, 20_000, cancellationToken);
        var result = await PayExpenseAsync(planId, null, cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.NotACreditorInstallment, result.Error);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_an_unknown_payment_plan() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var result = await PayExpenseAsync(Guid.NewGuid(), null, cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.PaymentPlanNotFound, result.Error);
    }

    [Fact]
    public async Task Handle_skips_a_reversed_installment_and_pays_only_the_non_reversed_remaining() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Quinn", ["Quinn Bank"], cancellationToken);
        var (planId, installmentIds) = await SeedCreditorPlanAsync(
            creditor,
            new DateOnly(2026, 1, 10),
            3,
            30_000,
            cancellationToken,
            markReversedSequence: 2
        );
        var result = await PayExpenseAsync(planId, null, cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
        var reversed = await LoadInstallmentAsync(installmentIds[1], cancellationToken);
        Assert.True(reversed.IsReversed);
        Assert.False(reversed.IsPaid);
        Assert.Equal(0, reversed.PaidMinorUnits);
        var first = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        var third = await LoadInstallmentAsync(installmentIds[2], cancellationToken);
        Assert.True(first.IsPaid);
        Assert.True(third.IsPaid);
    }

    [Fact]
    public async Task Handle_returns_zero_when_nothing_remains_to_pay() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Rosa", ["Rosa Bank"], cancellationToken);
        var (planId, _) = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 2, 20_000, cancellationToken);
        Assert.True((await PayExpenseAsync(planId, null, cancellationToken)).IsSuccess);
        var result = await PayExpenseAsync(planId, null, cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public async Task Undo_on_a_partly_paid_cuota_removes_only_its_own_partial_row() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Sami", ["Sami Bank"], cancellationToken);
        var (planId, installmentIds) = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 2, 20_000, cancellationToken);
        Assert.True((await PayExpenseAsync(planId, 15_000, cancellationToken)).IsSuccess);
        var partiallyPaid = await LoadInstallmentAsync(installmentIds[1], cancellationToken);
        Assert.Equal(5_000, partiallyPaid.PaidMinorUnits);
        var undo = await UnpayAsync(installmentIds[1], cancellationToken);
        Assert.True(undo.IsSuccess);
        var afterUndo = await LoadInstallmentAsync(installmentIds[1], cancellationToken);
        Assert.Equal(0, afterUndo.PaidMinorUnits);
        Assert.Equal(10_000, afterUndo.RemainingMinorUnits);
        var untouched = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        Assert.True(untouched.IsPaid);
        Assert.Equal(10_000, untouched.PaidMinorUnits);
    }

    private async Task<Result<int>> PayExpenseAsync(Guid paymentPlanId, long? amountMinorUnits, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new PayCreditorExpenseHandler(context, new FixedTimeProvider(fixedNow)).HandleAsync(new PayCreditorExpenseCommand(paymentPlanId, amountMinorUnits), cancellationToken);
    }

    private async Task<Result<Guid>> UnpayAsync(Guid installmentId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new UnpayCreditorInstallmentHandler(context).HandleAsync(new UnpayCreditorInstallmentCommand(installmentId), cancellationToken);
    }

    private async Task<Installment> LoadInstallmentAsync(Guid installmentId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await context.Set<Installment>()
            .Include(candidate => candidate.Payments)
        .FirstAsync(candidate => candidate.Id == installmentId, cancellationToken);
    }

    private async Task<(Guid PlanId, IReadOnlyList<Guid> InstallmentIds)> SeedCreditorPlanAsync(
        (Guid CreditorId, IReadOnlyList<Guid> AccountIds, CreditorRow Row) creditor,
        DateOnly purchaseDate,
        int installmentCount,
        long totalMinorUnits,
        CancellationToken cancellationToken,
        int? markReversedSequence = null
    ) {
        await using var context = NewContext();
        var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], purchaseDate, installmentCount, totalMinorUnits);
        var ordered = plan.Installments.OrderBy(installment => installment.Sequence).ToList();
        if(markReversedSequence is not null) {
            ordered[markReversedSequence.Value - 1].MarkReversed();
        }
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return (plan.Id, ordered.Select(installment => installment.Id).ToList());
    }

    private async Task<Guid> SeedCardPlanAsync(DateOnly purchaseDate, int installmentCount, long totalMinorUnits, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var plan = CreateCardPlan(purchaseDate, installmentCount, totalMinorUnits);
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return plan.Id;
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
