using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.CreateCreditor;
using PersonalFinance.Financing.Application.Commands.PayCreditorExpense;
using PersonalFinance.Financing.Application.Commands.PayCreditorInstallment;
using PersonalFinance.Financing.Application.Queries.ListCreditors;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class PayCreditorSplitPaymentHandlerTests : IDisposable {
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly FakeLedgerApi ledger = new();
    private readonly Guid bankAccountId = Guid.CreateVersion7();
    private readonly Guid payableAccountId = Guid.CreateVersion7();
    private readonly DbContextOptions<FinancingDbContext> options;

    public PayCreditorSplitPaymentHandlerTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<FinancingDbContext>()
            .UseSqlite(connection)
            .Options;
        ledger.InstrumentAccountIds.Add(bankAccountId);
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() {
        connection.Dispose();
    }

    [Fact]
    public async Task A_partial_payment_smaller_than_my_share_only_debits_creditor_payments() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, installmentIds, _) = await SeedAsync("Ana", 1, 90_00, cancellationToken);
        Assert.True((await PayInstallmentAsync(installmentIds[0], 20_00, cancellationToken)).IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(20_00, DebitTo(posted, ledger.ExpenseCategoryId));
        Assert.Equal(0, DebitTo(posted, payableAccountId));
        Assert.Equal(20_00, CreditTo(posted, bankAccountId));
    }

    [Fact]
    public async Task Paying_an_unaccrued_split_installment_in_full_debits_my_share_and_fronts_every_party() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, installmentIds, _) = await SeedAsync("Beto", 1, 90_00, cancellationToken);
        Assert.True((await PayInstallmentAsync(installmentIds[0], null, cancellationToken)).IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(30_00, DebitTo(posted, ledger.ExpenseCategoryId));
        Assert.Equal(60_00, DebitTo(posted, payableAccountId));
        Assert.Equal(90_00, CreditTo(posted, bankAccountId));
    }

    [Fact]
    public async Task A_payment_past_my_share_fronts_only_the_remainder() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, installmentIds, _) = await SeedAsync("Cora", 1, 90_00, cancellationToken);
        Assert.True((await PayInstallmentAsync(installmentIds[0], 50_00, cancellationToken)).IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(30_00, DebitTo(posted, ledger.ExpenseCategoryId));
        Assert.Equal(20_00, DebitTo(posted, payableAccountId));
    }

    [Fact]
    public async Task A_party_share_already_paid_leaves_only_the_other_parties_to_front() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, installmentIds, partyIds) = await SeedAsync("Dani", 1, 90_00, cancellationToken);
        await using(var context = NewContext()) {
            var installment = await context.Set<Installment>().Include(candidate => candidate.Payments).FirstAsync(candidate => candidate.Id == installmentIds[0], cancellationToken);
            installment.ApplyPayment(30_00, fixedNow, partyIds[0], Guid.CreateVersion7());
            await context.SaveChangesAsync(cancellationToken);
        }
        Assert.True((await PayInstallmentAsync(installmentIds[0], null, cancellationToken)).IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(30_00, DebitTo(posted, ledger.ExpenseCategoryId));
        Assert.Equal(30_00, DebitTo(posted, payableAccountId));
        Assert.Equal(60_00, CreditTo(posted, bankAccountId));
    }

    [Fact]
    public async Task Once_every_party_share_is_paid_my_payment_is_entirely_mine() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, installmentIds, partyIds) = await SeedAsync("Eli", 1, 90_00, cancellationToken);
        await using(var context = NewContext()) {
            var installment = await context.Set<Installment>().Include(candidate => candidate.Payments).FirstAsync(candidate => candidate.Id == installmentIds[0], cancellationToken);
            installment.ApplyPayment(30_00, fixedNow, partyIds[0], Guid.CreateVersion7());
            installment.ApplyPayment(30_00, fixedNow, partyIds[1], Guid.CreateVersion7());
            await context.SaveChangesAsync(cancellationToken);
        }
        Assert.True((await PayInstallmentAsync(installmentIds[0], null, cancellationToken)).IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(30_00, DebitTo(posted, ledger.ExpenseCategoryId));
        Assert.Equal(0, DebitTo(posted, payableAccountId));
    }

    [Fact]
    public async Task A_multi_installment_waterfall_posts_one_transaction_with_my_part_and_the_fronted_part_summed() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (planId, installmentIds, _) = await SeedAsync("Fede", 2, 180_00, cancellationToken);
        await using var context = NewContext();
        var result = await new PayCreditorExpenseHandler(context, new FixedTimeProvider(fixedNow), ledger)
            .HandleAsync(new PayCreditorExpenseCommand(planId, bankAccountId, 120_00), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(60_00, DebitTo(posted, ledger.ExpenseCategoryId));
        Assert.Equal(60_00, DebitTo(posted, payableAccountId));
        Assert.Equal(120_00, CreditTo(posted, bankAccountId));
        Assert.Equal(2, installmentIds.Count);
    }

    private static long DebitTo(PostTransactionCommand posted, Guid accountId) {
        return posted.Lines.Where(line => line.AccountId == accountId && line.Direction == DebitOrCredit.Debit).Sum(line => line.Amount.MinorUnits);
    }

    private static long CreditTo(PostTransactionCommand posted, Guid accountId) {
        return posted.Lines.Where(line => line.AccountId == accountId && line.Direction == DebitOrCredit.Credit).Sum(line => line.Amount.MinorUnits);
    }

    private async Task<Result<Guid>> PayInstallmentAsync(Guid installmentId, long? amount, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new PayCreditorInstallmentHandler(context, new FixedTimeProvider(fixedNow), ledger)
            .HandleAsync(new PayCreditorInstallmentCommand(installmentId, bankAccountId, amount), cancellationToken);
    }

    // Weights [1 me, 1 party, 1 party]: each installment splits in thirds.
    private async Task<(Guid PlanId, IReadOnlyList<Guid> InstallmentIds, IReadOnlyList<Guid> PartyIds)> SeedAsync(string creditorName, int installmentCount, long totalMinorUnits, CancellationToken cancellationToken) {
        await using(var context = NewContext()) {
            await new CreateCreditorHandler(context).HandleAsync(new CreateCreditorCommand(creditorName, [new CreditorAccountPayload($"{creditorName} Bank", null)]), cancellationToken);
        }
        await using var readContext = NewContext();
        var row = (await new ListCreditorsHandler(readContext).HandleAsync(new Contracts.Queries.ListCreditorsQuery(), cancellationToken)).Rows.Single(candidate => candidate.Name == creditorName);
        var partyIds = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() };
        var plan = PaymentPlan.Create(
            cardId: null,
            Money.FromMinorUnits(totalMinorUnits, Currency.Reference),
            installmentCount,
            new DateOnly(2026, 1, 10),
            "Creditor split purchase",
            cutoffDay: null,
            new PhantomPennyAllocator(),
            [(partyIds[0], 1L), (partyIds[1], 1L)],
            creditorId: row.Id,
            creditorAccountId: row.Accounts[0].Id
        ).Value;
        plan.AssignCreditorPayableAccount(payableAccountId);
        plan.LinkSplit(Guid.CreateVersion7(), partyIds.ToDictionary(partyId => partyId, _ => Guid.CreateVersion7()));
        await using var seedContext = NewContext();
        seedContext.PaymentPlans.Add(plan);
        await seedContext.SaveChangesAsync(cancellationToken);
        return (plan.Id, plan.Installments.OrderBy(installment => installment.Sequence).Select(installment => installment.Id).ToList(), partyIds);
    }

    private FinancingDbContext NewContext() {
        return new FinancingDbContext(options, ThrowingConnectionFactory.Instance);
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
}
