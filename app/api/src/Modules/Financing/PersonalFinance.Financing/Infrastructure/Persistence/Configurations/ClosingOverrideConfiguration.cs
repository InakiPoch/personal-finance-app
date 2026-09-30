using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Financing.Domain;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Configurations;

internal sealed class ClosingOverrideConfiguration : IEntityTypeConfiguration<ClosingOverride> {
    public void Configure(EntityTypeBuilder<ClosingOverride> builder) {
        builder.ToTable("financing_card_closing_overrides");
        builder.HasKey(closingOverride => closingOverride.Id);
        builder.Property(closingOverride => closingOverride.Id).ValueGeneratedNever();
        builder.Property(closingOverride => closingOverride.CardId).IsRequired();
        builder.Property(closingOverride => closingOverride.CycleYear).IsRequired();
        builder.Property(closingOverride => closingOverride.CycleMonth).IsRequired();
        builder.Property(closingOverride => closingOverride.ClosingDay).IsRequired();
        builder.Ignore(closingOverride => closingOverride.Cycle);
        builder.HasIndex(closingOverride => new { closingOverride.CardId, closingOverride.CycleYear, closingOverride.CycleMonth }).IsUnique();
    }
}
