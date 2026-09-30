using Microsoft.Data.Sqlite;
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
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Contracts.Commands;
using Xunit;

namespace PersonalFinance.Subscriptions.Tests;

/// <summary>
/// Proves the one-time rename (friendly-ui slice 4): a legacy "Netflix Expense" subscription account becomes
/// "Netflix Subscription", matched by the template's ExpenseAccountId so an unrelated "Office Expense" category
/// is untouched; Down restores the legacy name and re-running Up is a no-op on already-renamed rows.
/// </summary>
public sealed class SubscriptionAccountRenameMigrationTests : IAsyncLifetime {
    private const string SubscriptionsPreRenameMigration = "20260924191856_FlipSubscriptionsToUsd";
    private static readonly DateTimeOffset fixedNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly IModule[] modules = [new LedgerModule(), new SubscriptionsModule()];

    private IHost? host;
    private string databasePath = string.Empty;
    private IReadOnlyDictionary<string, Type> contextTypesByName = new Dictionary<string, Type>();

    public ValueTask InitializeAsync() {
        databasePath = Path.Combine(Path.GetTempPath(), $"pf-account-rename-{Guid.CreateVersion7():N}.db");
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
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() {
        host?.Dispose();
        SqliteConnection.ClearAllPools();
        foreach(var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" }) {
            TryDelete(databasePath + suffix);
        }
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Rename_turns_subscription_accounts_into_subscription_and_leaves_other_categories_alone() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await MigrateAsync("LedgerDbContext", targetMigration: null);
        await MigrateAsync("SubscriptionsDbContext", SubscriptionsPreRenameMigration);

        var funding = await CreateAccountAsync("Bank", AccountType.Asset, AccountKind.Bank, cancellationToken);
        await CreateAccountAsync("Office Expense", AccountType.Expense, AccountKind.Expense, cancellationToken);
        await CreateSubscriptionAsync("Netflix", funding, cancellationToken);
        await CreateSubscriptionAsync("Spotify", funding, cancellationToken);
        // Simulate the legacy data: Netflix still carries the old suffix, Spotify was already named by the user.
        await ExecuteAsync("UPDATE ledger_accounts SET Name = 'Netflix Expense' WHERE Name = 'Netflix Subscription'");
        await ExecuteAsync("UPDATE ledger_accounts SET Name = 'Spotify Family' WHERE Name = 'Spotify Subscription'");

        await MigrateAsync("SubscriptionsDbContext", targetMigration: null);

        var names = await ExpenseAccountNamesAsync();
        Assert.Contains("Netflix Subscription", names);
        Assert.DoesNotContain("Netflix Expense", names);
        Assert.Contains("Spotify Family", names);
        Assert.Contains("Office Expense", names);

        await MigrateAsync("SubscriptionsDbContext", SubscriptionsPreRenameMigration);
        names = await ExpenseAccountNamesAsync();
        Assert.Contains("Netflix Expense", names);
        Assert.Contains("Office Expense", names);

        await MigrateAsync("SubscriptionsDbContext", targetMigration: null);
        Assert.Contains("Netflix Subscription", await ExpenseAccountNamesAsync());
    }

    private async Task<Guid> CreateAccountAsync(string name, AccountType type, AccountKind kind, CancellationToken cancellationToken) {
        await using var scope = host!.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var result = await ledger.CreateAccountAsync(new CreateAccountCommand(name, type, kind), cancellationToken);
        Assert.True(result.IsSuccess, $"CreateAccount '{name}' failed: {result.Error}");
        return result.Value;
    }

    private async Task CreateSubscriptionAsync(string name, Guid fundingAccountId, CancellationToken cancellationToken) {
        await using var scope = host!.Services.CreateAsyncScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionsApi>();
        var result = await subscriptions.CreateSubscriptionTemplateAsync(
            new CreateSubscriptionTemplateCommand(name, 1_500, "Streaming", fundingAccountId, RecurrenceFrequency.Monthly, fixedNow.Day),
            cancellationToken);
        Assert.True(result.IsSuccess, $"CreateSubscriptionTemplate '{name}' failed: {result.Error}");
    }

    private async Task ExecuteAsync(string sql) {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=false");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<string>> ExpenseAccountNamesAsync() {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=false");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM ledger_accounts WHERE Name <> 'Bank'";
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while(await reader.ReadAsync()) {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    private async Task MigrateAsync(string contextTypeName, string? targetMigration) {
        await using var scope = host!.Services.CreateAsyncScope();
        var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextTypesByName[contextTypeName]);
        var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(targetMigration);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() {
            return now;
        }
    }
}
