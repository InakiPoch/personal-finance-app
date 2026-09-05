using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Application.Commands.GetOrCreateExpenseCategory;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public sealed class GetOrCreateExpenseCategoryHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<LedgerDbContext> options;

    public GetOrCreateExpenseCategoryHandlerTests() {
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
    public async Task Creates_a_new_expense_account_for_an_unseen_name() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await Handle("  Groceries  ", cancellationToken);

        Assert.True(result.IsSuccess);
        await using var context = NewContext();
        var account = await context.Accounts.SingleAsync(candidate => candidate.Id == result.Value, cancellationToken);
        Assert.Equal("Groceries", account.Name);
        Assert.Equal(AccountType.Expense, account.Type);
        Assert.Equal(AccountKind.Expense, account.Kind);
    }

    [Fact]
    public async Task Returns_the_existing_account_for_the_same_name_ignoring_case_and_whitespace() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await Handle("Groceries", cancellationToken);

        var second = await Handle("  groceries  ", cancellationToken);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);
        await using var context = NewContext();
        Assert.Equal(1, await context.Accounts.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task Keeps_the_first_writers_casing_as_canonical() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await Handle("Groceries", cancellationToken);

        await Handle("GROCERIES", cancellationToken);

        await using var context = NewContext();
        var account = await context.Accounts.SingleAsync(candidate => candidate.Id == first.Value, cancellationToken);
        Assert.Equal("Groceries", account.Name);
    }

    [Fact]
    public async Task Rejects_a_blank_name() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await Handle("   ", cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.InvalidAccountName", result.Error.Code);
    }

    private async Task<Result<Guid>> Handle(string name, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new GetOrCreateExpenseCategoryHandler(context)
            .HandleAsync(new GetOrCreateExpenseCategoryCommand(name), cancellationToken);
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
