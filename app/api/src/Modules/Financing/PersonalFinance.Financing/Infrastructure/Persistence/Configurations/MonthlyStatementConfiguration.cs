using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Configurations;

internal sealed class MonthlyStatementConfiguration : IEntityTypeConfiguration<MonthlyStatement> {
    public void Configure(EntityTypeBuilder<MonthlyStatement> builder) {
        builder.ToTable("financing_monthly_statements");
        builder.HasKey(statement => statement.Id);
        builder.Property(statement => statement.Id).ValueGeneratedNever();
        builder.Property(statement => statement.CardId).IsRequired();
        builder.Property(statement => statement.CycleYear).IsRequired();
        builder.Property(statement => statement.CycleMonth).IsRequired();
        builder.Property(statement => statement.PaidOnUtc);
        builder.Property(statement => statement.AmountDue)
            .HasConversion(
                amount => amount.MinorUnits,
                value => Money.FromMinorUnits(value, Currency.Reference))
            .HasColumnName("AmountDueMinorUnits")
            .IsRequired();
        builder.HasIndex(statement => new { statement.CardId, statement.CycleYear, statement.CycleMonth }).IsUnique();
        builder.Ignore(statement => statement.IsPaid);
        builder.Ignore(statement => statement.Cycle);
        builder.Ignore(statement => statement.DomainEvents);
    }
}
