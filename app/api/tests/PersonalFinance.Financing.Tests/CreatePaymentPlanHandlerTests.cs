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
    public async Task Handle_persists_the_creditor_and_account_when_supplied() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var creditorId = Guid.CreateVersion7();
        var creditorAccountId = Guid.CreateVersion7();
        var command = new CreatePaymentPlanCommand(
            10000, cardId, 3, new DateOnly(2026, 1, 10),
            CreditorId: creditorId, CreditorAccountId: creditorAccountId);
        Guid planId;
        await using(var context = NewContext()) {
            var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context)).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        await using var verifyContext = NewContext();
        var plan = await verifyContext.PaymentPlans.SingleAsync(p => p.Id == planId, cancellationToken);
        Assert.Equal(creditorId, plan.CreditorId);
        Assert.Equal(creditorAccountId, plan.CreditorAccountId);
    }

    [Fact]
    public async Task Handle_leaves_creditor_fields_null_when_absent() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(10000, cardId, 3, new DateOnly(2026, 1, 10));
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

    private async Task<Guid> SeedCardAsync(CancellationToken cancellationToken) {
        var cardId = Guid.CreateVersion7();
        await using var context = NewContext();
        var card = CreditCard.Create(cardId, "Visa", 15, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
        context.CreditCards.Add(card);
        await context.SaveChangesAsync(cancellationToken);
        return cardId;
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
