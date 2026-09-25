using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Application;
using PersonalFinance.Ledger.Application.Commands.ReverseTransaction;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Ledger.Tests;

public sealed class ReverseTransactionHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<LedgerDbContext> options;
    private readonly FakePartiesApi parties = new();
    private readonly FakeFinancingApi financing = new();

    public ReverseTransactionHandlerTests() {
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
    public async Task Reversing_a_transaction_once_posts_the_storno() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var original = await SeedTransactionAsync(cancellationToken);
        var result = await Handle(original, cancellationToken);
        Assert.True(result.IsSuccess);
        await using var context = NewContext();
        var storno = await context.Transactions.SingleAsync(transaction => transaction.OriginalTransactionId == original, cancellationToken);
        Assert.Equal(result.Value.ReversalTransactionId, storno.Id);
    }

    [Fact]
    public async Task Reversing_the_same_transaction_twice_is_rejected_and_posts_no_second_storno() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var original = await SeedTransactionAsync(cancellationToken);
        await Handle(original, cancellationToken);
        var second = await Handle(original, cancellationToken);
        Assert.True(second.IsFailure);
        Assert.Equal("Ledger.TransactionAlreadyReversed", second.Error.Code);
        await using var context = NewContext();
        var stornoCount = await context.Transactions.CountAsync(transaction => transaction.OriginalTransactionId == original, cancellationToken);
        Assert.Equal(1, stornoCount);
    }

    [Fact]
    public async Task The_double_reversal_guard_is_global_and_also_rejects_a_debit_expense_reversed_twice() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var expense = await SeedAccountAsync("Groceries", AccountType.Expense, AccountKind.Expense, cancellationToken);
        var bank = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var original = await SeedTransactionAsync(expense, bank, cancellationToken);
        await Handle(original, cancellationToken);
        var second = await Handle(original, cancellationToken);
        Assert.True(second.IsFailure);
        Assert.Equal("Ledger.TransactionAlreadyReversed", second.Error.Code);
    }

    [Fact]
    public async Task A_reversal_transaction_cannot_itself_be_reversed() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var original = await SeedTransactionAsync(cancellationToken);
        var reversed = await Handle(original, cancellationToken);
        var second = await Handle(reversed.Value.ReversalTransactionId, cancellationToken);
        Assert.True(second.IsFailure);
        Assert.Equal("Ledger.CannotReverseAReversal", second.Error.Code);
    }

    private async Task<Result<ReverseTransactionResult>> Handle(Guid originalTransactionId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var handler = new ReverseTransactionHandler(
            context,
            new TransactionWriter(context, new NoOpIntegrationEventDispatcher()),
            financing,
            parties,
            NullLogger<ReverseTransactionHandler>.Instance);
        return await handler.HandleAsync(new ReverseTransactionCommand(originalTransactionId, new DateTimeOffset(2026, 3, 11, 0, 0, 0, TimeSpan.Zero)), cancellationToken);
    }

    private async Task<Guid> SeedTransactionAsync(CancellationToken cancellationToken) {
        var income = await SeedAccountAsync("Income", AccountType.Income, AccountKind.Income, cancellationToken);
        var bank = await SeedAccountAsync("Checking", AccountType.Asset, AccountKind.Bank, cancellationToken);
        return await SeedTransactionAsync(bank, income, cancellationToken);
    }

    private async Task<Guid> SeedTransactionAsync(Guid debitAccountId, Guid creditAccountId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var transaction = Transaction.Post(
            [
                new EntryDraft(debitAccountId, DebitOrCredit.Debit, Money.FromMinorUnits(100_00, Currency.Reference)),
                new EntryDraft(creditAccountId, DebitOrCredit.Credit, Money.FromMinorUnits(100_00, Currency.Reference))
            ],
            new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero)).Value;
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);
        return transaction.Id;
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

    /// <summary>
    /// Every member throws — a plain reversal (no installment/split reference) never calls Financing.
    /// </summary>
    private sealed class FakeFinancingApi : IFinancingApi {
        public Task<Result<Guid>> CreateCreditCardAsync(CreateCreditCardCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Result<Guid>> CreatePaymentPlanAsync(CreatePaymentPlanCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Result<Guid>> PayStatementAsync(PayStatementCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Result> MarkInstallmentReversedAsync(MarkInstallmentReversedCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Result> LinkSplitAsync(LinkPaymentPlanSplitCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<InstallmentStatusResponse> GetInstallmentStatusAsync(GetInstallmentStatusQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<CardFutureScheduleResponse> GetCardFutureScheduleAsync(GetCardFutureScheduleQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<ListCreditCardsResponse> ListCreditCardsAsync(ListCreditCardsQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Result<Guid>> CreateCreditorAsync(CreateCreditorCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<ListCreditorsResponse> ListCreditorsAsync(ListCreditorsQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<GetFuturePartySharesResponse> GetFuturePartySharesAsync(GetFuturePartySharesQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<GetPendingSharesByPartyResponse> GetPendingSharesByPartyAsync(GetPendingSharesByPartyQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }
    }
}
