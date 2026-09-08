using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalFinance.Financing.Application.Commands.CreatePaymentPlan;
using PersonalFinance.Financing.Application.Scheduling;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Financing.Infrastructure.Persistence.Outbox;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Parties.Contracts;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class CreatePaymentPlanHandlerTests : IDisposable {
    private static readonly DateTimeOffset asOf = new(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset septemberSeventh = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;
    private readonly FakeLedgerApi ledger = new();
    private readonly TimeProvider clock = new FixedTimeProvider(asOf);

    public CreatePaymentPlanHandlerTests() {
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
    public async Task Handle_persists_a_card_less_creditor_financed_plan() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (creditorId, creditorAccountId) = await SeedCreditorAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            10000, CardId: null, 3, new DateOnly(2026, 1, 10),
            Description: "New laptop", CreditorId: creditorId, CreditorAccountId: creditorAccountId);
        Guid planId;
        await using(var context = NewContext()) {
            var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context), clock, ledger).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        await using var verifyContext = NewContext();
        var plan = await verifyContext.PaymentPlans.SingleAsync(p => p.Id == planId, cancellationToken);
        Assert.Null(plan.CardId);
        Assert.Equal(creditorId, plan.CreditorId);
        Assert.Equal(creditorAccountId, plan.CreditorAccountId);
    }

    [Fact]
    public async Task Handle_rejects_an_unknown_creditor() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new CreatePaymentPlanCommand(
            10000, CardId: null, 3, new DateOnly(2026, 1, 10),
            Description: "New laptop", CreditorId: Guid.CreateVersion7(), CreditorAccountId: Guid.CreateVersion7());
        await using var context = NewContext();
        var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context), clock, ledger).HandleAsync(command, cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.CreditorNotFound", result.Error.Code);
    }

    [Fact]
    public async Task Handle_rejects_an_account_that_does_not_belong_to_the_creditor() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (creditorId, _) = await SeedCreditorAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            10000, CardId: null, 3, new DateOnly(2026, 1, 10),
            Description: "New laptop", CreditorId: creditorId, CreditorAccountId: Guid.CreateVersion7());
        await using var context = NewContext();
        var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context), clock, ledger).HandleAsync(command, cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.CreditorAccountMismatch", result.Error.Code);
    }

    [Fact]
    public async Task Handle_stores_creditor_installments_from_the_purchase_month_so_the_due_cycle_is_the_following_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (creditorId, creditorAccountId) = await SeedCreditorAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            9000, CardId: null, 3, new DateOnly(2026, 1, 10),
            Description: "New laptop", CreditorId: creditorId, CreditorAccountId: creditorAccountId);
        Guid planId;
        await using(var context = NewContext()) {
            var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context), clock, ledger).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        await using var verifyContext = NewContext();
        var plan = await verifyContext.PaymentPlans
            .Include(candidate => candidate.Installments)
            .SingleAsync(candidate => candidate.Id == planId, cancellationToken);
        var installments = plan.Installments.OrderBy(installment => installment.Sequence).ToArray();
        var storedCycles = installments
            .Select(installment => (installment.CycleYear, installment.CycleMonth))
            .ToArray();
        var dueCycles = installments
            .Select(installment => (installment.DueCycle.Year, installment.DueCycle.Month))
            .ToArray();
        Assert.Equal([(2026, 1), (2026, 2), (2026, 3)], storedCycles);
        Assert.Equal([(2026, 2), (2026, 3), (2026, 4)], dueCycles);
    }

    [Fact]
    public async Task Handle_leaves_creditor_fields_null_when_absent() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(10000, cardId, 3, new DateOnly(2026, 1, 10), Description: "New laptop");
        Guid planId;
        await using(var context = NewContext()) {
            var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context), clock, ledger).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        await using var verifyContext = NewContext();
        var plan = await verifyContext.PaymentPlans.SingleAsync(p => p.Id == planId, cancellationToken);
        Assert.Null(plan.CreditorId);
        Assert.Null(plan.CreditorAccountId);
    }

    [Fact]
    public async Task Handle_persists_the_trimmed_description() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(10000, cardId, 3, new DateOnly(2026, 1, 10), Description: "  New laptop  ");
        Guid planId;
        await using(var context = NewContext()) {
            var result = await new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context), clock, ledger).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        await using var verifyContext = NewContext();
        var plan = await verifyContext.PaymentPlans.SingleAsync(p => p.Id == planId, cancellationToken);
        Assert.Equal("New laptop", plan.Description);
    }

    [Fact]
    public async Task Handle_backdated_card_plan_accrues_every_closed_cycle_and_pays_the_ones_whose_due_month_has_passed() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var bankAccountId = Guid.CreateVersion7();
        var command = new CreatePaymentPlanCommand(
            300_000, cardId, 3, new DateOnly(2026, 6, 15), Description: "Back-dated laptop", BankAccountId: bankAccountId);
        Guid planId;
        await using(var context = NewContext()) {
            var result = await HandlerAsOf(context, septemberSeventh).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        var installments = (await LoadPlanAsync(planId, cancellationToken)).Installments
            .OrderBy(installment => installment.Sequence)
            .ToArray();
        Assert.All(installments, installment => Assert.True(installment.IsAccrued));
        Assert.All(installments, installment => Assert.NotNull(installment.StatementId));
        Assert.True(installments[0].IsPaid);
        Assert.True(installments[1].IsPaid);
        Assert.False(installments[2].IsPaid);
        Assert.Equal(2, installments.Count(installment => installment.IsPaid));
    }

    [Fact]
    public async Task Handle_backdated_card_plan_posts_historically_dated_accrual_and_payment_transactions() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var bankAccountId = Guid.CreateVersion7();
        var command = new CreatePaymentPlanCommand(
            300_000, cardId, 3, new DateOnly(2026, 6, 15), Description: "Back-dated laptop", BankAccountId: bankAccountId);
        await using(var context = NewContext()) {
            var result = await HandlerAsOf(context, septemberSeventh).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
        }
        var card = await LoadCardAsync(cardId, cancellationToken);
        var accruals = ledger.PostedTransactions
            .Where(post => post.Lines.Any(line => line.AccountId == card.ExpenseAccountId && line.Direction == DebitOrCredit.Debit))
            .ToList();
        var payments = ledger.PostedTransactions
            .Where(post => post.Lines.Any(line => line.AccountId == bankAccountId && line.Direction == DebitOrCredit.Credit))
            .ToList();
        Assert.Equal(3, accruals.Count);
        Assert.Equal(2, payments.Count);
        Assert.All(accruals, post => {
            Assert.Contains(post.Lines, line => line.AccountId == card.ExpenseAccountId && line.Direction == DebitOrCredit.Debit && line.Amount.MinorUnits == 100_000);
            Assert.Contains(post.Lines, line => line.AccountId == card.LiabilityAccountId && line.Direction == DebitOrCredit.Credit && line.Amount.MinorUnits == 100_000);
            Assert.NotNull(post.InstallmentReferenceId);
        });
        Assert.All(payments, post => {
            Assert.Contains(post.Lines, line => line.AccountId == card.LiabilityAccountId && line.Direction == DebitOrCredit.Debit && line.Amount.MinorUnits == 100_000);
            Assert.Contains(post.Lines, line => line.AccountId == bankAccountId && line.Direction == DebitOrCredit.Credit && line.Amount.MinorUnits == 100_000);
        });
        Assert.Equal(
            [
                new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero)
            ],
            accruals.Select(post => post.PostedOnUtc).OrderBy(instant => instant).ToArray());
        Assert.Equal(
            [
                new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero)
            ],
            payments.Select(post => post.PostedOnUtc).OrderBy(instant => instant).ToArray());
    }

    [Fact]
    public async Task Handle_fully_elapsed_backdated_card_plan_marks_every_installment_and_statement_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            300_000, cardId, 3, new DateOnly(2026, 1, 10), Description: "Old TV", BankAccountId: Guid.CreateVersion7());
        Guid planId;
        await using(var context = NewContext()) {
            var result = await HandlerAsOf(context, septemberSeventh).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        var plan = await LoadPlanAsync(planId, cancellationToken);
        Assert.All(plan.Installments, installment => Assert.True(installment.IsPaid));
        await using var verify = NewContext();
        var statements = await verify.MonthlyStatements.Where(statement => statement.CardId == cardId).ToListAsync(cancellationToken);
        Assert.Equal(3, statements.Count);
        Assert.All(statements, statement => Assert.True(statement.IsPaid));
    }

    [Fact]
    public async Task Handle_fresh_card_plan_accrues_and_pays_nothing() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(300_000, cardId, 3, new DateOnly(2026, 9, 5), Description: "New phone");
        Guid planId;
        await using(var context = NewContext()) {
            var result = await HandlerAsOf(context, septemberSeventh).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        var plan = await LoadPlanAsync(planId, cancellationToken);
        Assert.All(plan.Installments, installment => Assert.False(installment.IsAccrued));
        Assert.All(plan.Installments, installment => Assert.False(installment.IsPaid));
        Assert.Empty(ledger.PostedTransactions);
        await using var verify = NewContext();
        Assert.Empty(await verify.MonthlyStatements.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task Handle_rejects_a_future_purchase_date() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            300_000, cardId, 3, new DateOnly(2026, 9, 8), Description: "Too soon", BankAccountId: Guid.CreateVersion7());
        await using var context = NewContext();
        var result = await HandlerAsOf(context, septemberSeventh).HandleAsync(command, cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.FuturePurchaseDate", result.Error.Code);
        Assert.Empty(ledger.PostedTransactions);
    }

    [Fact]
    public async Task Handle_rejects_a_backdated_card_plan_with_no_bank_account() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            300_000, cardId, 3, new DateOnly(2026, 6, 15), Description: "Back-dated laptop");
        await using var context = NewContext();
        var result = await HandlerAsOf(context, septemberSeventh).HandleAsync(command, cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.BackdatedCardBankAccountRequired", result.Error.Code);
        Assert.Empty(ledger.PostedTransactions);
    }

    [Fact]
    public async Task Accrue_installments_scheduler_leaves_the_presettled_backdated_installments_untouched() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            300_000, cardId, 3, new DateOnly(2026, 6, 15), Description: "Back-dated laptop", BankAccountId: Guid.CreateVersion7());
        Guid planId;
        await using(var context = NewContext()) {
            var result = await HandlerAsOf(context, septemberSeventh).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        var postsAfterCreation = ledger.PostedTransactions.Count;
        Assert.Equal(5, postsAfterCreation);

        await RunAccrueInstallmentsAsync(septemberSeventh);

        Assert.Equal(postsAfterCreation, ledger.PostedTransactions.Count);
        var plan = await LoadPlanAsync(planId, cancellationToken);
        Assert.Equal(3, plan.Installments.Count(installment => installment.IsAccrued));
        Assert.Equal(2, plan.Installments.Count(installment => installment.IsPaid));
    }

    [Fact]
    public async Task Handle_applies_the_26th_cutoff_uniformly_to_creditor_purchases() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (creditorId, creditorAccountId) = await SeedCreditorAsync(cancellationToken);
        var asOfMarch28 = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);
        var afterCutoff = new CreatePaymentPlanCommand(
            9000, CardId: null, 3, new DateOnly(2026, 3, 27),
            Description: "After the cutoff", CreditorId: creditorId, CreditorAccountId: creditorAccountId);
        var onCutoff = new CreatePaymentPlanCommand(
            9000, CardId: null, 3, new DateOnly(2026, 3, 26),
            Description: "On the cutoff", CreditorId: creditorId, CreditorAccountId: creditorAccountId);
        Guid afterId;
        Guid onId;
        await using(var context = NewContext()) {
            afterId = (await HandlerAsOf(context, asOfMarch28).HandleAsync(afterCutoff, cancellationToken)).Value;
        }
        await using(var context = NewContext()) {
            onId = (await HandlerAsOf(context, asOfMarch28).HandleAsync(onCutoff, cancellationToken)).Value;
        }
        var afterCycles = (await LoadPlanAsync(afterId, cancellationToken)).Installments
            .OrderBy(installment => installment.Sequence)
            .Select(installment => (installment.CycleYear, installment.CycleMonth))
            .ToArray();
        var onCycles = (await LoadPlanAsync(onId, cancellationToken)).Installments
            .OrderBy(installment => installment.Sequence)
            .Select(installment => (installment.CycleYear, installment.CycleMonth))
            .ToArray();
        Assert.Equal([(2026, 4), (2026, 5), (2026, 6)], afterCycles);
        Assert.Equal([(2026, 3), (2026, 4), (2026, 5)], onCycles);
    }

    [Fact]
    public async Task Handle_backdated_creditor_plan_stamps_the_elapsed_installments_paid_and_posts_nothing_to_the_ledger() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (creditorId, creditorAccountId) = await SeedCreditorAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            300_000, CardId: null, 3, new DateOnly(2026, 6, 15),
            Description: "Back-dated fridge", CreditorId: creditorId, CreditorAccountId: creditorAccountId);
        Guid planId;
        await using(var context = NewContext()) {
            var result = await HandlerAsOf(context, septemberSeventh).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        // Jun 15 (<= 26) → close cycles Jun/Jul/Aug, due cycles Jul/Aug/Sep. As of Sep 7: Jul + Aug are past, Sep is current.
        var installments = (await LoadPlanAsync(planId, cancellationToken)).Installments
            .OrderBy(installment => installment.Sequence)
            .ToArray();
        Assert.True(installments[0].IsPaid);
        Assert.True(installments[1].IsPaid);
        Assert.False(installments[2].IsPaid);
        Assert.Equal(2, installments.Count(installment => installment.IsPaid));
        Assert.All(installments, installment => Assert.False(installment.IsAccrued));
        Assert.All(installments, installment => Assert.Null(installment.SplitAccruedOnUtc));
        Assert.Empty(ledger.PostedTransactions);
        await using var verify = NewContext();
        Assert.Empty(await verify.MonthlyStatements.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task Handle_fully_elapsed_backdated_creditor_plan_marks_every_installment_paid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (creditorId, creditorAccountId) = await SeedCreditorAsync(cancellationToken);
        var command = new CreatePaymentPlanCommand(
            300_000, CardId: null, 3, new DateOnly(2026, 1, 10),
            Description: "Old sofa", CreditorId: creditorId, CreditorAccountId: creditorAccountId);
        Guid planId;
        await using(var context = NewContext()) {
            var result = await HandlerAsOf(context, septemberSeventh).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        var plan = await LoadPlanAsync(planId, cancellationToken);
        Assert.All(plan.Installments, installment => Assert.True(installment.IsPaid));
        Assert.Empty(ledger.PostedTransactions);
    }

    [Fact]
    public async Task Handle_backdated_creditor_split_stamps_the_holder_cuotas_and_leaves_gate_three_to_accrue_the_co_borrower() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (creditorId, creditorAccountId) = await SeedCreditorAsync(cancellationToken);
        var partyId = Guid.CreateVersion7();
        var command = new CreatePaymentPlanCommand(
            300_000, CardId: null, 3, new DateOnly(2026, 6, 15),
            Description: "Back-dated shared TV",
            Split: new PaymentPlanSplitPayload([new SplitParticipant(partyId, 1L)]),
            CreditorId: creditorId, CreditorAccountId: creditorAccountId);
        Guid planId;
        await using(var context = NewContext()) {
            var result = await HandlerAsOf(context, septemberSeventh).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            planId = result.Value;
        }
        var installments = (await LoadPlanAsync(planId, cancellationToken)).Installments
            .OrderBy(installment => installment.Sequence)
            .ToArray();
        Assert.True(installments[0].IsPaid);
        Assert.True(installments[1].IsPaid);
        Assert.False(installments[2].IsPaid);
        Assert.Empty(ledger.PostedTransactions);

        // Simulate the split link the outbox consumer performs, then let the scheduler run.
        var receivableAccountId = Guid.CreateVersion7();
        var payableAccountId = Guid.CreateVersion7();
        await using(var linkContext = NewContext()) {
            var plan = await linkContext.PaymentPlans
                .Include(candidate => candidate.SplitParticipants)
                .SingleAsync(candidate => candidate.Id == planId, cancellationToken);
            plan.LinkSplit(Guid.CreateVersion7(), new Dictionary<Guid, Guid> { [partyId] = receivableAccountId });
            plan.AssignCreditorPayableAccount(payableAccountId);
            await linkContext.SaveChangesAsync(cancellationToken);
        }

        await RunAccrueInstallmentsAsync(septemberSeventh);

        var creditorSplitAccruals = ledger.PostedTransactions
            .Where(post => post.Description == "Creditor-financed split accrual")
            .ToList();
        Assert.NotEmpty(creditorSplitAccruals);
        Assert.All(creditorSplitAccruals, post => {
            Assert.Contains(post.Lines, line => line.AccountId == receivableAccountId && line.Direction == DebitOrCredit.Debit);
            Assert.Contains(post.Lines, line => line.AccountId == payableAccountId && line.Direction == DebitOrCredit.Credit);
        });
        var afterScheduler = (await LoadPlanAsync(planId, cancellationToken)).Installments
            .OrderBy(installment => installment.Sequence)
            .ToArray();
        Assert.True(afterScheduler[0].IsPaid);
        Assert.True(afterScheduler[1].IsPaid);
        Assert.All(afterScheduler, installment => Assert.NotNull(installment.SplitAccruedOnUtc));
    }

    private async Task<Guid> SeedCardAsync(CancellationToken cancellationToken) {
        var cardId = Guid.CreateVersion7();
        await using var context = NewContext();
        var card = CreditCard.Create(cardId, "Visa", 15, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
        context.CreditCards.Add(card);
        await context.SaveChangesAsync(cancellationToken);
        return cardId;
    }

    private async Task<(Guid CreditorId, Guid AccountId)> SeedCreditorAsync(CancellationToken cancellationToken) {
        var creditorId = Guid.CreateVersion7();
        await using var context = NewContext();
        var creditor = Creditor.Create(creditorId, "MercadoPago", [("Main", "alias.pay")]).Value;
        context.Creditors.Add(creditor);
        await context.SaveChangesAsync(cancellationToken);
        return (creditorId, creditor.Accounts[0].Id);
    }

    private CreatePaymentPlanHandler HandlerAsOf(FinancingDbContext context, DateTimeOffset now) {
        return new CreatePaymentPlanHandler(context, new FinancingOutboxWriter(context), new FixedTimeProvider(now), ledger);
    }

    private async Task<PaymentPlan> LoadPlanAsync(Guid planId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await context.PaymentPlans
            .Include(plan => plan.Installments)
            .SingleAsync(plan => plan.Id == planId, cancellationToken);
    }

    private async Task<CreditCard> LoadCardAsync(Guid cardId, CancellationToken cancellationToken) {
        await using var context = NewContext();
        return await context.CreditCards.SingleAsync(card => card.Id == cardId, cancellationToken);
    }

    private async Task RunAccrueInstallmentsAsync(DateTimeOffset now) {
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddScoped(serviceProvider => new FinancingDbContext(
            serviceProvider.GetRequiredService<DbContextOptions<FinancingDbContext>>(), ThrowingConnectionFactory.Instance));
        services.AddSingleton<ILedgerApi>(ledger);
        services.AddSingleton<IPartiesApi>(new FakePartiesApi());
        services.AddSingleton<IIntegrationEventDispatcher>(new NoOpIntegrationEventDispatcher());
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        var scheduler = new AccrueInstallments(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<AccrueInstallments>>(),
            new FixedTimeProvider(now)
        );
        var tickAsync = typeof(AccrueInstallments)
            .GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)tickAsync.Invoke(scheduler, [CancellationToken.None])!;
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
