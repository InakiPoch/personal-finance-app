using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Infrastructure.Persistence;

internal sealed class FinancingDbContextFactory : IDesignTimeDbContextFactory<FinancingDbContext> {
    public FinancingDbContext CreateDbContext(string[] args) {
        var connectionString = SqliteConnectionStringHelper.ResolveForDesignTime();
        var options = new DbContextOptionsBuilder<FinancingDbContext>()
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Financing"))
            .Options;
        return new FinancingDbContext(options, new DesignTimeSqliteConnectionFactory());
    }
}
