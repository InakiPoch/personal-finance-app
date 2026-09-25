using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Application;
using PersonalFinance.Ledger.Application.Commands.RecordIncome;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public sealed class RecordIncomeHandlerTests : IDisposable {
    private static readonly DateOnly today = new(2026, 3, 10);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<LedgerDbContext> options;

    public RecordIncomeHandlerTests() {
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
    public async Task Posts_a_balanced_two_leg_transaction_dr_target_cr_income() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordIncomeCommand(500_00, bankId, today, "Salary"), cancellationToken);
        Assert.True(result.IsSuccess);
        await using var context = NewContext();
        var income = await context.Accounts.SingleAsync(account => account.Kind == AccountKind.Income, cancellationToken);
        Assert.Equal("Income", income.Name);
        var transaction = await context.Transactions.Include(t => t.Entries).SingleAsync(cancellationToken);
        Assert.Equal(result.Value, transaction.Id);
        var debit = transaction.Entries.Single(entry => entry.Direction == DebitOrCredit.Debit);
        var credit = transaction.Entries.Single(entry => entry.Direction == DebitOrCredit.Credit);
        Assert.Equal(bankId, debit.AccountId);
        Assert.Equal(500_00, debit.Amount.MinorUnits);
        Assert.Equal(income.Id, credit.AccountId);
        Assert.Equal(500_00, credit.Amount.MinorUnits);
    }

    [Fact]
    public async Task An_income_recorded_in_usd_posts_both_legs_in_usd() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordIncomeCommand(100_00, bankId, today, "Freelance", CurrencyCode: "USD"), cancellationToken);
        Assert.True(result.IsSuccess);
        await using var context = NewContext();
        var transaction = await context.Transactions.Include(t => t.Entries).SingleAsync(cancellationToken);
        Assert.All(transaction.Entries, entry => Assert.Equal(Currency.Usd, entry.Amount.Currency));
    }

    [Fact]
    public async Task An_income_with_no_currency_code_defaults_to_ars() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordIncomeCommand(100_00, bankId, today, "Salary"), cancellationToken);
        Assert.True(result.IsSuccess);
        await using var context = NewContext();
        var transaction = await context.Transactions.Include(t => t.Entries).SingleAsync(cancellationToken);
        Assert.All(transaction.Entries, entry => Assert.Equal(Currency.Reference, entry.Amount.Currency));
    }

    [Fact]
    public async Task The_income_account_is_created_once_and_reused_on_the_second_income() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        await Handle(new RecordIncomeCommand(500_00, bankId, today, "Salary"), cancellationToken);
        await Handle(new RecordIncomeCommand(200_00, bankId, today, "Bonus"), cancellationToken);
        await using var context = NewContext();
        Assert.Equal(1, await context.Accounts.CountAsync(account => account.Kind == AccountKind.Income, cancellationToken));
    }

    [Fact]
    public async Task The_description_is_persisted_and_trimmed() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        await Handle(new RecordIncomeCommand(500_00, bankId, today, "  Salary  "), cancellationToken);
        await using var context = NewContext();
        var transaction = await context.Transactions.SingleAsync(cancellationToken);
        Assert.Equal("Salary", transaction.Description);
    }

    [Fact]
    public async Task Rejects_a_target_account_that_is_not_bank_or_cash() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var categoryId = await SeedAccountAsync("Groceries", AccountType.Expense, AccountKind.Expense, cancellationToken);
        var result = await Handle(new RecordIncomeCommand(100_00, categoryId, today, "Salary"), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.SourceAccountNotSpendable", result.Error.Code);
    }

    [Fact]
    public async Task Rejects_an_unknown_target_account() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var result = await Handle(new RecordIncomeCommand(100_00, Guid.CreateVersion7(), today, "Salary"), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.AccountNotFound", result.Error.Code);
    }

    [Fact]
    public async Task Rejects_a_future_received_on_date() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordIncomeCommand(100_00, bankId, today.AddDays(1), "Salary"), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.IncomeDateInFuture", result.Error.Code);
    }

    [Fact]
    public async Task Accepts_todays_date() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordIncomeCommand(100_00, bankId, today, "Salary"), cancellationToken);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_back_dated_income_posts_at_that_date() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var backDated = new DateOnly(2026, 2, 1);
        var result = await Handle(new RecordIncomeCommand(100_00, bankId, backDated, "Salary"), cancellationToken);
        Assert.True(result.IsSuccess);
        await using var context = NewContext();
        var transaction = await context.Transactions.SingleAsync(cancellationToken);
        Assert.Equal(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), transaction.PostedOnUtc);
    }

    [Fact]
    public async Task Rejects_a_non_positive_amount() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordIncomeCommand(0, bankId, today, "Salary"), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.NonPositiveEntryAmount", result.Error.Code);
    }

    [Fact]
    public async Task Rejects_a_blank_description() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordIncomeCommand(100_00, bankId, today, "  "), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.InvalidIncomeDescription", result.Error.Code);
    }

    [Fact]
    public async Task Rejects_an_unsupported_currency_code() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordIncomeCommand(100_00, bankId, today, "Salary", CurrencyCode: "EUR"), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.InvalidCurrencyCode", result.Error.Code);
    }

    private async Task<Result<Guid>> Handle(RecordIncomeCommand command, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var handler = new RecordIncomeHandler(context, new TransactionWriter(context, new NoOpIntegrationEventDispatcher()), new FixedTimeProvider(today));
        return await handler.HandleAsync(command, cancellationToken);
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

    private sealed class FixedTimeProvider(DateOnly today) : TimeProvider {
        public override DateTimeOffset GetUtcNow() {
            return today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        }
    }

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();

        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }
}
