using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class LinkPaymentPlanSplitHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;
    private readonly FakeLedgerApi ledger = new();

    public LinkPaymentPlanSplitHandlerTests() {
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
    public async Task Handle_provisions_a_creditor_payable_liability_for_a_card_less_plan() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var payableAccountId = Guid.CreateVersion7();
        ledger.NextAccountId = payableAccountId;
        var plan = cardLessPlan(partyId);
        await Persist(plan, cancellationToken);
        var splitReferenceId = Guid.CreateVersion7();

        var result = await Handle(new LinkPaymentPlanSplitCommand(
            plan.Id, splitReferenceId, [new PartyReceivable(partyId, Guid.CreateVersion7())]), cancellationToken);

        Assert.True(result.IsSuccess);
        var created = Assert.Single(ledger.CreatedAccounts);
        Assert.Equal(AccountType.Liability, created.Type);
        Assert.Equal(AccountKind.CreditorPayable, created.Kind);
        Assert.Equal(plan.Id, created.OwnerReferenceId);
        await using var verifyContext = NewContext();
        var stored = await verifyContext.PaymentPlans.SingleAsync(candidate => candidate.Id == plan.Id, cancellationToken);
        Assert.Equal(payableAccountId, stored.CreditorPayableAccountId);
        Assert.Equal(splitReferenceId, stored.SplitReferenceId);
    }

    [Fact]
    public async Task Handle_books_the_co_borrower_receivable_in_full_up_front_for_a_card_less_split() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var receivableAccountId = Guid.CreateVersion7();
        var payableAccountId = Guid.CreateVersion7();
        ledger.NextAccountId = payableAccountId;
        var plan = cardLessPlan(partyId);
        await Persist(plan, cancellationToken);
        var splitReferenceId = Guid.CreateVersion7();
        var result = await Handle(new LinkPaymentPlanSplitCommand(
            plan.Id, splitReferenceId, [new PartyReceivable(partyId, receivableAccountId)]), cancellationToken);
        Assert.True(result.IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(splitReferenceId, posted.SplitReferenceId);
        Assert.Null(posted.InstallmentReferenceId);
        Assert.Equal(new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero), posted.PostedOnUtc);
        var debit = Assert.Single(posted.Lines, line => line.Direction == DebitOrCredit.Debit);
        Assert.Equal(receivableAccountId, debit.AccountId);
        Assert.Equal(4500, debit.Amount.MinorUnits);
        var credit = Assert.Single(posted.Lines, line => line.Direction == DebitOrCredit.Credit);
        Assert.Equal(payableAccountId, credit.AccountId);
        Assert.Equal(4500, credit.Amount.MinorUnits);
    }

    [Fact]
    public async Task Handle_does_not_provision_a_creditor_payable_for_a_card_backed_plan() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var plan = PaymentPlan.Create(
            cardId: Guid.CreateVersion7(),
            Money.FromMinorUnits(9000, Currency.Reference),
            installmentCount: 3,
            purchaseDate: new DateOnly(2026, 1, 10),
            description: "New laptop",
            cutoffDay: 15,
            allocator: new PhantomPennyAllocator(),
            splitParticipants: [(partyId, 1L)]
        ).Value;
        await Persist(plan, cancellationToken);
        var result = await Handle(new LinkPaymentPlanSplitCommand(
            plan.Id, Guid.CreateVersion7(), [new PartyReceivable(partyId, Guid.CreateVersion7())]), cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Empty(ledger.CreatedAccounts);
        Assert.Empty(ledger.PostedTransactions);
        await using var verifyContext = NewContext();
        var stored = await verifyContext.PaymentPlans.SingleAsync(candidate => candidate.Id == plan.Id, cancellationToken);
        Assert.Null(stored.CreditorPayableAccountId);
    }

    [Fact]
    public async Task Handle_is_idempotent_when_the_plan_is_already_linked() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var partyId = Guid.CreateVersion7();
        var plan = cardLessPlan(partyId);
        await Persist(plan, cancellationToken);
        var command = new LinkPaymentPlanSplitCommand(plan.Id, Guid.CreateVersion7(), [new PartyReceivable(partyId, Guid.CreateVersion7())]);

        await Handle(command, cancellationToken);
        await Handle(command, cancellationToken);

        Assert.Single(ledger.CreatedAccounts);
    }

    private static PaymentPlan cardLessPlan(Guid partyId) {
        return PaymentPlan.Create(
            cardId: null,
            Money.FromMinorUnits(9000, Currency.Reference),
            installmentCount: 3,
            purchaseDate: new DateOnly(2026, 1, 10),
            description: "New laptop",
            cutoffDay: null,
            allocator: new PhantomPennyAllocator(),
            splitParticipants: [(partyId, 1L)],
            creditorId: Guid.CreateVersion7(),
            creditorAccountId: Guid.CreateVersion7()
        ).Value;
    }

    private async Task Persist(PaymentPlan plan, CancellationToken cancellationToken) {
        await using var context = NewContext();
        context.PaymentPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result> Handle(LinkPaymentPlanSplitCommand command, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new LinkPaymentPlanSplitHandler(context, ledger).HandleAsync(command, cancellationToken);
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
