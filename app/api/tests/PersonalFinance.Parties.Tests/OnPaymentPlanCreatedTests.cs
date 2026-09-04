using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.IntegrationEvents;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Idempotency;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Parties.Application.EventHandlers;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.Parties.Infrastructure.Persistence.Inbox;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Parties.Tests;

public sealed class OnPaymentPlanCreatedTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<PartiesDbContext> options;
    private readonly FakeLedgerApi ledger = new();
    private readonly FakeFinancingApi financing = new();

    public OnPaymentPlanCreatedTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<PartiesDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = NewContext();
        context.Database.EnsureCreated();
    }
    
    public void Dispose() {
        connection.Dispose();
    }

    [Fact]
    public async Task Records_one_expense_split_provisions_receivables_and_links_the_plan() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var integrationEvent = SplitEvent();
        await Handle(integrationEvent);
        await using var context = NewContext();
        Assert.Equal(1, await context.ExpenseSplits.CountAsync(cancellationToken));
        Assert.Equal(2, await context.Parties.CountAsync(cancellationToken));
        Assert.Equal(1, await context.Set<InboxConsumedMessage>().CountAsync(cancellationToken));
        Assert.Equal(2, ledger.CreateAccountCalls);
        Assert.Equal(1, financing.LinkCalls);
    }

    [Fact]
    public async Task Redelivering_the_same_event_is_a_no_op() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var integrationEvent = SplitEvent();

        await Handle(integrationEvent);
        await Handle(integrationEvent);

        await using var context = NewContext();
        Assert.Equal(1, await context.ExpenseSplits.CountAsync(cancellationToken));
        Assert.Equal(2, await context.Parties.CountAsync(cancellationToken));
        Assert.Equal(1, await context.Set<InboxConsumedMessage>().CountAsync(cancellationToken));
        Assert.Equal(2, ledger.CreateAccountCalls);
        Assert.Equal(1, financing.LinkCalls);
    }

    private async Task Handle(PaymentPlanCreatedIntegrationEvent integrationEvent) {
        await using var context = NewContext();
        var handler = new OnPaymentPlanCreated(context, ledger, financing, new PartiesInboxStore(context));
        await handler.HandleAsync(integrationEvent, TestContext.Current.CancellationToken);
    }

    private PartiesDbContext NewContext() {
        return new PartiesDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private static PaymentPlanCreatedIntegrationEvent SplitEvent() {
        return new PaymentPlanCreatedIntegrationEvent(
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            100_000,
            new DateOnly(2026, 3, 15),
            [new SplitParticipant(Guid.CreateVersion7(), 1), new SplitParticipant(Guid.CreateVersion7(), 1)]
        );
    }

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();

        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }

    private sealed class FakeLedgerApi : ILedgerApi {
        public int CreateAccountCalls { get; private set; }

        public Task<Result<Guid>> CreateAccountAsync(CreateAccountCommand command, CancellationToken ct = default) {
            CreateAccountCalls++;
            return Task.FromResult<Result<Guid>>(Guid.CreateVersion7());
        }

        public Task<Result<Guid>> PostTransactionAsync(PostTransactionCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Result<Guid>> PostReceivableAsync(PostReceivableCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Result<ReverseTransactionResult>> ReverseTransactionAsync(ReverseTransactionCommand command, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Money> GetAccountBalanceAsync(GetAccountBalanceQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<Money> GetCardLiabilityAsync(GetCardLiabilityQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<InstrumentAccountsResponse> ListInstrumentAccountsAsync(ListInstrumentAccountsQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<TransactionFeedResponse> GetTransactionsAsync(GetTransactionsQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<AccrualTransactionIdsResponse> FindAccrualTransactionIdsAsync(FindAccrualTransactionIdsQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeFinancingApi : IFinancingApi {
        public int LinkCalls { get; private set; }

        public Task<Result> LinkSplitAsync(LinkPaymentPlanSplitCommand command, CancellationToken ct = default) {
            LinkCalls++;
            return Task.FromResult(Result.Success());
        }

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

        public Task<InstallmentStatusResponse> GetInstallmentStatusAsync(GetInstallmentStatusQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<CardFutureScheduleResponse> GetCardFutureScheduleAsync(GetCardFutureScheduleQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }

        public Task<ListCreditCardsResponse> ListCreditCardsAsync(ListCreditCardsQuery query, CancellationToken ct = default) {
            throw new NotSupportedException();
        }
    }
}
