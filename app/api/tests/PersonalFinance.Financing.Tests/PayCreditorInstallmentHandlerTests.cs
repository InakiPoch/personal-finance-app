using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.CreateCreditor;
using PersonalFinance.Financing.Application.Commands.PayCreditorInstallment;
using PersonalFinance.Financing.Application.Commands.UnpayCreditorInstallment;
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

public sealed class PayCreditorInstallmentHandlerTests : IDisposable {
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public PayCreditorInstallmentHandlerTests() {
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
    public async Task Handle_stamps_paid_on_utc_to_the_clock_for_a_creditor_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Nora", ["Nora Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 3, 30_000, cancellationToken);
        var result = await PayAsync(installmentIds[0], cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(installmentIds[0], result.Value);
        var installment = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        Assert.True(installment.IsPaid);
        Assert.Equal(fixedNow, installment.PaidOnUtc);
    }

    [Fact]
    public async Task Handle_rejects_a_card_installment_with_not_a_creditor_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var installmentIds = await SeedCardPlanAsync(new DateOnly(2026, 1, 10), 2, 20_000, cancellationToken);
        var result = await PayAsync(installmentIds[0], cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.NotACreditorInstallment, result.Error);
        var installment = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        Assert.False(installment.IsPaid);
    }

    [Fact]
    public async Task Handle_rejects_an_already_paid_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Omar", ["Omar Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(
            creditor,
            new DateOnly(2026, 1, 10),
            3,
            30_000,
            cancellationToken,
            markPaidSequence: 1
        );
        var result = await PayAsync(installmentIds[0], cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.InstallmentAlreadyPaid, result.Error);
    }

    [Fact]
    public async Task Handle_rejects_a_reversed_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Paula", ["Paula Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(
            creditor,
            new DateOnly(2026, 1, 10),
            3,
            30_000,
            cancellationToken,
            markReversedSequence: 1
        );
        var result = await PayAsync(installmentIds[0], cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.InstallmentAlreadyReversed, result.Error);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_an_unknown_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var result = await PayAsync(Guid.NewGuid(), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.InstallmentNotFound, result.Error);
    }

    [Fact]
    public async Task Unpay_clears_the_paid_stamp() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Quinn", ["Quinn Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(
            creditor,
            new DateOnly(2026, 1, 10),
            3,
            30_000,
            cancellationToken,
            markPaidSequence: 1
        );
        var result = await UnpayAsync(installmentIds[0], cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(installmentIds[0], result.Value);
        var installment = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        Assert.False(installment.IsPaid);
        Assert.Null(installment.PaidOnUtc);
    }

    [Fact]
    public async Task Unpay_rejects_a_card_installment_with_not_a_creditor_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var installmentIds = await SeedCardPlanAsync(new DateOnly(2026, 1, 10), 2, 20_000, cancellationToken);
        var result = await UnpayAsync(installmentIds[0], cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.NotACreditorInstallment, result.Error);
    }

    [Fact]
    public async Task Unpay_returns_not_found_for_an_unknown_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var result = await UnpayAsync(Guid.NewGuid(), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.InstallmentNotFound, result.Error);
    }

    [Fact]
    public async Task Unpay_of_an_unpaid_installment_reports_no_payment_to_undo() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Rosa", ["Rosa Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 3, 30_000, cancellationToken);
        var result = await UnpayAsync(installmentIds[0], cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.NoPaymentToUndo, result.Error);
        var installment = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        Assert.False(installment.IsPaid);
    }

    [Fact]
    public async Task Unpay_of_a_party_paid_installment_reverses_its_settlement_transaction() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Vito", ["Vito Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 1, 10_000, cancellationToken);
        var partyId = Guid.CreateVersion7();
        var settlementTransactionId = Guid.CreateVersion7();
        await using(var context = NewContext()) {
            var installment = await context.Set<Installment>().Include(candidate => candidate.Payments).FirstAsync(candidate => candidate.Id == installmentIds[0], cancellationToken);
            installment.ApplyPayment(10_000, fixedNow, partyId, settlementTransactionId);
            await context.SaveChangesAsync(cancellationToken);
        }
        var ledger = new FakeLedgerApi();
        var result = await UnpayAsync(installmentIds[0], ledger, cancellationToken);
        Assert.True(result.IsSuccess);
        var reversed = Assert.Single(ledger.ReversedTransactions);
        Assert.Equal(settlementTransactionId, reversed.OriginalTransactionId);
        var installmentAfter = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        Assert.False(installmentAfter.IsPaid);
        Assert.Empty(installmentAfter.Payments);
    }

    [Fact]
    public async Task Unpay_of_a_party_paid_installment_keeps_the_row_when_the_reversal_fails() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Wendy", ["Wendy Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 1, 10_000, cancellationToken);
        var partyId = Guid.CreateVersion7();
        var settlementTransactionId = Guid.CreateVersion7();
        await using(var context = NewContext()) {
            var installment = await context.Set<Installment>().Include(candidate => candidate.Payments).FirstAsync(candidate => candidate.Id == installmentIds[0], cancellationToken);
            installment.ApplyPayment(10_000, fixedNow, partyId, settlementTransactionId);
            await context.SaveChangesAsync(cancellationToken);
        }
        var ledger = new FakeLedgerApi { ReverseTransactionResultOverride = new Error("Ledger.TransactionAlreadyReversed", "Already reversed.") };
        var result = await UnpayAsync(installmentIds[0], ledger, cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.TransactionAlreadyReversed", result.Error.Code);
        var installmentAfter = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        Assert.True(installmentAfter.IsPaid);
        Assert.Single(installmentAfter.Payments);
    }

    [Fact]
    public async Task Unpay_of_an_own_payment_does_not_touch_the_ledger() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Xena", ["Xena Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 3, 30_000, cancellationToken, markPaidSequence: 1);
        var ledger = new FakeLedgerApi();
        var result = await UnpayAsync(installmentIds[0], ledger, cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Empty(ledger.ReversedTransactions);
    }

    [Fact]
    public async Task Handle_with_a_null_amount_pays_whatever_remains_after_an_earlier_partial_payment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Tomas", ["Tomas Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 1, 10_000, cancellationToken);
        Assert.True((await PayAsync(installmentIds[0], 4_000, cancellationToken)).IsSuccess);
        var result = await PayAsync(installmentIds[0], null, cancellationToken);
        Assert.True(result.IsSuccess);
        var installment = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        Assert.True(installment.IsPaid);
        Assert.Equal(0, installment.RemainingMinorUnits);
        Assert.Equal(10_000, installment.PaidMinorUnits);
    }

    [Fact]
    public async Task Handle_a_partial_payment_then_a_second_partial_payment_settles_the_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Ursula", ["Ursula Bank"], cancellationToken);
        var installmentIds = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 1, 10_000, cancellationToken);

        Assert.True((await PayAsync(installmentIds[0], 6_000, cancellationToken)).IsSuccess);
        var installmentAfterFirst = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        Assert.False(installmentAfterFirst.IsPaid);
        Assert.Equal(4_000, installmentAfterFirst.RemainingMinorUnits);

        Assert.True((await PayAsync(installmentIds[0], 4_000, cancellationToken)).IsSuccess);
        var installmentAfterSecond = await LoadInstallmentAsync(installmentIds[0], cancellationToken);
        Assert.True(installmentAfterSecond.IsPaid);
        Assert.Equal(0, installmentAfterSecond.RemainingMinorUnits);
    }

    [Fact]
    public async Task Pay_then_undo_round_trips_the_creditor_payables_figures() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Sami", ["Sami Bank"], cancellationToken);
        // Purchase Jan 10 2026, 3 cuotas / 30_000 -> DueCycles Feb/Mar/Apr 2026, all at or before the
        // current creditor cycle at the fixed clock, so every cuota is "due now".
        var installmentIds = await SeedCreditorPlanAsync(creditor, new DateOnly(2026, 1, 10), 3, 30_000, cancellationToken);
        Assert.Equal((30_000, 30_000), await ReadPayablesFiguresAsync(creditor.CreditorId, cancellationToken));
        Assert.True((await PayAsync(installmentIds[0], cancellationToken)).IsSuccess);
        Assert.Equal((20_000, 20_000), await ReadPayablesFiguresAsync(creditor.CreditorId, cancellationToken));
        Assert.True((await UnpayAsync(installmentIds[0], cancellationToken)).IsSuccess);
        Assert.Equal((30_000, 30_000), await ReadPayablesFiguresAsync(creditor.CreditorId, cancellationToken));
    }

    private async Task<Result<Guid>> PayAsync(Guid installmentId, CancellationToken cancellationToken) {
        return await PayAsync(installmentId, null, cancellationToken);
    }

    private async Task<Result<Guid>> PayAsync(Guid installmentId, long? amountMinorUnits, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new PayCreditorInstallmentHandler(context, new FixedTimeProvider(fixedNow)).HandleAsync(new PayCreditorInstallmentCommand(installmentId, amountMinorUnits), cancellationToken);
    }

    private async Task<Result<Guid>> UnpayAsync(Guid installmentId, CancellationToken cancellationToken) {
        return await UnpayAsync(installmentId, new FakeLedgerApi(), cancellationToken);
    }

    private async Task<Result<Guid>> UnpayAsync(Guid installmentId, FakeLedgerApi ledger, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new UnpayCreditorInstallmentHandler(context, new FixedTimeProvider(fixedNow), ledger).HandleAsync(new UnpayCreditorInstallmentCommand(installmentId), cancellationToken);
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
        DateOnly purchaseDate, int installmentCount,
        long totalMinorUnits, 
        CancellationToken cancellationToken, 
        int? markPaidSequence = null, 
        int? markReversedSequence = null
    ) {
        await using var context = NewContext();
        var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], purchaseDate, installmentCount, totalMinorUnits);
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

    private async Task<IReadOnlyList<Guid>> SeedCardPlanAsync(
        DateOnly purchaseDate, int installmentCount, long totalMinorUnits, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var plan = CreateCardPlan(purchaseDate, installmentCount, totalMinorUnits);
        var ids = plan.Installments.OrderBy(installment => installment.Sequence).Select(installment => installment.Id).ToList();
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return ids;
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
