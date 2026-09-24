using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalFinance.Abstractions.Modularity;
using PersonalFinance.Infrastructure.DependencyInjection;
using PersonalFinance.Ledger;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Contracts.Queries;
using Xunit;

namespace PersonalFinance.Subscriptions.Tests;

/// <summary>
/// Proves the one-time data migration (slice-3-subscriptions-and-migration.md, "THE FLIP"): a
/// pre-existing ARS subscription's template and ledger-posted charge both become USD, while the
/// shared funding account keeps its unrelated ARS activity untouched (poly-currency, no FX).
/// </summary>
public sealed class SubscriptionCurrencyFlipMigrationTests : IAsyncLifetime {
    private const string SubscriptionsPreFlipMigration = "20260924191455_AddSubscriptionCurrencyCode";
    private const string LedgerPreFlipMigration = "20260924183837_RebuildCardLiabilityAccruedView";
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly IModule[] modules = [new LedgerModule(), new SubscriptionsModule()];

    private IHost? host;
    private string databasePath = string.Empty;
    private IReadOnlyDictionary<string, Type> contextTypesByName = new Dictionary<string, Type>();

    public async ValueTask InitializeAsync() {
        databasePath = Path.Combine(Path.GetTempPath(), $"pf-currency-flip-{Guid.CreateVersion7():N}.db");
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(fixedNow));
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
        contextTypesByName = builder.Services
            .Select(descriptor => descriptor.ServiceType)
            .Where(type => type.IsClass && typeof(DbContext).IsAssignableFrom(type))
            .Distinct()
            .ToDictionary(type => type.Name);
        host = builder.Build();
    }

    public ValueTask DisposeAsync() {
        host?.Dispose();
        foreach(var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" }) {
            TryDelete(databasePath + suffix);
        }
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Flip_turns_the_existing_subscription_usd_and_leaves_the_funding_account_poly_currency() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await MigrateAsync("SubscriptionsDbContext", SubscriptionsPreFlipMigration);
        await MigrateAsync("LedgerDbContext", LedgerPreFlipMigration);

        var funding = await CreateAccountAsync("Bank", AccountType.Asset, AccountKind.Bank, cancellationToken);
        var groceries = await CreateAccountAsync("Groceries", AccountType.Expense, AccountKind.Expense, cancellationToken);
        await PostAsync(groceries, funding, 5_000, cancellationToken);
        var createResult = await CreateSubscriptionAsync(funding, cancellationToken);
        Assert.True(createResult.IsSuccess, $"CreateSubscriptionTemplate failed: {createResult.Error}");

        await MigrateAsync("SubscriptionsDbContext", targetMigration: null);
        await MigrateAsync("LedgerDbContext", targetMigration: null);

        var template = Assert.Single(await ActiveSubscriptionsAsync(cancellationToken));
        Assert.Equal("USD", template.CurrencyCode);
        var balances = await BalancesAsync(funding, cancellationToken);
        Assert.Equal(2, balances.Count);
        Assert.Equal(-5_000, Assert.Single(balances, balance => balance.Currency == Currency.Reference).MinorUnits);
        Assert.Equal(-1_500, Assert.Single(balances, balance => balance.Currency == Currency.Usd).MinorUnits);
    }

    private async Task<Guid> CreateAccountAsync(string name, AccountType type, AccountKind kind, CancellationToken cancellationToken) {
        await using var scope = host!.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var result = await ledger.CreateAccountAsync(new CreateAccountCommand(name, type, kind), cancellationToken);
        Assert.True(result.IsSuccess, $"CreateAccount '{name}' failed: {result.Error}");
        return result.Value;
    }

    private async Task PostAsync(Guid debitAccountId, Guid creditAccountId, long amountMinorUnits, CancellationToken cancellationToken) {
        await using var scope = host!.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var amount = new Money(amountMinorUnits, Currency.Reference);
        var command = new PostTransactionCommand(
            [
                new PostTransactionLine(debitAccountId, DebitOrCredit.Debit, amount),
                new PostTransactionLine(creditAccountId, DebitOrCredit.Credit, amount)
            ],
            fixedNow);
        var result = await ledger.PostTransactionAsync(command, cancellationToken);
        Assert.True(result.IsSuccess, $"PostTransaction failed: {result.Error}");
    }

    private async Task<Result<Guid>> CreateSubscriptionAsync(Guid fundingAccountId, CancellationToken cancellationToken) {
        await using var scope = host!.Services.CreateAsyncScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionsApi>();
        return await subscriptions.CreateSubscriptionTemplateAsync(
            new CreateSubscriptionTemplateCommand("Netflix", 1_500, "Streaming", fundingAccountId, RecurrenceFrequency.Monthly, fixedNow.Day),
            cancellationToken);
    }

    private async Task<IReadOnlyList<ActiveSubscriptionRow>> ActiveSubscriptionsAsync(CancellationToken cancellationToken) {
        await using var scope = host!.Services.CreateAsyncScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionsApi>();
        var response = await subscriptions.GetActiveSubscriptionsAsync(new GetActiveSubscriptionsQuery(), cancellationToken);
        return response.Rows;
    }

    private async Task<IReadOnlyList<Money>> BalancesAsync(Guid accountId, CancellationToken cancellationToken) {
        await using var scope = host!.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        return await ledger.GetAccountBalanceAsync(new GetAccountBalanceQuery(accountId), cancellationToken);
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

    private async Task MigrateAsync(string contextTypeName, string? targetMigration) {
        await using var scope = host!.Services.CreateAsyncScope();
        var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextTypesByName[contextTypeName]);
        var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(targetMigration);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() {
            return now;
        }
    }
}
