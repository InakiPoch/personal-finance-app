using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.CreateCreditor;
using PersonalFinance.Financing.Application.Queries.GetCreditorPayables;
using PersonalFinance.Financing.Application.Queries.GetDueThisMonth;
using PersonalFinance.Financing.Application.Queries.ListCreditors;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class GetDueThisMonthHandlerTests : IDisposable {
    // Card cutoff 15: a purchase on May 10 falls in the May cycle, due June.
    private static readonly DateOnly dueThisMonthPurchase = new(2026, 5, 10);
    private static readonly DateOnly overduePurchase = new(2026, 1, 10);
    private static readonly DateOnly dueNextMonthPurchase = new(2026, 6, 10);
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public GetDueThisMonthHandlerTests() {
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
    public async Task Handle_counts_a_card_installment_due_this_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        await SeedPlansAsync([CreateCardPlan(cardId, dueThisMonthPurchase, 1, 10_000, Currency.Reference)], cancellationToken);
        var row = Assert.Single((await RunAsync(fixedNow, cancellationToken)).Rows);
        Assert.Equal("card", row.Kind);
        Assert.Equal(cardId, row.SourceId);
        Assert.Equal("Visa", row.SourceName);
        Assert.Equal("ARS", row.CurrencyCode);
        Assert.Equal(10_000, row.AmountMinorUnits);
    }

    [Fact]
    public async Task Handle_counts_an_overdue_card_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        await SeedPlansAsync([CreateCardPlan(cardId, overduePurchase, 1, 7_000, Currency.Reference)], cancellationToken);
        var row = Assert.Single((await RunAsync(fixedNow, cancellationToken)).Rows);
        Assert.Equal(7_000, row.AmountMinorUnits);
    }

    [Fact]
    public async Task Handle_excludes_a_card_installment_due_next_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        await SeedPlansAsync([CreateCardPlan(cardId, dueNextMonthPurchase, 1, 10_000, Currency.Reference)], cancellationToken);
        Assert.Empty((await RunAsync(fixedNow, cancellationToken)).Rows);
    }

    [Fact]
    public async Task Handle_excludes_a_paid_card_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        var plan = CreateCardPlan(cardId, overduePurchase, 2, 20_000, Currency.Reference);
        var first = plan.Installments.OrderBy(installment => installment.Sequence).First();
        first.ApplyPayment(first.Amount.MinorUnits, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        await SeedPlansAsync([plan], cancellationToken);
        var row = Assert.Single((await RunAsync(fixedNow, cancellationToken)).Rows);
        Assert.Equal(10_000, row.AmountMinorUnits);
    }

    [Fact]
    public async Task Handle_excludes_a_reversed_card_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        var plan = CreateCardPlan(cardId, overduePurchase, 2, 20_000, Currency.Reference);
        plan.Installments.OrderBy(installment => installment.Sequence).First().MarkReversed();
        await SeedPlansAsync([plan], cancellationToken);
        var row = Assert.Single((await RunAsync(fixedNow, cancellationToken)).Rows);
        Assert.Equal(10_000, row.AmountMinorUnits);
    }

    [Fact]
    public async Task Handle_emits_separate_rows_for_ars_and_usd_cards_and_plans() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var arsCard = await SeedCardAsync("Amex", cancellationToken);
        var usdCard = await SeedCardAsync("Visa", cancellationToken);
        await SeedPlansAsync([
            CreateCardPlan(arsCard, overduePurchase, 1, 5_000, Currency.Reference),
            CreateCardPlan(usdCard, overduePurchase, 1, 3_000, Currency.Usd),
            // Same card, second currency -> its own row.
            CreateCardPlan(arsCard, overduePurchase, 1, 2_000, Currency.Usd)
        ], cancellationToken);
        var rows = (await RunAsync(fixedNow, cancellationToken)).Rows;
        Assert.Equal(3, rows.Count);
        Assert.Equal(5_000, rows.Single(row => row.SourceId == arsCard && row.CurrencyCode == "ARS").AmountMinorUnits);
        Assert.Equal(2_000, rows.Single(row => row.SourceId == arsCard && row.CurrencyCode == "USD").AmountMinorUnits);
        Assert.Equal(3_000, rows.Single(row => row.SourceId == usdCard && row.CurrencyCode == "USD").AmountMinorUnits);
    }

    [Fact]
    public async Task Handle_counts_only_the_remaining_amount_of_a_partly_paid_creditor_installment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Felipe", ["Felipe Bank"], cancellationToken);
        var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], overduePurchase, 2, 20_000, Currency.Reference);
        plan.Installments.OrderBy(installment => installment.Sequence).First().ApplyPayment(4_000, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        await SeedPlansAsync([plan], cancellationToken);
        var row = Assert.Single((await RunAsync(fixedNow, cancellationToken)).Rows);
        Assert.Equal("creditor", row.Kind);
        Assert.Equal(creditor.CreditorId, row.SourceId);
        Assert.Equal("Felipe", row.SourceName);
        Assert.Equal(16_000, row.AmountMinorUnits);
    }

    [Fact]
    public async Task Handle_counts_creditor_installments_due_by_the_current_month_and_ignores_later_ones() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Calendar", ["Calendar Bank"], cancellationToken);
        await SeedPlansAsync([
            CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], overduePurchase, 1, 10_000, Currency.Reference),
            CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], dueNextMonthPurchase, 1, 7_000, Currency.Reference)
        ], cancellationToken);
        var row = Assert.Single((await RunAsync(fixedNow, cancellationToken)).Rows);
        Assert.Equal(10_000, row.AmountMinorUnits);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(27)]
    public async Task Handle_ignores_the_creditor_cutoff_day_and_never_pulls_later_months_in(int dayOfMonth) {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Cutoff", ["Cutoff Bank"], cancellationToken);
        // May 10 purchase -> due Jun (counted). Jul 10 purchase -> due Aug (later months stay out, even after the 26th).
        await SeedPlansAsync([
            CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 5, 10), 1, 10_000, Currency.Reference),
            CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], new DateOnly(2026, 7, 10), 1, 4_000, Currency.Reference)
        ], cancellationToken);
        var rows = (await RunAsync(new DateTimeOffset(2026, 6, dayOfMonth, 0, 0, 0, TimeSpan.Zero), cancellationToken)).Rows;
        Assert.Equal(10_000, Assert.Single(rows).AmountMinorUnits);
    }

    [Fact]
    public async Task Handle_omits_sources_whose_due_amount_is_zero() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var paidOff = await SeedCreditorAsync("PaidOff", ["PaidOff Bank"], cancellationToken);
        var cardId = await SeedCardAsync("Idle", cancellationToken);
        var paidPlan = CreateCreditorPlan(paidOff.CreditorId, paidOff.AccountIds[0], overduePurchase, 1, 10_000, Currency.Reference);
        var installment = paidPlan.Installments.Single();
        installment.ApplyPayment(installment.Amount.MinorUnits, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        await SeedPlansAsync([paidPlan, CreateCardPlan(cardId, dueNextMonthPurchase, 1, 1_000, Currency.Reference)], cancellationToken);
        Assert.Empty((await RunAsync(fixedNow, cancellationToken)).Rows);
    }

    [Fact]
    public async Task Handle_treats_an_explicit_current_month_like_no_month() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        var plan = CreateCardPlan(cardId, overduePurchase, 2, 20_000, Currency.Reference);
        var first = plan.Installments.OrderBy(installment => installment.Sequence).First();
        first.ApplyPayment(first.Amount.MinorUnits, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await SeedPlansAsync([plan, CreateCardPlan(cardId, dueNextMonthPurchase, 1, 1_000, Currency.Reference)], cancellationToken);
        var implicitRow = Assert.Single((await RunAsync(fixedNow, cancellationToken)).Rows);
        var explicitRow = Assert.Single((await RunAsync(fixedNow, cancellationToken, new DateOnly(2026, 6, 1))).Rows);
        Assert.Equal(10_000, implicitRow.AmountMinorUnits);
        Assert.Equal(implicitRow, explicitRow);
    }

    [Fact]
    public async Task Handle_for_the_local_current_month_excludes_installments_paid_this_month_when_utc_already_rolled_over() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        var plan = CreateCardPlan(cardId, new DateOnly(2026, 8, 10), 1, 5_000, Currency.Reference); // due Sep
        plan.Installments.Single().ApplyPayment(5_000, new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));
        await SeedPlansAsync([plan], cancellationToken);
        // Sep 30 23:03 in UTC-3 is already Oct 1 02:03 UTC; the client asks for September.
        var utcRolledOver = new DateTimeOffset(2026, 10, 1, 2, 3, 0, TimeSpan.Zero);
        Assert.Empty((await RunAsync(utcRolledOver, cancellationToken, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30))).Rows);
    }

    [Fact]
    public async Task Handle_for_a_future_month_counts_earlier_unpaid_and_that_month_but_not_later_ones() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        await SeedPlansAsync([
            CreateCardPlan(cardId, overduePurchase, 1, 5_000, Currency.Reference),                    // due Feb, unpaid
            CreateCardPlan(cardId, new DateOnly(2026, 7, 10), 1, 3_000, Currency.Reference),          // due Aug (= M)
            CreateCardPlan(cardId, new DateOnly(2026, 8, 10), 1, 9_000, Currency.Reference)           // due Sep (after M)
        ], cancellationToken);
        var row = Assert.Single((await RunAsync(fixedNow, cancellationToken, new DateOnly(2026, 8, 1))).Rows);
        Assert.Equal(8_000, row.AmountMinorUnits);
    }

    [Fact]
    public async Task Handle_for_a_future_month_excludes_an_installment_already_paid_today() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        var paid = CreateCardPlan(cardId, overduePurchase, 1, 5_000, Currency.Reference);
        paid.Installments.Single().ApplyPayment(5_000, new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero));
        await SeedPlansAsync([paid, CreateCardPlan(cardId, new DateOnly(2026, 7, 10), 1, 3_000, Currency.Reference)], cancellationToken);
        var row = Assert.Single((await RunAsync(fixedNow, cancellationToken, new DateOnly(2026, 8, 1))).Rows);
        Assert.Equal(3_000, row.AmountMinorUnits);
    }

    [Fact]
    public async Task Handle_for_a_past_month_counts_installments_paid_during_or_after_it_but_not_before_it() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        var paidBefore = CreateCardPlan(cardId, overduePurchase, 1, 1_000, Currency.Reference);          // due Feb
        paidBefore.Installments.Single().ApplyPayment(1_000, new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero));
        var paidDuring = CreateCardPlan(cardId, new DateOnly(2026, 2, 10), 1, 2_000, Currency.Reference); // due Mar
        paidDuring.Installments.Single().ApplyPayment(2_000, new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero));
        var paidAfter = CreateCardPlan(cardId, new DateOnly(2026, 1, 12), 1, 4_000, Currency.Reference); // due Feb
        paidAfter.Installments.Single().ApplyPayment(4_000, new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero));
        await SeedPlansAsync([paidBefore, paidDuring, paidAfter], cancellationToken);
        var row = Assert.Single((await RunAsync(fixedNow, cancellationToken, new DateOnly(2026, 3, 1))).Rows);
        Assert.Equal(6_000, row.AmountMinorUnits);
    }

    [Fact]
    public async Task Handle_for_a_past_month_only_subtracts_creditor_payments_made_before_that_month_began() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var creditor = await SeedCreditorAsync("Felipe", ["Felipe Bank"], cancellationToken);
        var plan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], overduePurchase, 1, 10_000, Currency.Reference);
        var installment = plan.Installments.Single();
        installment.ApplyPayment(4_000, new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero)); // before March
        installment.ApplyPayment(3_000, new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero)); // after March
        await SeedPlansAsync([plan], cancellationToken);
        var row = Assert.Single((await RunAsync(fixedNow, cancellationToken, new DateOnly(2026, 3, 1))).Rows);
        Assert.Equal(6_000, row.AmountMinorUnits);
    }

    [Fact]
    public async Task Handle_for_a_future_month_never_counts_reversed_installments() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        var creditor = await SeedCreditorAsync("Felipe", ["Felipe Bank"], cancellationToken);
        var cardPlan = CreateCardPlan(cardId, overduePurchase, 1, 5_000, Currency.Reference);
        cardPlan.Installments.Single().MarkReversed();
        var creditorPlan = CreateCreditorPlan(creditor.CreditorId, creditor.AccountIds[0], overduePurchase, 1, 7_000, Currency.Reference);
        creditorPlan.Installments.Single().MarkReversed();
        await SeedPlansAsync([cardPlan, creditorPlan], cancellationToken);
        Assert.Empty((await RunAsync(fixedNow, cancellationToken, new DateOnly(2026, 8, 1))).Rows);
    }

    [Fact]
    public async Task Handle_for_a_non_current_month_keeps_ars_and_usd_separate() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cardId = await SeedCardAsync("Visa", cancellationToken);
        await SeedPlansAsync([
            CreateCardPlan(cardId, overduePurchase, 1, 5_000, Currency.Reference),
            CreateCardPlan(cardId, new DateOnly(2026, 7, 10), 1, 3_000, Currency.Usd)
        ], cancellationToken);
        var rows = (await RunAsync(fixedNow, cancellationToken, new DateOnly(2026, 8, 1))).Rows;
        Assert.Equal(5_000, rows.Single(row => row.CurrencyCode == "ARS").AmountMinorUnits);
        Assert.Equal(3_000, rows.Single(row => row.CurrencyCode == "USD").AmountMinorUnits);
    }

    private static PaymentPlan CreateCreditorPlan(Guid creditorId, Guid creditorAccountId, DateOnly purchaseDate, int installmentCount, long totalMinorUnits, Currency currency) {
        return PaymentPlan.Create(
            cardId: null,
            Money.FromMinorUnits(totalMinorUnits, currency),
            installmentCount,
            purchaseDate,
            "Creditor purchase",
            cutoffDay: null,
            new PhantomPennyAllocator(),
            splitParticipants: null,
            creditorId: creditorId,
            creditorAccountId: creditorAccountId
        ).Value;
    }

    private static PaymentPlan CreateCardPlan(Guid cardId, DateOnly purchaseDate, int installmentCount, long totalMinorUnits, Currency currency) {
        return PaymentPlan.Create(
            cardId,
            Money.FromMinorUnits(totalMinorUnits, currency),
            installmentCount,
            purchaseDate,
            "Card purchase",
            15,
            new PhantomPennyAllocator()
        ).Value;
    }

    private async Task<Guid> SeedCardAsync(string name, CancellationToken cancellationToken) {
        await using var context = NewContext();
        var card = CreditCard.Create(Guid.CreateVersion7(), name, 15, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
        context.CreditCards.Add(card);
        await context.SaveChangesAsync(cancellationToken);
        return card.Id;
    }

    private async Task SeedPlansAsync(IEnumerable<PaymentPlan> plans, CancellationToken cancellationToken) {
        await using var context = NewContext();
        context.PaymentPlans.AddRange(plans);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<DueThisMonthResponse> RunAsync(DateTimeOffset now, CancellationToken cancellationToken, DateOnly? month = null, DateOnly? today = null) {
        await using var context = NewContext();
        return await new GetDueThisMonthHandler(context, new FixedTimeProvider(now)).HandleAsync(new GetDueThisMonthQuery(month, today), cancellationToken);
    }

    private async Task<(Guid CreditorId, IReadOnlyList<Guid> AccountIds, CreditorRow Row)> SeedCreditorAsync(string name, string[] accountLabels, CancellationToken cancellationToken) {
        await using(var context = NewContext()) {
            var accounts = accountLabels.Select(label => new CreditorAccountPayload(label, null)).ToList();
            await new CreateCreditorHandler(context).HandleAsync(new CreateCreditorCommand(name, accounts), cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new ListCreditorsHandler(readContext).HandleAsync(new ListCreditorsQuery(), cancellationToken);
        var row = response.Rows.Single(candidate => candidate.Name == name);
        return (row.Id, row.Accounts.Select(account => account.Id).ToList(), row);
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
