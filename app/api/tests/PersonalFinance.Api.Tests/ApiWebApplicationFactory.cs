using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using PersonalFinance.Api.Helpers;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime {
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"pf-api-{Guid.CreateVersion7():N}.db");

    public IReadOnlyList<Type> ContextTypes { get; private set; } = [];

    public async ValueTask InitializeAsync() {
        await DatabaseMigrationHelper.MigrateAsync(Services, ContextTypes, NullLogger.Instance);
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
            ContextTypes = DatabaseMigrationHelper.GetOrderedContextTypes(services);
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
