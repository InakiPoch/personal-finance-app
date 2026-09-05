using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Application.Queries.ListExpenseCategories;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public sealed class ListExpenseCategoriesHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<LedgerDbContext> options;

    public ListExpenseCategoriesHandlerTests() {
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
    public async Task Returns_expense_categories_ordered_case_insensitively() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAccountsAsync(cancellationToken,
            ("Transport", AccountType.Expense, AccountKind.Expense),
            ("Groceries", AccountType.Expense, AccountKind.Expense),
            ("dining out", AccountType.Expense, AccountKind.Expense));

        var response = await Handle(cancellationToken);

        Assert.Equal(["dining out", "Groceries", "Transport"], response.Rows.Select(row => row.Name));
    }

    [Fact]
    public async Task De_duplicates_names_that_differ_only_by_case() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAccountsAsync(cancellationToken,
            ("Groceries", AccountType.Expense, AccountKind.Expense),
            ("groceries", AccountType.Expense, AccountKind.Expense));

        var response = await Handle(cancellationToken);

        Assert.Single(response.Rows);
    }

    [Fact]
    public async Task Excludes_accounts_that_are_not_expense_categories() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAccountsAsync(cancellationToken,
            ("Groceries", AccountType.Expense, AccountKind.Expense),
            ("Visa Purchases", AccountType.Expense, AccountKind.CardPurchases),
            ("Checking", AccountType.Asset, AccountKind.Bank),
            ("Alice", AccountType.Asset, AccountKind.Receivable));

        var response = await Handle(cancellationToken);

        Assert.Equal(["Groceries"], response.Rows.Select(row => row.Name));
    }

    [Fact]
    public async Task Returns_an_empty_list_when_no_categories_exist() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await Handle(cancellationToken);

        Assert.Empty(response.Rows);
    }

    private async Task<ExpenseCategoriesResponse> Handle(CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new ListExpenseCategoriesHandler(context).HandleAsync(new ListExpenseCategoriesQuery(), cancellationToken);
    }

    private async Task SeedAccountsAsync(CancellationToken cancellationToken, params (string Name, AccountType Type, AccountKind Kind)[] accounts) {
        await using var context = NewContext();
        foreach(var (name, type, kind) in accounts) {
            context.Accounts.Add(Account.Create(name, type, kind).Value);
        }
        await context.SaveChangesAsync(cancellationToken);
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
