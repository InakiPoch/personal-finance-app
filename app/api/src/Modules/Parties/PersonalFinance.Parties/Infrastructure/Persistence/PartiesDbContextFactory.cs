using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Parties.Infrastructure.Persistence;

internal sealed class PartiesDbContextFactory : IDesignTimeDbContextFactory<PartiesDbContext> {
    public PartiesDbContext CreateDbContext(string[] args) {
        var connectionString = SqliteConnectionStringHelper.ResolveForDesignTime();
        var options = new DbContextOptionsBuilder<PartiesDbContext>()
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Parties"))
            .Options;
        return new PartiesDbContext(options, new DesignTimeSqliteConnectionFactory());
    }
}
