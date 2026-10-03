using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Api.Helpers;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class DatabaseMigrationHelperTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task MigrateAsync_clears_a_stale_migration_lock_instead_of_hanging() {
        var cancellationToken = TestContext.Current.CancellationToken;
        using(var scope = factory.Services.CreateScope()) {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(factory.ContextTypes[0]);
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"__EFMigrationsLock\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK___EFMigrationsLock\" PRIMARY KEY, \"Timestamp\" TEXT NOT NULL)",
                cancellationToken
            );
            await context.Database.ExecuteSqlRawAsync(
                "INSERT OR IGNORE INTO \"__EFMigrationsLock\"(\"Id\", \"Timestamp\") VALUES (1, '2026-01-01 00:00:00')",
                cancellationToken
            );
        }
        await DatabaseMigrationHelper.MigrateAsync(factory.Services, factory.ContextTypes, NullLogger.Instance).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }
}
