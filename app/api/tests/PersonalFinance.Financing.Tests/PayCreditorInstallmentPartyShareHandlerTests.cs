using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.CreateCreditor;
using PersonalFinance.Financing.Application.Commands.PayCreditorInstallmentPartyShare;
using PersonalFinance.Financing.Application.Commands.UnpayCreditorInstallment;
using PersonalFinance.Financing.Application.Queries.ListCreditors;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class PayCreditorInstallmentPartyShareHandlerTests : IDisposable {
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;
    private readonly FakePartiesApi parties = new();
    private readonly FakeLedgerApi ledger = new();
    private readonly Guid payableAccountId = Guid.CreateVersion7();

    public PayCreditorInstallmentPartyShareHandlerTests() {
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
    public async Task Handle_settles_the_party_and_records_the_payment_row() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Nora", ["Nora Bank"], cancellationToken);
        var (installmentId, partyId) = await SeedSplitAccruedInstallmentAsync(creditor, 10_000, cancellationToken);
        var bankAccountId = Guid.CreateVersion7();
        var result = await PayAsync(installmentId, partyId, bankAccountId, cancellationToken);
        Assert.True(result.IsSuccess);
        var settlement = Assert.Single(parties.Settlements);
        Assert.Equal(partyId, settlement.PartyId);
        Assert.Equal(5_000, settlement.AmountMinorUnits);
        Assert.Equal(bankAccountId, settlement.BankAccountId);
        Assert.Equal(fixedNow, settlement.SettledOnUtc);
        Assert.Equal("ARS", settlement.CurrencyCode);
        var installment = await LoadInstallmentAsync(installmentId, cancellationToken);
        var payment = Assert.Single(installment.Payments);
        Assert.Equal(partyId, payment.PartyId);
        Assert.Equal(result.Value, payment.Id);
        Assert.NotNull(payment.SettlementTransactionId);
        Assert.NotNull(payment.LedgerTransactionId);
        var outflow = Assert.Single(ledger.PostedTransactions);
        Assert.Equal("Installment 1/1 · Creditor split purchase · Nora", outflow.Description);
        var debit = Assert.Single(outflow.Lines, line => line.Direction == DebitOrCredit.Debit);
        var credit = Assert.Single(outflow.Lines, line => line.Direction == DebitOrCredit.Credit);
        Assert.Equal(payableAccountId, debit.AccountId);
        Assert.Equal(bankAccountId, credit.AccountId);
        Assert.Equal(5_000, debit.Amount.MinorUnits);
        Assert.Equal(5_000, credit.Amount.MinorUnits);
    }

    [Fact]
    public async Task Handle_compensates_the_settlement_when_the_outgoing_transaction_fails() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Ursula", ["Ursula Bank"], cancellationToken);
        var (installmentId, partyId) = await SeedSplitAccruedInstallmentAsync(creditor, 10_000, cancellationToken);
        ledger.PostTransactionResultOverride = new Error("Ledger.Boom", "Nope.");
        var result = await PayAsync(installmentId, partyId, Guid.CreateVersion7(), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.Boom", result.Error.Code);
        Assert.Single(ledger.ReversedTransactions);
        Assert.Empty((await LoadInstallmentAsync(installmentId, cancellationToken)).Payments);
    }

    [Fact]
    public async Task Handle_marks_the_installment_paid_once_the_party_share_completes_the_remaining() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Omar", ["Omar Bank"], cancellationToken);
        var (installmentId, partyId) = await SeedSplitAccruedInstallmentAsync(creditor, 10_000, cancellationToken);
        await using(var context = NewContext()) {
            var installment = await context.Set<Installment>().Include(candidate => candidate.Payments).FirstAsync(candidate => candidate.Id == installmentId, cancellationToken);
            installment.ApplyPayment(5_000, fixedNow);
            await context.SaveChangesAsync(cancellationToken);
        }
        var result = await PayAsync(installmentId, partyId, Guid.CreateVersion7(), cancellationToken);
        Assert.True(result.IsSuccess);
        var installmentAfter = await LoadInstallmentAsync(installmentId, cancellationToken);
        Assert.True(installmentAfter.IsPaid);
        Assert.Equal(0, installmentAfter.RemainingMinorUnits);
    }

    [Fact]
    public async Task Handle_rejects_a_share_on_an_installment_whose_split_has_not_accrued() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Paula", ["Paula Bank"], cancellationToken);
        var partyId = Guid.CreateVersion7();
        var installmentId = await SeedInstallmentAsync(creditor, 10_000, partyId, splitAccrued: false, cancellationToken);
        var result = await PayAsync(installmentId, partyId, Guid.CreateVersion7(), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.PartyShareNotDue, result.Error);
        Assert.Empty(parties.Settlements);
    }

    [Fact]
    public async Task Handle_rejects_a_party_that_is_not_a_split_participant() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Quinn", ["Quinn Bank"], cancellationToken);
        var (installmentId, _) = await SeedSplitAccruedInstallmentAsync(creditor, 10_000, cancellationToken);
        var strangerId = Guid.CreateVersion7();
        var result = await PayAsync(installmentId, strangerId, Guid.CreateVersion7(), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.PartyNotInSplit, result.Error);
        Assert.Empty(parties.Settlements);
    }

    [Fact]
    public async Task Handle_rejects_a_second_payment_of_the_same_party_share() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Rosa", ["Rosa Bank"], cancellationToken);
        var (installmentId, partyId) = await SeedSplitAccruedInstallmentAsync(creditor, 10_000, cancellationToken);
        Assert.True((await PayAsync(installmentId, partyId, Guid.CreateVersion7(), cancellationToken)).IsSuccess);
        var result = await PayAsync(installmentId, partyId, Guid.CreateVersion7(), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.PartyShareAlreadyPaid, result.Error);
        Assert.Single(parties.Settlements);
    }

    [Fact]
    public async Task Handle_rejects_a_share_that_exceeds_what_remains_without_calling_settlement() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Sami", ["Sami Bank"], cancellationToken);
        var (installmentId, partyId) = await SeedSplitAccruedInstallmentAsync(creditor, 10_000, cancellationToken);
        // The holder pays down to 3_000 remaining, less than the party's fixed 5_000 share.
        await using(var context = NewContext()) {
            var installment = await context.Set<Installment>().Include(candidate => candidate.Payments).FirstAsync(candidate => candidate.Id == installmentId, cancellationToken);
            installment.ApplyPayment(7_000, fixedNow);
            await context.SaveChangesAsync(cancellationToken);
        }
        var result = await PayAsync(installmentId, partyId, Guid.CreateVersion7(), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.PaymentExceedsRemaining, result.Error);
        Assert.Empty(parties.Settlements);
    }

    [Fact]
    public async Task Handle_propagates_a_settlement_failure_and_persists_nothing() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Tomas", ["Tomas Bank"], cancellationToken);
        var (installmentId, partyId) = await SeedSplitAccruedInstallmentAsync(creditor, 10_000, cancellationToken);
        parties.SettleCurrentAccountResultOverride = new Error("Parties.SettlementExceedsBalance", "Already settled.");
        var result = await PayAsync(installmentId, partyId, Guid.CreateVersion7(), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Parties.SettlementExceedsBalance", result.Error.Code);
        var installment = await LoadInstallmentAsync(installmentId, cancellationToken);
        Assert.Empty(installment.Payments);
        Assert.Empty(ledger.ReversedTransactions);
    }

    [Fact]
    public async Task Handle_rejects_an_empty_paid_from_account_before_any_lookup() {
        var result = await PayAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.Empty, TestContext.Current.CancellationToken);
        Assert.Equal(FinancingErrors.InvalidPaidFromAccount, result.Error);
        Assert.Empty(parties.Settlements);
    }

    [Fact]
    public async Task Undo_reverses_the_outgoing_transaction_first_and_stops_when_that_reversal_fails() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Ulises", ["Ulises Bank"], cancellationToken);
        var (installmentId, partyId) = await SeedSplitAccruedInstallmentAsync(creditor, 10_000, cancellationToken);
        Assert.True((await PayAsync(installmentId, partyId, Guid.CreateVersion7(), cancellationToken)).IsSuccess);
        var outgoingId = Assert.Single((await LoadInstallmentAsync(installmentId, cancellationToken)).Payments).LedgerTransactionId;
        ledger.ReversedTransactions.Clear();
        ledger.ReverseTransactionResultOverride = new Error("Ledger.TransactionNotFound", "Missing.");
        await using var context = NewContext();
        var result = await new UnpayCreditorInstallmentHandler(context, new FixedTimeProvider(fixedNow), ledger).HandleAsync(new UnpayCreditorInstallmentCommand(installmentId), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(outgoingId, Assert.Single(ledger.ReversedTransactions).OriginalTransactionId);
        Assert.Single((await LoadInstallmentAsync(installmentId, cancellationToken)).Payments);
    }

    private async Task<Result<Guid>> PayAsync(Guid installmentId, Guid partyId, Guid bankAccountId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new PayCreditorInstallmentPartyShareHandler(context, new FixedTimeProvider(fixedNow), parties, ledger).HandleAsync(new PayCreditorInstallmentPartyShareCommand(installmentId, partyId, bankAccountId), cancellationToken);
    }

    private async Task<(Guid InstallmentId, Guid PartyId)> SeedSplitAccruedInstallmentAsync(
        (Guid CreditorId, IReadOnlyList<Guid> AccountIds, CreditorRow Row) creditor, long totalMinorUnits, CancellationToken cancellationToken) {
        var partyId = Guid.CreateVersion7();
        var installmentId = await SeedInstallmentAsync(creditor, totalMinorUnits, partyId, splitAccrued: true, cancellationToken);
        return (installmentId, partyId);
    }

    private async Task<Guid> SeedInstallmentAsync(
        (Guid CreditorId, IReadOnlyList<Guid> AccountIds, CreditorRow Row) creditor, long totalMinorUnits, Guid partyId, bool splitAccrued, CancellationToken cancellationToken) {
        await using var context = NewContext();
        // 1 cuota; split weights [1 holder, 1 party] -> half each.
        var plan = PaymentPlan.Create(
            cardId: null,
            Money.FromMinorUnits(totalMinorUnits, Currency.Reference),
            1,
            new DateOnly(2026, 1, 10),
            "Creditor split purchase",
            cutoffDay: null,
            new PhantomPennyAllocator(),
            [(partyId, 1L)],
            creditorId: creditor.CreditorId,
            creditorAccountId: creditor.AccountIds[0]
        ).Value;
        plan.AssignCreditorPayableAccount(payableAccountId);
        var installment = plan.Installments.Single();
        if(splitAccrued) {
            installment.MarkSplitAccrued(fixedNow);
        }
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return installment.Id;
    }

    private async Task<Installment> LoadInstallmentAsync(Guid installmentId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await context.Set<Installment>()
            .Include(candidate => candidate.Payments)
            .FirstAsync(candidate => candidate.Id == installmentId, cancellationToken);
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
