using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Queries.GetCardStatements;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class GetCardStatementsHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public GetCardStatementsHandlerTests() {
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
    public async Task Handle_labels_a_statement_by_its_close_month_not_the_due_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = Guid.CreateVersion7();
        await using(var seed = NewContext()) {
            var card = CreditCard.Create(cardId, "Visa", 15, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            var plan = PaymentPlan.Create(
                cardId,
                Money.FromMinorUnits(9_000, Currency.Reference),
                installmentCount: 3,
                purchaseDate: new DateOnly(2026, 9, 6),
                description: "Sofa",
                cutoffDay: 15,
                allocator: new PhantomPennyAllocator()
            ).Value;
            var statement = MonthlyStatement.Open(cardId, plan.Installments[0].Cycle);
            plan.Installments[0].MarkAccrued(DateTimeOffset.UtcNow, statement);
            statement.Accrue(plan.Installments[0]);
            seed.CreditCards.Add(card);
            seed.PaymentPlans.Add(plan);
            seed.MonthlyStatements.Add(statement);
            await seed.SaveChangesAsync(cancellationToken);
        }
        await using var context = NewContext();
        var response = await new GetCardStatementsHandler(context)
            .HandleAsync(new GetCardStatementsQuery(cardId), cancellationToken);
        var row = Assert.Single(response.Rows);
        Assert.Equal(2026, row.CycleYear);
        Assert.Equal(9, row.CycleMonth);
        Assert.Equal(3_000, row.AmountDueMinorUnits);
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
