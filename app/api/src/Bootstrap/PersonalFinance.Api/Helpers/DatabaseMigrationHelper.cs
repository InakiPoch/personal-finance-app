using Microsoft.EntityFrameworkCore;

namespace PersonalFinance.Api.Helpers;

/// <summary>
/// Discovers the module DbContexts registered in the host and applies their migrations in a fixed order.
/// </summary>
internal static class DatabaseMigrationHelper {
    private static readonly IReadOnlyDictionary<string, int> migrationOrder = new Dictionary<string, int> {
        ["LedgerDbContext"] = 0,
        ["FinancingDbContext"] = 1,
        ["SubscriptionsDbContext"] = 2,
        ["PartiesDbContext"] = 3
    };

    public static IReadOnlyList<Type> GetOrderedContextTypes(IServiceCollection services) {
        return services
            .Select(descriptor => descriptor.ServiceType)
            .Where(type => type.IsClass && typeof(DbContext).IsAssignableFrom(type))
            .Distinct()
            .OrderBy(type => migrationOrder.TryGetValue(type.Name, out var rank) ? rank : int.MaxValue)
        .ToList();
    }

    public static async Task MigrateAsync(IServiceProvider services, IReadOnlyList<Type> contextTypes, ILogger logger) {
        using var scope = services.CreateScope();
        foreach(var contextType in contextTypes) {
            try {
                var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrated for {Context}", contextType.Name);
            } catch(Exception exception) {
                logger.LogCritical(exception, "Database migration failed for {Context}; aborting startup", contextType.Name);
                throw;
            }
        }
    }
}
