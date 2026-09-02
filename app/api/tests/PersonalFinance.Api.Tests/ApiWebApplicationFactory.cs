using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime {
    private static readonly IReadOnlyDictionary<string, int> migrationOrder = new Dictionary<string, int> {
        ["LedgerDbContext"] = 0,
        ["FinancingDbContext"] = 1,
        ["SubscriptionsDbContext"] = 2,
        ["PartiesDbContext"] = 3
    };

    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"pf-api-{Guid.CreateVersion7():N}.db");

    private IReadOnlyList<Type> contextTypes = [];

    public async ValueTask InitializeAsync() {
        using var scope = Services.CreateScope();
        foreach(var contextType in contextTypes) {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
            await context.Database.MigrateAsync();
        }
    }

    public override async ValueTask DisposeAsync() {
        await base.DisposeAsync();
        foreach(var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" }) {
            tryDelete(databasePath + suffix);
        }
        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseSetting("ConnectionStrings:PersonalFinanceDb", $"Data Source={databasePath}");
        builder.UseSetting("Sqlite:JournalMode", "DELETE");
        builder.UseSetting("Sqlite:BusyTimeoutMs", "5000");
        builder.UseSetting("Sqlite:ForeignKeys", "true");
        builder.ConfigureServices(services => {
            contextTypes = services
                .Select(descriptor => descriptor.ServiceType)
                .Where(type => type.IsClass && typeof(DbContext).IsAssignableFrom(type))
                .Distinct()
                .OrderBy(type => migrationOrder.TryGetValue(type.Name, out var rank) ? rank : int.MaxValue)
                .ToList();
            var appHostedServices = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.Namespace?.StartsWith("PersonalFinance.", StringComparison.Ordinal) == true)
                .ToList();
            foreach(var descriptor in appHostedServices) {
                services.Remove(descriptor);
            }
        });
    }

    private static void tryDelete(string path) {
        try {
            if(File.Exists(path)) {
                File.Delete(path);
            }
        } catch(IOException) {
            //...
        }
    }
}
