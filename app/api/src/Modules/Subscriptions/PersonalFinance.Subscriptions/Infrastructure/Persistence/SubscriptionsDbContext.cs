using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Subscriptions.Domain;

namespace PersonalFinance.Subscriptions.Infrastructure.Persistence;

internal sealed class SubscriptionsDbContext(DbContextOptions<SubscriptionsDbContext> options, ISqliteConnectionFactory connectionFactory) : ModuleDbContextBase(options, connectionFactory) {
    public DbSet<SubscriptionTemplate> SubscriptionTemplates => Set<SubscriptionTemplate>();

    protected override string ModuleName => "Subscriptions";

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubscriptionsDbContext).Assembly);
    }
}
