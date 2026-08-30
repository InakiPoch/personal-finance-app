using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Ledger.Domain;

namespace PersonalFinance.Ledger.Infrastructure.Persistence;

/// <summary>
/// The Ledger module's unit of work.
/// </summary>
internal sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options, ISqliteConnectionFactory connectionFactory) : ModuleDbContextBase(options, connectionFactory) {
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override string ModuleName => "Ledger";

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);
    }
}
