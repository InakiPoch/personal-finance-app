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
    public async Task Handle_settles_exactly_the_remaining_amount_on_a_partly_paid_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Nina", ["Nina Bank"], cancellationToken);
        Guid partiallyPaidId;
        await using(var context = NewContext()) {
            var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 1, 10), 2, 20_000);
            var ordered = plan.Installments.OrderBy(installment => installment.Sequence).ToList();
            ordered[0].ApplyPayment(3_000, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
            partiallyPaidId = ordered[0].Id;
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
        }
        var result = await PayFullDebtAsync(creditor.CreditorId, cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
        var settled = await LoadInstallmentAsync(partiallyPaidId, cancellationToken);
        Assert.True(settled.IsPaid);
        Assert.Equal(0, settled.RemainingMinorUnits);
        Assert.Equal(10_000, settled.PaidMinorUnits);
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

    [Fact]
    public async Task Handle_a_partial_amount_fills_oldest_due_cuotas_first_across_purchases_leaving_the_last_partial() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Tomas", ["Tomas Bank"], cancellationToken);
        var planA = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 5), 3, 30_000, cancellationToken);
        var planB = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 2, 20), 1, 10_000, cancellationToken);
        var result = await PayFullDebtAsync(creditor.CreditorId, cancellationToken, amountMinorUnits: 25_000, currencyCode: "ARS");
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
        var aSeq1 = await LoadInstallmentAsync(planA[0], cancellationToken);
        var aSeq2 = await LoadInstallmentAsync(planA[1], cancellationToken);
        var aSeq3 = await LoadInstallmentAsync(planA[2], cancellationToken);
        var bSeq1 = await LoadInstallmentAsync(planB[0], cancellationToken);
        Assert.True(aSeq1.IsPaid);
        Assert.True(aSeq2.IsPaid);
        Assert.Equal(0, aSeq3.PaidMinorUnits);
        Assert.False(bSeq1.IsPaid);
        Assert.Equal(5_000, bSeq1.PaidMinorUnits);
        Assert.Equal(5_000, bSeq1.RemainingMinorUnits);
    }

    [Fact]
    public async Task Handle_a_set_amount_only_touches_the_chosen_currency_in_a_mixed_currency_creditor() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Uma", ["Uma Bank"], cancellationToken);
        var arsPlan = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 5), 3, 30_000, cancellationToken);
        var usdPlan = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 5), 3, 30_000, cancellationToken, currency: Currency.Usd);
        var result = await PayFullDebtAsync(creditor.CreditorId, cancellationToken, amountMinorUnits: 15_000, currencyCode: "USD");
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        var usdFirst = await LoadInstallmentAsync(usdPlan[0], cancellationToken);
        var usdSecond = await LoadInstallmentAsync(usdPlan[1], cancellationToken);
        Assert.True(usdFirst.IsPaid);
        Assert.Equal(5_000, usdSecond.PaidMinorUnits);
        foreach(var installmentId in arsPlan) {
            var installment = await LoadInstallmentAsync(installmentId, cancellationToken);
            Assert.Equal(0, installment.PaidMinorUnits);
        }
    }

    [Fact]
    public async Task Handle_a_null_amount_settles_every_currencys_remaining_debt() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Vera", ["Vera Bank"], cancellationToken);
        var arsPlan = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 5), 2, 20_000, cancellationToken);
        var usdPlan = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 5), 2, 20_000, cancellationToken, currency: Currency.Usd);
        var result = await PayFullDebtAsync(creditor.CreditorId, cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value);
        foreach(var installmentId in arsPlan.Concat(usdPlan)) {
            var installment = await LoadInstallmentAsync(installmentId, cancellationToken);
            Assert.True(installment.IsPaid);
        }
    }

    [Fact]
    public async Task Handle_a_set_amount_without_a_currency_code_rejects_with_invalid_currency_code() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Willa", ["Willa Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 5), 2, 20_000, cancellationToken);
        var result = await PayFullDebtAsync(creditor.CreditorId, cancellationToken, amountMinorUnits: 10_000);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.InvalidCurrencyCode, result.Error);
        foreach(var installmentId in installmentIds) {
            var installment = await LoadInstallmentAsync(installmentId, cancellationToken);
            Assert.Equal(0, installment.PaidMinorUnits);
        }
    }

    [Fact]
    public async Task Handle_a_set_amount_over_the_currencys_remaining_total_rejects_with_payment_exceeds_remaining_and_persists_nothing() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Xena", ["Xena Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 5), 3, 30_000, cancellationToken);
        var result = await PayFullDebtAsync(creditor.CreditorId, cancellationToken, amountMinorUnits: 40_000, currencyCode: "ARS");
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.PaymentExceedsRemaining, result.Error);
        foreach(var installmentId in installmentIds) {
            var installment = await LoadInstallmentAsync(installmentId, cancellationToken);
            Assert.Equal(0, installment.PaidMinorUnits);
        }
    }

    private async Task<Result<int>> PayFullDebtAsync(Guid creditorId, CancellationToken cancellationToken, long? amountMinorUnits = null, string? currencyCode = null) {
        await using var context = NewContext();
        return await new PayCreditorFullDebtHandler(context, new FixedTimeProvider(fixedNow))
            .HandleAsync(new PayCreditorFullDebtCommand(creditorId, amountMinorUnits, currencyCode), cancellationToken);
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
        return await context.Set<Installment>()
            .Include(candidate => candidate.Payments)
        .FirstAsync(candidate => candidate.Id == installmentId, cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> SeedCreditorPlanAsync(
        (Guid CreditorId, IReadOnlyList<Guid> AccountIds, CreditorRow Row) creditor,
        DateOnly purchaseDate,
        int installmentCount,
        long totalMinorUnits,
        CancellationToken cancellationToken,
        int? markPaidSequence = null,
        int? markReversedSequence = null,
        Currency? currency = null
    ) {
        await using var context = NewContext();
        var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], purchaseDate, installmentCount, totalMinorUnits, currency);
        var ordered = plan.Installments.OrderBy(installment => installment.Sequence).ToList();
        if(markPaidSequence is not null) {
            var paidInstallment = ordered[markPaidSequence.Value - 1];
            paidInstallment.ApplyPayment(paidInstallment.Amount.MinorUnits, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        }
        if(markReversedSequence is not null) {
            ordered[markReversedSequence.Value - 1].MarkReversed();
        }
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return ordered.Select(installment => installment.Id).ToList();
    }

    private static PaymentPlan CreateCreditorPlan(Guid creditorId, Guid creditorAccountId, DateOnly purchaseDate, int installmentCount, long totalMinorUnits, Currency? currency = null) {
        var total = Money.FromMinorUnits(totalMinorUnits, currency ?? Currency.Reference);
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
