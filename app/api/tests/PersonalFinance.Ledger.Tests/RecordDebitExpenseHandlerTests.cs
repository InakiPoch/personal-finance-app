using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Application;
using PersonalFinance.Ledger.Application.Commands.RecordDebitExpense;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public sealed class RecordDebitExpenseHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<LedgerDbContext> options;
    private readonly FakePartiesApi parties = new();

    public RecordDebitExpenseHandlerTests() {
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
    public async Task Unsplit_expense_posts_a_balanced_two_leg_transaction() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordDebitExpenseCommand(250_00, bankId, "Groceries", new DateOnly(2026, 3, 10), "Weekly shop"), cancellationToken);
        Assert.True(result.IsSuccess);
        await using var context = NewContext();
        var category = await context.Accounts.SingleAsync(account => account.Kind == AccountKind.Expense, cancellationToken);
        Assert.Equal("Groceries", category.Name);
        var transaction = await context.Transactions.Include(t => t.Entries).SingleAsync(cancellationToken);
        Assert.Equal(result.Value, transaction.Id);
        var debit = transaction.Entries.Single(entry => entry.Direction == DebitOrCredit.Debit);
        var credit = transaction.Entries.Single(entry => entry.Direction == DebitOrCredit.Credit);
        Assert.Equal(category.Id, debit.AccountId);
        Assert.Equal(250_00, debit.Amount.MinorUnits);
        Assert.Equal(bankId, credit.AccountId);
        Assert.Equal(250_00, credit.Amount.MinorUnits);
    }

    [Fact]
    public async Task Unsplit_expense_persists_its_description() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        await Handle(new RecordDebitExpenseCommand(250_00, bankId, "Groceries", new DateOnly(2026, 3, 10), "  Weekly shop  "), cancellationToken);
        await using var context = NewContext();
        var transaction = await context.Transactions.SingleAsync(cancellationToken);
        Assert.Equal("Weekly shop", transaction.Description);
    }

    [Fact]
    public async Task Unsplit_expense_reuses_an_existing_category_account() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        await SeedAccountAsync("Groceries", AccountType.Expense, AccountKind.Expense, cancellationToken);
        var result = await Handle(new RecordDebitExpenseCommand(100_00, bankId, "  groceries  ", new DateOnly(2026, 3, 10), "Snacks"), cancellationToken);
        Assert.True(result.IsSuccess);
        await using var context = NewContext();
        Assert.Equal(1, await context.Accounts.CountAsync(account => account.Kind == AccountKind.Expense, cancellationToken));
    }

    [Fact]
    public async Task Split_expense_delegates_to_the_parties_api_with_the_resolved_category_and_source() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        parties.NextSplitReferenceId = Guid.CreateVersion7();
        var result = await Handle(
            new RecordDebitExpenseCommand(300_00, bankId, "Dinner", new DateOnly(2026, 3, 10), "  Team dinner  ",
                [new RecordDebitExpenseParticipant(alice, 1), new RecordDebitExpenseParticipant(bob, 2)]),
            cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(parties.NextSplitReferenceId, result.Value);
        await using var context = NewContext();
        var category = await context.Accounts.SingleAsync(account => account.Kind == AccountKind.Expense, cancellationToken);
        Assert.False(await context.Transactions.AnyAsync(cancellationToken));
        Assert.NotNull(parties.LastSharedExpense);
        var sent = parties.LastSharedExpense!;
        Assert.Equal(category.Id, sent.ExpenseAccountId);
        Assert.Equal(bankId, sent.FundingAccountId);
        Assert.Equal(300_00, sent.TotalMinorUnits);
        Assert.Equal("Team dinner", sent.Description);
        Assert.Equal(new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero), sent.IncurredOnUtc);
        Assert.Equal([(alice, 1L), (bob, 2L)], sent.Participants.Select(participant => (participant.PartyId, participant.Weight)));
    }

    [Fact]
    public async Task Rejects_a_source_account_that_is_not_bank_or_cash() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var receivableId = await SeedAccountAsync("Alice", AccountType.Asset, AccountKind.Receivable, cancellationToken);
        var result = await Handle(new RecordDebitExpenseCommand(100_00, receivableId, "Groceries", new DateOnly(2026, 3, 10), "Shop"), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.SourceAccountNotSpendable", result.Error.Code);
    }

    [Fact]
    public async Task Rejects_an_unknown_source_account() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var result = await Handle(new RecordDebitExpenseCommand(100_00, Guid.CreateVersion7(), "Groceries", new DateOnly(2026, 3, 10), "Shop"), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.AccountNotFound", result.Error.Code);
    }

    [Fact]
    public async Task Rejects_a_non_positive_amount() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordDebitExpenseCommand(0, bankId, "Groceries", new DateOnly(2026, 3, 10), "Shop"), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.NonPositiveEntryAmount", result.Error.Code);
    }

    [Fact]
    public async Task Rejects_a_blank_category() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordDebitExpenseCommand(100_00, bankId, "   ", new DateOnly(2026, 3, 10), "Shop"), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.InvalidExpenseCategory", result.Error.Code);
    }

    [Fact]
    public async Task Rejects_a_blank_description() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordDebitExpenseCommand(100_00, bankId, "Groceries", new DateOnly(2026, 3, 10), "  "), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.InvalidExpenseDescription", result.Error.Code);
    }

    [Fact]
    public async Task An_expense_recorded_in_usd_posts_both_legs_in_usd() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(
            new RecordDebitExpenseCommand(50_00, bankId, "Software", new DateOnly(2026, 3, 10), "Subscription", CurrencyCode: "USD"),
            cancellationToken);
        Assert.True(result.IsSuccess);
        await using var context = NewContext();
        var transaction = await context.Transactions.Include(t => t.Entries).SingleAsync(cancellationToken);
        Assert.All(transaction.Entries, entry => Assert.Equal(Currency.Usd, entry.Amount.Currency));
    }

    [Fact]
    public async Task An_expense_with_no_currency_code_defaults_to_ars() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(new RecordDebitExpenseCommand(50_00, bankId, "Groceries", new DateOnly(2026, 3, 10), "Shop"), cancellationToken);
        Assert.True(result.IsSuccess);
        await using var context = NewContext();
        var transaction = await context.Transactions.Include(t => t.Entries).SingleAsync(cancellationToken);
        Assert.All(transaction.Entries, entry => Assert.Equal(Currency.Reference, entry.Amount.Currency));
    }

    [Fact]
    public async Task Rejects_an_unsupported_currency_code() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(
            new RecordDebitExpenseCommand(100_00, bankId, "Groceries", new DateOnly(2026, 3, 10), "Shop", CurrencyCode: "EUR"),
            cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.InvalidCurrencyCode", result.Error.Code);
    }

    [Fact]
    public async Task Rejects_a_split_with_a_non_positive_weight() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bankId = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var result = await Handle(
            new RecordDebitExpenseCommand(100_00, bankId, "Groceries", new DateOnly(2026, 3, 10), "Shop",
                [new RecordDebitExpenseParticipant(Guid.CreateVersion7(), 0)]),
            cancellationToken
        );
        Assert.True(result.IsFailure);
        Assert.Equal("Ledger.InvalidExpenseSplit", result.Error.Code);
    }

    private async Task<Result<Guid>> Handle(RecordDebitExpenseCommand command, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var handler = new RecordDebitExpenseHandler(context, new TransactionWriter(context, new NoOpIntegrationEventDispatcher()), parties);
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

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();

        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }
}
