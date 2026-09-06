using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.CreatePaymentPlan;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Financing.Infrastructure.Persistence.Outbox;
using PersonalFinance.Infrastructure.Persistence;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class CreatePaymentPlanHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public CreatePaymentPlanHandlerTests() {
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
    public async Task Handle_persists_a_card_less_creditor_financed_plan() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (creditorId, creditorAccountId) = await SeedCreditorAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            10000, CardId: null, 3, new DateOnly(2026, 1, 10),
            Description: "New laptop", CreditorId: creditorId, CreditorAccountId: creditorAccountId);
        Guid planId;
        await using(var context = NewContext()) {
            var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context)).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        await using var verifyContext = NewContext();
        var plan = await verifyContext.PaymentPlans.SingleAsync(p => p.Id == planId, cancellationToken);
        Assert.Null(plan.CardId);
        Assert.Equal(creditorId, plan.CreditorId);
        Assert.Equal(creditorAccountId, plan.CreditorAccountId);
    }

    [Fact]
    public async Task Handle_rejects_an_unknown_creditor() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new CreatePaymentPlanCommand(
            10000, CardId: null, 3, new DateOnly(2026, 1, 10),
            Description: "New laptop", CreditorId: Guid.CreateVersion7(), CreditorAccountId: Guid.CreateVersion7());
        await using var context = NewContext();
        var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context)).HandleAsync(command, cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.CreditorNotFound", result.Error.Code);
    }

    [Fact]
    public async Task Handle_rejects_an_account_that_does_not_belong_to_the_creditor() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (creditorId, _) = await SeedCreditorAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            10000, CardId: null, 3, new DateOnly(2026, 1, 10),
            Description: "New laptop", CreditorId: creditorId, CreditorAccountId: Guid.CreateVersion7());
        await using var context = NewContext();
        var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context)).HandleAsync(command, cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.CreditorAccountMismatch", result.Error.Code);
    }

    [Fact]
    public async Task Handle_stores_creditor_installments_from_the_purchase_month_so_the_due_cycle_is_the_following_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (creditorId, creditorAccountId) = await SeedCreditorAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            9000, CardId: null, 3, new DateOnly(2026, 1, 10),
            Description: "New laptop", CreditorId: creditorId, CreditorAccountId: creditorAccountId);
        Guid planId;
        await using(var context = NewContext()) {
            var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context)).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        await using var verifyContext = NewContext();
        var plan = await verifyContext.PaymentPlans
            .Include(candidate => candidate.Installments)
            .SingleAsync(candidate => candidate.Id == planId, cancellationToken);
        var installments = plan.Installments.OrderBy(installment => installment.Sequence).ToArray();
        var storedCycles = installments
            .Select(installment => (installment.CycleYear, installment.CycleMonth))
            .ToArray();
        var dueCycles = installments
            .Select(installment => (installment.DueCycle.Year, installment.DueCycle.Month))
            .ToArray();
        Assert.Equal([(2026, 1), (2026, 2), (2026, 3)], storedCycles);
        Assert.Equal([(2026, 2), (2026, 3), (2026, 4)], dueCycles);
    }

    [Fact]
    public async Task Handle_leaves_creditor_fields_null_when_absent() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(10000, cardId, 3, new DateOnly(2026, 1, 10), Description: "New laptop");
        Guid planId;
        await using(var context = NewContext()) {
            var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context)).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        await using var verifyContext = NewContext();
        var plan = await verifyContext.PaymentPlans.SingleAsync(p => p.Id == planId, cancellationToken);
        Assert.Null(plan.CreditorId);
        Assert.Null(plan.CreditorAccountId);
    }

    [Fact]
    public async Task Handle_persists_the_trimmed_description() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(10000, cardId, 3, new DateOnly(2026, 1, 10), Description: "  New laptop  ");
        Guid planId;
        await using(var context = NewContext()) {
            var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context)).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        await using var verifyContext = NewContext();
        var plan = await verifyContext.PaymentPlans.SingleAsync(p => p.Id == planId, cancellationToken);
        Assert.Equal("New laptop", plan.Description);
    }

    private async Task<Guid> SeedCardAsync(CancellationToken cancellationToken) {
        var cardId = Guid.CreateVersion7();
        await using var context = NewContext();
        var card = CreditCard.Create(cardId, "Visa", 15, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
        context.CreditCards.Add(card);
        await context.SaveChangesAsync(cancellationToken);
        return cardId;
    }

    private async Task<(Guid CreditorId, Guid AccountId)> SeedCreditorAsync(CancellationToken cancellationToken) {
        var creditorId = Guid.CreateVersion7();
        await using var context = NewContext();
        var creditor = Creditor.Create(creditorId, "MercadoPago", [("Main", "alias.pay")]).Value;
        context.Creditors.Add(creditor);
        await context.SaveChangesAsync(cancellationToken);
        return (creditorId, creditor.Accounts[0].Id);
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
