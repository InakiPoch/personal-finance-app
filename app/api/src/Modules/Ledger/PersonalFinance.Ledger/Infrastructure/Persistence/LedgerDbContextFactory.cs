using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PersonalFinance.Ledger.Infrastructure.Persistence;

/// <summary>
/// Construction for <c>dotnet ef</c>. Uses an explicit connection string (env
/// <c>PF_SQLITE_CONNECTION</c>, else the shared <c>personalfinance.db</c> beside <c>PersonalFinance.sln</c>)
/// so <c>IsConfigured</c> is true and the runtime connection factory is never touched.
/// </summary>
internal sealed class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext> {
    public LedgerDbContext CreateDbContext(string[] args) {
        var connectionString = Environment.GetEnvironmentVariable("PF_SQLITE_CONNECTION") ?? $"Data Source={Path.Combine(SolutionRootLocatorHelper.FindSolutionRoot(), "personalfinance.db")}";
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Ledger"))
            .Options;
        return new LedgerDbContext(options, new DesignTimeSqliteConnectionFactory());
    }
}
