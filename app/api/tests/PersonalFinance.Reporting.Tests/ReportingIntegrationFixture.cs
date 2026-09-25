using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalFinance.Abstractions.Modularity;
using PersonalFinance.Financing;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Infrastructure.DependencyInjection;
using PersonalFinance.Ledger;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Parties;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions;
using Xunit;

namespace PersonalFinance.Reporting.Tests;

public sealed class ReportingIntegrationFixture : IAsyncLifetime {
    public IServiceProvider Services => host!.Services;
    public Guid AliceId { get; private set; }
    public Guid BobId { get; private set; }
    public Guid ReportingCardId { get; private set; }
    public long AliceOwed { get; private set; }
    public long BobOwed { get; private set; }

    private static readonly IModule[] modules = [
        new LedgerModule(),
        new FinancingModule(),
        new SubscriptionsModule(),
        new PartiesModule(),
        new ReportingModule()
    ];

    private static readonly IReadOnlyDictionary<string, int> migrationOrder = new Dictionary<string, int> {
        ["LedgerDbContext"] = 0,
        ["FinancingDbContext"] = 1,
        ["SubscriptionsDbContext"] = 2,
        ["PartiesDbContext"] = 3
    };

    private IHost? host;
    private string databasePath = string.Empty;

    public async ValueTask InitializeAsync() {
        databasePath = Path.Combine(Path.GetTempPath(), $"pf-reporting-{Guid.CreateVersion7():N}.db");
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:PersonalFinanceDb"] = $"Data Source={databasePath}",
            ["Sqlite:JournalMode"] = "DELETE",
            ["Sqlite:BusyTimeoutMs"] = "5000",
            ["Sqlite:ForeignKeys"] = "true"
        });
        builder.Services.AddSharedInfrastructure(builder.Configuration);
        foreach(var module in modules) {
            module.Register(builder.Services, builder.Configuration);
        }
        var contextTypes = DiscoverContextTypes(builder.Services);
        host = builder.Build();
        await MigrateAllAsync(contextTypes);
        await SeedAsync();
    }

    public ValueTask DisposeAsync() {
        host?.Dispose();
        foreach(var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" }) {
            TryDelete(databasePath + suffix);
        }
        return ValueTask.CompletedTask;
    }

    private static IReadOnlyList<Type> DiscoverContextTypes(IServiceCollection services) {
        return services
            .Select(descriptor => descriptor.ServiceType)
            .Where(type => type.IsClass && typeof(DbContext).IsAssignableFrom(type))
            .Distinct()
            .OrderBy(type => migrationOrder.TryGetValue(type.Name, out var rank) ? rank : int.MaxValue)
        .ToList();
    }

    private static void TryDelete(string path) {
        try {
            if(File.Exists(path)) {
                File.Delete(path);
            }
        }
        catch(IOException) {
        }
    }

    private async Task MigrateAllAsync(IReadOnlyList<Type> contextTypes) {
        foreach(var contextType in contextTypes) {
            await using var scope = host!.Services.CreateAsyncScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
            await context.Database.MigrateAsync();
        }
    }

    private async Task SeedAsync() {
        var bank = await CreateAccountAsync("Bank", AccountType.Asset, AccountKind.Bank);
        var cash = await CreateAccountAsync("Wallet", AccountType.Asset, AccountKind.Cash);
        var groceries = await CreateAccountAsync("Groceries", AccountType.Expense, AccountKind.Expense);
        var rent = await CreateAccountAsync("Rent", AccountType.Expense, AccountKind.Expense);
        var snacks = await CreateAccountAsync("Snacks", AccountType.Expense, AccountKind.Expense);
        var sharedDining = await CreateAccountAsync("Shared Dining", AccountType.Expense, AccountKind.Expense);
        ReportingCardId = await CreateCreditCardAsync("Visa Reporting", 15);
        var cardPurchases = await CreateAccountAsync("Card Purchases", AccountType.Expense, AccountKind.CardPurchases, ReportingCardId);
        var cardLiability = await CreateAccountAsync("Manual Card Liability", AccountType.Liability, AccountKind.CardLiability, ReportingCardId);
        await PostAsync("Groceries", At(5, 10), groceries, bank, 5_000);
        await PostAsync("Rent", At(5, 1), rent, bank, 30_000);
        await PostAsync("Snacks", At(5, 12), snacks, cash, 2_000);
        await PostAsync("Card accrual", At(5, 20), cardPurchases, cardLiability, 12_000);
        await CreatePaymentPlanAsync(300_000, ReportingCardId, 3, DateOnly.FromDateTime(DateTime.UtcNow));
        await CreatePaymentPlanAsync(60_000, ReportingCardId, 3, DateOnly.FromDateTime(DateTime.UtcNow), "USD");
        AliceId = await CreatePartyAsync("Alice Reporting");
        await RegisterSharedExpenseAsync("Alice dinner", 10_000, sharedDining, bank, At(5, 25), AliceId);
        AliceOwed = await GetPartyBalanceAsync(AliceId);
        Assert.True(AliceOwed > 0, $"Expected Alice to owe a positive amount, got {AliceOwed}.");
        await SettleAsync(AliceId, AliceOwed, bank, At(5, 28));
        BobId = await CreatePartyAsync("Bob Reporting");
        await RegisterSharedExpenseAsync("Bob concert", 8_000, sharedDining, bank, At(5, 27), BobId);
        BobOwed = await GetPartyBalanceAsync(BobId);
        Assert.True(BobOwed > 0, $"Expected Bob to owe a positive amount, got {BobOwed}.");
    }

    private static DateTimeOffset At(int month, int day) {
        return new DateTimeOffset(2026, month, day, 9, 0, 0, TimeSpan.Zero);
    }

    private async Task<Guid> CreateAccountAsync(string name, AccountType type, AccountKind kind, Guid? ownerReferenceId = null) {
        await using var scope = host!.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var result = await ledger.CreateAccountAsync(new CreateAccountCommand(name, type, kind, OwnerReferenceId: ownerReferenceId), CancellationToken.None);
        Assert.True(result.IsSuccess, $"CreateAccount '{name}' failed: {result.Error}");
        return result.Value;
    }

    private async Task PostAsync(string description, DateTimeOffset postedOnUtc, Guid debitAccountId, Guid creditAccountId, long amountMinorUnits) {
        await using var scope = host!.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var amount = new Money(amountMinorUnits, Currency.Reference);
        var command = new PostTransactionCommand(
            [
                new PostTransactionLine(debitAccountId, DebitOrCredit.Debit, amount),
                new PostTransactionLine(creditAccountId, DebitOrCredit.Credit, amount)
            ],
            postedOnUtc,
            Description: description);
        var result = await ledger.PostTransactionAsync(command, CancellationToken.None);
        Assert.True(result.IsSuccess, $"PostTransaction '{description}' failed: {result.Error}");
    }

    private async Task<Guid> CreateCreditCardAsync(string name, int cutoffDate) {
        await using var scope = host!.Services.CreateAsyncScope();
        var financing = scope.ServiceProvider.GetRequiredService<IFinancingApi>();
        var result = await financing.CreateCreditCardAsync(new CreateCreditCardCommand(name, cutoffDate), CancellationToken.None);
        Assert.True(result.IsSuccess, $"CreateCreditCard '{name}' failed: {result.Error}");
        return result.Value;
    }

    private async Task CreatePaymentPlanAsync(long amountMinorUnits, Guid cardId, int installmentCount, DateOnly purchaseDate, string currencyCode = "ARS") {
        await using var scope = host!.Services.CreateAsyncScope();
        var financing = scope.ServiceProvider.GetRequiredService<IFinancingApi>();
        var result = await financing.CreatePaymentPlanAsync(
            new CreatePaymentPlanCommand(amountMinorUnits, cardId, installmentCount, purchaseDate, "Reporting fixture purchase", CurrencyCode: currencyCode),
            CancellationToken.None);
        Assert.True(result.IsSuccess, $"CreatePaymentPlan failed: {result.Error}");
    }

    private async Task<Guid> CreatePartyAsync(string name) {
        await using var scope = host!.Services.CreateAsyncScope();
        var parties = scope.ServiceProvider.GetRequiredService<IPartiesApi>();
        var result = await parties.CreatePartyAsync(new CreatePartyCommand(name), CancellationToken.None);
        Assert.True(result.IsSuccess, $"CreateParty '{name}' failed: {result.Error}");
        return result.Value;
    }

    private async Task RegisterSharedExpenseAsync(string description, long totalMinorUnits, Guid expenseAccountId, Guid fundingAccountId, DateTimeOffset incurredOnUtc, Guid participantPartyId) {
        await using var scope = host!.Services.CreateAsyncScope();
        var parties = scope.ServiceProvider.GetRequiredService<IPartiesApi>();
        var command = new RegisterSharedExpenseCommand(
            description,
            totalMinorUnits,
            expenseAccountId,
            fundingAccountId,
            incurredOnUtc,
            [new SharedExpenseParticipant(participantPartyId, 1)]
        );
        var result = await parties.RegisterSharedExpenseAsync(command, CancellationToken.None);
        Assert.True(result.IsSuccess, $"RegisterSharedExpense '{description}' failed: {result.Error}");
    }

    private async Task<long> GetPartyBalanceAsync(Guid partyId) {
        await using var scope = host!.Services.CreateAsyncScope();
        var parties = scope.ServiceProvider.GetRequiredService<IPartiesApi>();
        var response = await parties.GetCurrentAccountBalanceAsync(new GetCurrentAccountBalanceQuery(partyId), CancellationToken.None);
        return response.Balances.FirstOrDefault(balance => balance.CurrencyCode == "ARS")?.BalanceMinorUnits ?? 0;
    }

    private async Task SettleAsync(Guid partyId, long amountMinorUnits, Guid bankAccountId, DateTimeOffset settledOnUtc) {
        await using var scope = host!.Services.CreateAsyncScope();
        var parties = scope.ServiceProvider.GetRequiredService<IPartiesApi>();
        var result = await parties.SettleCurrentAccountAsync(
            new SettleCurrentAccountCommand(partyId, amountMinorUnits, bankAccountId, settledOnUtc),
            CancellationToken.None
        );
        Assert.True(result.IsSuccess, $"Settle failed: {result.Error}");
    }
}
