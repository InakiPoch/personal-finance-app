using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.PayInstallment;
using PersonalFinance.Financing.Application.Commands.PayStatement;
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

public sealed class PayInstallmentHandlerTests : IDisposable {
    private const int cutoffDay = 15;
    private static readonly DateTimeOffset paidOnUtc = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;
    private readonly FakeLedgerApi ledger = new();

    public PayInstallmentHandlerTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<FinancingDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() {
        connection.Dispose();
    }

    [Fact]
    public async Task Pays_one_installment_with_a_plain_liability_to_bank_leg_and_leaves_the_statement_unpaid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await SeedStatementAsync(
            planTotalMinorUnits: 9_000,
            installmentCount: 3,
            accrueCount: 3,
            reverseIndexes: [],
            cancellationToken
        );
        var bankAccountId = Guid.CreateVersion7();

        var result = await PayInstallmentAsync(seeded.InstallmentIds[0], bankAccountId, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(seeded.InstallmentIds[0], result.Value);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(paidOnUtc, posted.PostedOnUtc);
        Assert.Equal(2, posted.Lines.Count);
        assertLeg(posted.Lines[0], seeded.LiabilityAccountId, DebitOrCredit.Debit, 3_000);
        assertLeg(posted.Lines[1], bankAccountId, DebitOrCredit.Credit, 3_000);

        await using var verify = NewContext();
        var installments = await LoadInstallmentsAsync(verify, seeded.PlanId, cancellationToken);
        Assert.Equal(paidOnUtc, installments[0].PaidOnUtc);
        Assert.False(installments[1].IsPaid);
        Assert.False(installments[2].IsPaid);
        var statement = await verify.MonthlyStatements
            .SingleAsync(candidate => candidate.Id == seeded.StatementId, cancellationToken);
        Assert.False(statement.IsPaid);
    }

    [Fact]
    public async Task Paying_the_last_unpaid_installment_marks_the_statement_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await SeedStatementAsync(
            planTotalMinorUnits: 9_000,
            installmentCount: 3,
            accrueCount: 3,
            reverseIndexes: [],
            cancellationToken
        );
        var bankAccountId = Guid.CreateVersion7();

        Assert.True((await PayInstallmentAsync(seeded.InstallmentIds[0], bankAccountId, cancellationToken)).IsSuccess);
        Assert.True((await PayInstallmentAsync(seeded.InstallmentIds[1], bankAccountId, cancellationToken)).IsSuccess);
        await using(var midway = NewContext()) {
            var open = await midway.MonthlyStatements
                .SingleAsync(candidate => candidate.Id == seeded.StatementId, cancellationToken);
            Assert.False(open.IsPaid);
        }

        Assert.True((await PayInstallmentAsync(seeded.InstallmentIds[2], bankAccountId, cancellationToken)).IsSuccess);

