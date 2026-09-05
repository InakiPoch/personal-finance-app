using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.MarkInstallmentReversed;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class MarkInstallmentReversedHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public MarkInstallmentReversedHandlerTests() {
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
    public async Task Handle_reverses_a_card_less_creditor_installment_without_touching_a_card() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var plan = PaymentPlan.Create(
            cardId: null,
            Money.FromMinorUnits(9000, Currency.Reference),
            installmentCount: 3,
            purchaseDate: new DateOnly(2026, 1, 10),
            description: "New laptop",
            cutoffDay: null,
            allocator: new PhantomPennyAllocator(),
            creditorId: Guid.CreateVersion7(),
            creditorAccountId: Guid.CreateVersion7()
        ).Value;
        Guid installmentId;
        await using(var context = NewContext()) {
            context.PaymentPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);
            installmentId = plan.Installments[0].Id;
        }

        await using(var context = NewContext()) {
            var result = await new MarkInstallmentReversedHandler(context).HandleAsync(
                new MarkInstallmentReversedCommand(installmentId, CompensatingCreditPosted: false, CreditAmountMinorUnits: 0),
                cancellationToken
            );
            Assert.True(result.IsSuccess);
        }

        await using var verifyContext = NewContext();
        var reversed = await verifyContext.Set<Installment>()
            .SingleAsync(installment => installment.Id == installmentId, cancellationToken);
        Assert.True(reversed.IsReversed);
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
