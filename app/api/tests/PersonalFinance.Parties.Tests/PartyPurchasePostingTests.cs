using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Parties.Application;
using PersonalFinance.Parties.Application.Commands.RecordPartyPurchase;
using PersonalFinance.Parties.Application.Commands.UndoPartyPurchase;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Parties.Tests;

public sealed class PartyPurchasePostingTests : IDisposable {
    private static readonly DateOnly today = new(2026, 10, 9);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<PartiesDbContext> options;
    private readonly FailOnSaveInterceptor failOnSave = new();
    private readonly FakeLedger ledger = new();
    private readonly TimeProvider clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));

    public PartyPurchasePostingTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<PartiesDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(failOnSave)
            .Options;
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() {
        connection.Dispose();
    }

    [Fact]
    public async Task Debit_purchase_reverses_its_ledger_transaction_when_saving_fails() {
        var partyId = await SeedParty();
        await using var context = NewContext();
        var handler = new RecordPartyPurchaseHandler(context, ledger, NewPoster(context), clock);
        failOnSave.Armed = true;

        await Assert.ThrowsAnyAsync<Exception>(() => handler.HandleAsync(DebitCommand(partyId), TestContext.Current.CancellationToken));

        var posted = Assert.Single(ledger.Posted);
        Assert.Contains(posted, ledger.Reversed);
    }

    [Fact]
    public async Task Poster_reverses_the_transaction_when_marking_the_installment_fails() {
        var purchaseId = await SeedCreditPurchase(installments: 1);
        await using var context = NewContext();
        var poster = NewPoster(context);
        failOnSave.Armed = true;

        await Assert.ThrowsAnyAsync<Exception>(() => poster.PostDueAsync(today, purchaseId, TestContext.Current.CancellationToken));

        var posted = Assert.Single(ledger.Posted);
        Assert.Contains(posted, ledger.Reversed);
    }

    [Fact]
    public async Task Poster_never_posts_a_cancelled_purchase() {
        var purchaseId = await SeedCreditPurchase(installments: 2);
        await using(var context = NewContext()) {
            var purchase = await context.PartyPurchases.FirstAsync(candidate => candidate.Id == purchaseId, TestContext.Current.CancellationToken);
            purchase.Cancel();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var postingContext = NewContext();

        await NewPoster(postingContext).PostDueAsync(today, null, TestContext.Current.CancellationToken);

        Assert.Empty(ledger.Posted);
    }

    [Fact]
    public async Task Undo_that_fails_midway_stays_cancelled_and_a_retry_finishes_the_job() {
        var purchaseId = await SeedCreditPurchase(installments: 2);
        await using(var context = NewContext()) {
            await NewPoster(context).PostDueAsync(new DateOnly(2026, 11, 1), purchaseId, TestContext.Current.CancellationToken);
        }
        Assert.Equal(2, ledger.Posted.Count);
        ledger.FailReversalOf = ledger.Posted[1];
        var partyId = await PartyOf(purchaseId);

        await using(var context = NewContext()) {
            var first = await new UndoPartyPurchaseHandler(context, ledger, clock).HandleAsync(new UndoPartyPurchaseCommand(partyId, purchaseId), TestContext.Current.CancellationToken);
            Assert.True(first.IsFailure);
        }
        await using(var context = NewContext()) {
            var purchase = await context.PartyPurchases.FirstAsync(candidate => candidate.Id == purchaseId, TestContext.Current.CancellationToken);
            Assert.True(purchase.IsCancelled);
        }
        ledger.FailReversalOf = null;

        await using(var context = NewContext()) {
            var retry = await new UndoPartyPurchaseHandler(context, ledger, clock).HandleAsync(new UndoPartyPurchaseCommand(partyId, purchaseId), TestContext.Current.CancellationToken);
            Assert.True(retry.IsSuccess);
        }
        Assert.Equal(2, ledger.Reversed.Count);
        await using(var context = NewContext()) {
            var again = await new UndoPartyPurchaseHandler(context, ledger, clock).HandleAsync(new UndoPartyPurchaseCommand(partyId, purchaseId), TestContext.Current.CancellationToken);
            Assert.Equal(PartiesErrors.PurchaseAlreadyUndone, again.Error);
        }
    }

    private static RecordPartyPurchaseCommand DebitCommand(Guid partyId) {
        return new RecordPartyPurchaseCommand(partyId, 1000, "ARS", "Dinner", "Food", today, PartyPurchaseKinds.Debit, today);
    }

    private PartyPurchaseInstallmentPoster NewPoster(PartiesDbContext context) {
        return new PartyPurchaseInstallmentPoster(context, ledger, clock, NullLogger<PartyPurchaseInstallmentPoster>.Instance);
    }

    private async Task<Guid> SeedParty() {
        await using var context = NewContext();
        var party = Party.Create("Ana", Guid.CreateVersion7(), Guid.CreateVersion7()).Value;
        context.Parties.Add(party);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return party.Id;
    }

    private async Task<Guid> SeedCreditPurchase(int installments) {
        var partyId = await SeedParty();
        await using var context = NewContext();
        var purchase = PartyPurchase.Credit(partyId, "Fridge", "Home", today, Money.FromMinorUnits(500, Currency.FromCode("ARS")), installments, new DateOnly(2026, 10, 1));
        context.PartyPurchases.Add(purchase);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return purchase.Id;
    }

    private async Task<Guid> PartyOf(Guid purchaseId) {
        await using var context = NewContext();
        return (await context.PartyPurchases.FirstAsync(candidate => candidate.Id == purchaseId, TestContext.Current.CancellationToken)).PartyId;
    }

    private PartiesDbContext NewContext() {
        return new PartiesDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private sealed class FailOnSaveInterceptor : SaveChangesInterceptor {
        public bool Armed { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if(Armed) {
                throw new InvalidOperationException("Simulated save failure.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() {
            return now;
        }
    }

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();

        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }

    private sealed class FakeLedger : ILedgerApi {
        public List<Guid> Posted { get; } = [];
        public List<Guid> Reversed { get; } = [];
        public Guid? FailReversalOf { get; set; }

        public Task<Result<Guid>> PostTransactionAsync(PostTransactionCommand command, CancellationToken ct = default) {
            var id = Guid.CreateVersion7();
            Posted.Add(id);
            return Task.FromResult<Result<Guid>>(id);
        }

        public Task<Result<ReverseTransactionResult>> ReverseTransactionAsync(ReverseTransactionCommand command, CancellationToken ct = default) {
            if(command.OriginalTransactionId == FailReversalOf) {
                return Task.FromResult<Result<ReverseTransactionResult>>(new Error("Ledger.Boom", "boom"));
            }
            if(Reversed.Contains(command.OriginalTransactionId)) {
                return Task.FromResult<Result<ReverseTransactionResult>>(new Error("Ledger.TransactionAlreadyReversed", "already reversed"));
            }
            Reversed.Add(command.OriginalTransactionId);
            return Task.FromResult<Result<ReverseTransactionResult>>(new ReverseTransactionResult(Guid.CreateVersion7(), true));
        }

        public Task<Result<Guid>> GetOrCreateExpenseCategoryAsync(GetOrCreateExpenseCategoryCommand command, CancellationToken ct = default) {
            return Task.FromResult<Result<Guid>>(Guid.CreateVersion7());
        }

        public Task<Result<Guid>> PostReceivableAsync(PostReceivableCommand command, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<Guid>> CreateAccountAsync(CreateAccountCommand command, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ExpenseCategoriesResponse> ListExpenseCategoriesAsync(ListExpenseCategoriesQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Money>> GetAccountBalanceAsync(GetAccountBalanceQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Money>> GetCardLiabilityAsync(GetCardLiabilityQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InstrumentAccountsResponse> ListInstrumentAccountsAsync(ListInstrumentAccountsQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AccrualTransactionIdsResponse> FindAccrualTransactionIdsAsync(FindAccrualTransactionIdsQuery query, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