        await using var verify = NewContext();
        var statement = await verify.MonthlyStatements
            .SingleAsync(candidate => candidate.Id == seeded.StatementId, cancellationToken);
        Assert.True(statement.IsPaid);
        Assert.Equal(paidOnUtc, statement.PaidOnUtc);
        var installments = await LoadInstallmentsAsync(verify, seeded.PlanId, cancellationToken);
        Assert.All(installments, installment => Assert.Equal(paidOnUtc, installment.PaidOnUtc));
    }

    [Fact]
    public async Task After_an_individual_payment_PayStatement_charges_only_the_remaining_installments() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await SeedStatementAsync(
            planTotalMinorUnits: 9_000,
            installmentCount: 3,
            accrueCount: 3,
            reverseIndexes: [],
            cancellationToken
        );
        var bankAccountId = Guid.CreateVersion7();

        Assert.True((await PayInstallmentAsync(seeded.InstallmentIds[0], bankAccountId, cancellationToken)).IsSuccess);
        var statementResult = await PayStatementAsync(seeded.StatementId, bankAccountId, cancellationToken);

        Assert.True(statementResult.IsSuccess);
        Assert.Equal(2, ledger.PostedTransactions.Count);
        var statementPosting = ledger.PostedTransactions[1];
        Assert.Equal(2, statementPosting.Lines.Count);
        // 6_000 = the two still-unpaid cuotas, not the 9_000 AmountDue — proves no double-pay.
        assertLeg(statementPosting.Lines[0], seeded.LiabilityAccountId, DebitOrCredit.Debit, 6_000);
        assertLeg(statementPosting.Lines[1], bankAccountId, DebitOrCredit.Credit, 6_000);
    }

    [Fact]
    public async Task Rejects_an_installment_that_was_already_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await SeedStatementAsync(
            planTotalMinorUnits: 9_000,
            installmentCount: 3,
            accrueCount: 3,
            reverseIndexes: [],
            cancellationToken
        );
        var bankAccountId = Guid.CreateVersion7();
        Assert.True((await PayInstallmentAsync(seeded.InstallmentIds[0], bankAccountId, cancellationToken)).IsSuccess);

        var result = await PayInstallmentAsync(seeded.InstallmentIds[0], bankAccountId, cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.InstallmentAlreadyPaid, result.Error);
        Assert.Single(ledger.PostedTransactions);
    }

    [Fact]
    public async Task Rejects_a_reversed_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await SeedStatementAsync(
            planTotalMinorUnits: 9_000,
            installmentCount: 3,
            accrueCount: 3,
            reverseIndexes: [0],
            cancellationToken
        );

        var result = await PayInstallmentAsync(seeded.InstallmentIds[0], Guid.CreateVersion7(), cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.InstallmentAlreadyReversed, result.Error);
        Assert.Empty(ledger.PostedTransactions);
    }

    [Fact]
    public async Task Rejects_an_installment_that_has_not_been_accrued_yet() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await SeedStatementAsync(
            planTotalMinorUnits: 9_000,
            installmentCount: 3,
            accrueCount: 1,
            reverseIndexes: [],
            cancellationToken
        );

        var result = await PayInstallmentAsync(seeded.InstallmentIds[2], Guid.CreateVersion7(), cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.InstallmentNotAccrued, result.Error);
        Assert.Empty(ledger.PostedTransactions);
    }

    [Fact]
    public async Task Rejects_an_unknown_installment_id() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await PayInstallmentAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(FinancingErrors.InstallmentNotFound, result.Error);
        Assert.Empty(ledger.PostedTransactions);
    }

    private async Task<Result<Guid>> PayInstallmentAsync(Guid installmentId, Guid bankAccountId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new PayInstallmentHandler(context, ledger)
            .HandleAsync(new PayInstallmentCommand(installmentId, bankAccountId, paidOnUtc), cancellationToken);
    }

    private async Task<Result<Guid>> PayStatementAsync(Guid statementId, Guid bankAccountId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new PayStatementHandler(context, ledger)
            .HandleAsync(new PayStatementCommand(statementId, bankAccountId, paidOnUtc), cancellationToken);
    }

    private async Task<SeededStatement> SeedStatementAsync(
        long planTotalMinorUnits,
        int installmentCount,
        int accrueCount,
        int[] reverseIndexes,
        CancellationToken cancellationToken) {
        var cardId = Guid.CreateVersion7();
        var liabilityAccountId = Guid.CreateVersion7();
        var expenseAccountId = Guid.CreateVersion7();
        var creditAccountId = Guid.CreateVersion7();
        await using var seed = NewContext();
        var card = CreditCard.Create(cardId, "Visa", cutoffDay, liabilityAccountId, expenseAccountId, creditAccountId).Value;
        var plan = PaymentPlan.Create(
            cardId,
            Money.FromMinorUnits(planTotalMinorUnits, Currency.Reference),
            installmentCount: installmentCount,
            purchaseDate: new DateOnly(2026, 1, 10),
            description: "Sofa",
            cutoffDay: cutoffDay,
            allocator: new PhantomPennyAllocator()
        ).Value;
        var statement = MonthlyStatement.Open(cardId, plan.Installments[0].Cycle, Currency.Reference);
        for(var index = 0; index < accrueCount; index++) {
            plan.Installments[index].MarkAccrued(DateTimeOffset.UtcNow, statement);
            statement.Accrue(plan.Installments[index]);
        }
        foreach(var index in reverseIndexes) {
            plan.Installments[index].MarkReversed();
        }
        var installmentIds = plan.Installments
            .OrderBy(installment => installment.Sequence)
            .Select(installment => installment.Id)
            .ToList();
        seed.CreditCards.Add(card);
        seed.PaymentPlans.Add(plan);
        seed.MonthlyStatements.Add(statement);
        await seed.SaveChangesAsync(cancellationToken);
        return new SeededStatement(cardId, statement.Id, plan.Id, liabilityAccountId, installmentIds);
    }

    private async Task<List<Installment>> LoadInstallmentsAsync(FinancingDbContext context, Guid planId, CancellationToken cancellationToken) {
        return await context.Set<Installment>()
            .Where(installment => installment.PaymentPlanId == planId)
            .OrderBy(installment => installment.Sequence)
            .ToListAsync(cancellationToken);
    }

    private FinancingDbContext NewContext() {
        return new FinancingDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private static void assertLeg(PostTransactionLine line, Guid accountId, DebitOrCredit direction, long minor) {
        Assert.Equal(accountId, line.AccountId);
        Assert.Equal(direction, line.Direction);
        Assert.Equal(minor, line.Amount.MinorUnits);
    }

    private sealed record SeededStatement(Guid CardId, Guid StatementId, Guid PlanId, Guid LiabilityAccountId, IReadOnlyList<Guid> InstallmentIds);

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();

        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }
}
