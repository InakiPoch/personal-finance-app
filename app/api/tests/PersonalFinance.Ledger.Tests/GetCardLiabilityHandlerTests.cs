using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Application;
using PersonalFinance.Ledger.Application.Queries.GetCardLiability;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public sealed class GetCardLiabilityHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<LedgerDbContext> options;

    public GetCardLiabilityHandlerTests() {
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
    public async Task An_empty_database_yields_no_liability_rows() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var rows = await HandleAsync(null, cancellationToken);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task A_card_with_ars_and_usd_purchases_yields_one_liability_row_per_currency() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardAccountId = await SeedAccountAsync("Visa Liability", AccountType.Liability, AccountKind.CardLiability, cancellationToken);
        var expenseAccountId = await SeedAccountAsync("Visa Purchases", AccountType.Expense, AccountKind.CardPurchases, cancellationToken);
        await PostAsync(expenseAccountId, cardAccountId, Money.FromMinorUnits(50_00, Currency.Reference), cancellationToken);
        await PostAsync(expenseAccountId, cardAccountId, Money.FromMinorUnits(20_00, Currency.Usd), cancellationToken);
        var rows = await HandleAsync(cardAccountId, cancellationToken);
        Assert.Equal(2, rows.Count);
        var arsRow = Assert.Single(rows, row => row.Currency == Currency.Reference);
        var usdRow = Assert.Single(rows, row => row.Currency == Currency.Usd);
        Assert.Equal(50_00, arsRow.MinorUnits);
        Assert.Equal(20_00, usdRow.MinorUnits);
    }

    private async Task PostAsync(Guid debitAccountId, Guid creditAccountId, Money amount, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var writer = new TransactionWriter(context, new NoOpIntegrationEventDispatcher());
        var transaction = Transaction.Post(
            [
                new EntryDraft(debitAccountId, DebitOrCredit.Debit, amount),
                new EntryDraft(creditAccountId, DebitOrCredit.Credit, amount)
            ],
            new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero)
        ).Value;
        await writer.PersistAsync(transaction, cancellationToken);
    }

    private async Task<IReadOnlyList<Money>> HandleAsync(Guid? cardAccountId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var handler = new GetCardLiabilityHandler(context);
        return await handler.HandleAsync(new GetCardLiabilityQuery(cardAccountId), cancellationToken);
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
