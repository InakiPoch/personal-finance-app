using Microsoft.EntityFrameworkCore;

namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// Base <see cref="DbContext"/> for a module.
/// </summary>
public abstract class ModuleDbContextBase(DbContextOptions options, ISqliteConnectionFactory connectionFactory) : DbContext(options) {
    protected abstract string ModuleName { get; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        if(!optionsBuilder.IsConfigured) {
            optionsBuilder.UseSqlite(
                connectionFactory.CreateOpenConnection(),
                contextOwnsConnection: true,
                sqlite => sqlite.MigrationsHistoryTable($"__EFMigrationsHistory_{ModuleName}"));
        }
        base.OnConfiguring(optionsBuilder);
    }
}
