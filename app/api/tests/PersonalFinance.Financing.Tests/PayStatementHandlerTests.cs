using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

public sealed class PayStatementHandlerTests : IDisposable {
    private const int cutoffDay = 15;
    private static readonly DateTimeOffset paidOnUtc = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;
    private readonly FakeLedgerApi ledger = new();

    public PayStatementHandlerTests() {
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
    public async Task Pays_a_reversal_free_statement_with_the_same_legs_as_the_stored_amount_due() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await SeedStatementAsync(
            planTotalMinorUnits: 6_000,
            installmentCount: 2,
            accrueCount: 2,
            reverseIndexes: [],
            carriedCreditMinorUnits: 0,
            cancellationToken
        );
        var bankAccountId = Guid.CreateVersion7();

        var result = await PayAsync(seeded.StatementId, bankAccountId, cancellationToken);

        Assert.True(result.IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(paidOnUtc, posted.PostedOnUtc);
        Assert.Equal(2, posted.Lines.Count);
        assertLeg(posted.Lines[0], seeded.LiabilityAccountId, DebitOrCredit.Debit, 6_000);
        assertLeg(posted.Lines[1], bankAccountId, DebitOrCredit.Credit, 6_000);
    }

    [Fact]
    public async Task Charges_only_the_non_reversed_installments_when_the_statement_has_a_reversed_cuota() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await SeedStatementAsync(
            planTotalMinorUnits: 9_000,
            installmentCount: 3,
            accrueCount: 3,
            reverseIndexes: [2],
            carriedCreditMinorUnits: 0,
            cancellationToken
        );
        var bankAccountId = Guid.CreateVersion7();

        var result = await PayAsync(seeded.StatementId, bankAccountId, cancellationToken);

        Assert.True(result.IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(2, posted.Lines.Count);
        assertLeg(posted.Lines[0], seeded.LiabilityAccountId, DebitOrCredit.Debit, 6_000);
        // 6_000, not the stored AmountDue of 9_000 — this is the overpay guard.
        assertLeg(posted.Lines[1], bankAccountId, DebitOrCredit.Credit, 6_000);

        await using var verify = NewContext();
        var statement = await verify.MonthlyStatements
            .SingleAsync(candidate => candidate.Id == seeded.StatementId, cancellationToken);
        Assert.Equal(9_000, statement.AmountDue.MinorUnits);
        var installments = await verify.Set<Installment>()
            .Where(installment => installment.PaymentPlanId == seeded.PlanId)
            .OrderBy(installment => installment.Sequence)
            .ToListAsync(cancellationToken);
        Assert.True(installments[0].IsPaid);
        Assert.True(installments[1].IsPaid);
        Assert.False(installments[2].IsPaid);
    }

    [Fact]
    public async Task Stamps_every_settled_installment_and_the_statement_as_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await SeedStatementAsync(
            planTotalMinorUnits: 9_000,
            installmentCount: 3,
            accrueCount: 3,
            reverseIndexes: [],
            carriedCreditMinorUnits: 0,
            cancellationToken
        );

        var result = await PayAsync(seeded.StatementId, Guid.CreateVersion7(), cancellationToken);

        Assert.True(result.IsSuccess);
        await using var verify = NewContext();
        var statement = await verify.MonthlyStatements
            .SingleAsync(candidate => candidate.Id == seeded.StatementId, cancellationToken);
        Assert.Equal(paidOnUtc, statement.PaidOnUtc);
        Assert.True(statement.IsPaid);
        var installments = await verify.Set<Installment>()
            .Where(installment => installment.PaymentPlanId == seeded.PlanId)
            .ToListAsync(cancellationToken);
        Assert.All(installments, installment => Assert.Equal(paidOnUtc, installment.PaidOnUtc));
    }

    [Fact]
    public async Task Nets_carried_credit_against_the_installment_derived_payable_not_the_stored_amount_due() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await SeedStatementAsync(
            planTotalMinorUnits: 9_000,
            installmentCount: 3,
            accrueCount: 3,
            reverseIndexes: [2],
            carriedCreditMinorUnits: 2_000,
            cancellationToken
        );
        var bankAccountId = Guid.CreateVersion7();

        var result = await PayAsync(seeded.StatementId, bankAccountId, cancellationToken);

        Assert.True(result.IsSuccess);
        var posted = Assert.Single(ledger.PostedTransactions);
        Assert.Equal(3, posted.Lines.Count);
        // Payable is 6_000 (the reversed cuota dropped); the 2_000 credit nets that, not the 9_000 AmountDue.
        assertLeg(posted.Lines[0], seeded.LiabilityAccountId, DebitOrCredit.Debit, 6_000);
        assertLeg(posted.Lines[1], bankAccountId, DebitOrCredit.Credit, 4_000);
        assertLeg(posted.Lines[2], seeded.CreditAccountId, DebitOrCredit.Credit, 2_000);

        await using var verify = NewContext();
        var card = await verify.CreditCards
            .SingleAsync(candidate => candidate.Id == seeded.CardId, cancellationToken);
        Assert.Equal(0, card.CarriedCreditBalance.MinorUnits);
    }

    private async Task<Result<Guid>> PayAsync(Guid statementId, Guid bankAccountId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await new PayStatementHandler(context, ledger)
            .HandleAsync(new PayStatementCommand(statementId, bankAccountId, paidOnUtc), cancellationToken);
    }

    private async Task<SeededStatement> SeedStatementAsync(
        long planTotalMinorUnits,
        int installmentCount,
        int accrueCount,
        int[] reverseIndexes,
        long carriedCreditMinorUnits,
        CancellationToken cancellationToken) {
        var cardId = Guid.CreateVersion7();
        var liabilityAccountId = Guid.CreateVersion7();
        var expenseAccountId = Guid.CreateVersion7();
        var creditAccountId = Guid.CreateVersion7();
        await using var seed = NewContext();
        var card = CreditCard.Create(cardId, "Visa", cutoffDay, liabilityAccountId, expenseAccountId, creditAccountId).Value;
        if(carriedCreditMinorUnits > 0) {
            card.ApplyCredit(Money.FromMinorUnits(carriedCreditMinorUnits, Currency.Reference));
        }
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
        seed.CreditCards.Add(card);
        seed.PaymentPlans.Add(plan);
        seed.MonthlyStatements.Add(statement);
        await seed.SaveChangesAsync(cancellationToken);
        return new SeededStatement(cardId, statement.Id, plan.Id, liabilityAccountId, creditAccountId);
    }

    private FinancingDbContext NewContext() {
        return new FinancingDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private static void assertLeg(PostTransactionLine line, Guid accountId, DebitOrCredit direction, long minor) {
        Assert.Equal(accountId, line.AccountId);
        Assert.Equal(direction, line.Direction);
        Assert.Equal(minor, line.Amount.MinorUnits);
    }

    private sealed record SeededStatement(Guid CardId, Guid StatementId, Guid PlanId, Guid LiabilityAccountId, Guid CreditAccountId);

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();

        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }
}
