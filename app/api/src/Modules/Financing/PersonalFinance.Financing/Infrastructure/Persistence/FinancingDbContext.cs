using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Infrastructure.Persistence;

internal sealed class FinancingDbContext(DbContextOptions<FinancingDbContext> options, ISqliteConnectionFactory connectionFactory) : ModuleDbContextBase(options, connectionFactory) {
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<PaymentPlan> PaymentPlans => Set<PaymentPlan>();
    public DbSet<MonthlyStatement> MonthlyStatements => Set<MonthlyStatement>();

    protected override string ModuleName => "Financing";

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinancingDbContext).Assembly);
    }
}
