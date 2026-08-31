using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Parties.Domain;

namespace PersonalFinance.Parties.Infrastructure.Persistence;

internal sealed class PartiesDbContext(DbContextOptions<PartiesDbContext> options, ISqliteConnectionFactory connectionFactory) : ModuleDbContextBase(options, connectionFactory) {
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<ExpenseSplit> ExpenseSplits => Set<ExpenseSplit>();

    protected override string ModuleName => "Parties";

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PartiesDbContext).Assembly);
    }
}
