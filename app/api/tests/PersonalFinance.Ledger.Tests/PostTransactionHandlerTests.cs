using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Application;
using PersonalFinance.Ledger.Application.Commands.PostTransaction;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public sealed class PostTransactionHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<LedgerDbContext> options;

    public PostTransactionHandlerTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() {
        connection.Dispose();
    }

    [Fact]
    public async Task A_transaction_posted_with_a_description_persists_it() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var debitId = await SeedAccountAsync("Groceries", AccountType.Expense, AccountKind.Expense, cancellationToken);
        var creditId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var lines = new List<PostTransactionLine> {
            new(debitId, DebitOrCredit.Debit, Money.FromMinorUnits(100_00, Currency.Reference)),
            new(creditId, DebitOrCredit.Credit, Money.FromMinorUnits(100_00, Currency.Reference))
        };
        var command = new PostTransactionCommand(lines, new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero), Description: "Settlement from Alice");
        await using var context = NewContext();
        var handler = new PostTransactionHandler(new TransactionWriter(context, new NoOpIntegrationEventDispatcher()));
        var result = await handler.HandleAsync(command, cancellationToken);
        Assert.True(result.IsSuccess);
        await using var verifyContext = NewContext();
        var transaction = await verifyContext.Transactions.SingleAsync(cancellationToken);
        Assert.Equal("Settlement from Alice", transaction.Description);
    }

    private async Task<Guid> SeedAccountAsync(string name, AccountType type, AccountKind kind, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var account = Account.Create(name, type, kind).Value;
        context.Accounts.Add(account);
        await context.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    private LedgerDbContext NewContext() {
        return new LedgerDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();

        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }
}
