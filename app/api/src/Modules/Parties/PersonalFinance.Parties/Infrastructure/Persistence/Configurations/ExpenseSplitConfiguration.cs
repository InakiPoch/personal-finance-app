using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseSplitConfiguration : IEntityTypeConfiguration<ExpenseSplit> {
    public void Configure(EntityTypeBuilder<ExpenseSplit> builder) {
        builder.ToTable("parties_expense_splits");
        builder.HasKey(split => split.Id);
        builder.Property(split => split.Id).ValueGeneratedNever();
        builder.Property(split => split.Source)
            .HasConversion<string>()
            .IsRequired();
        builder.Property(split => split.SourceReferenceId).IsRequired();
        builder.Property(split => split.Total)
            .HasConversion(
                amount => amount.MinorUnits,
                value => Money.FromMinorUnits(value, Currency.Reference))
            .HasColumnName("TotalMinorUnits")
            .IsRequired();
        builder.Property(split => split.HolderShare)
            .HasConversion(
                amount => amount.MinorUnits,
                value => Money.FromMinorUnits(value, Currency.Reference))
            .HasColumnName("HolderShareMinorUnits")
            .IsRequired();
        builder.Property(split => split.AccruedReceivable)
            .HasConversion(
                amount => amount.MinorUnits,
                value => Money.FromMinorUnits(value, Currency.Reference))
            .HasColumnName("AccruedReceivableMinorUnits")
            .IsRequired();
        builder.Property(split => split.ReversedReceivable)
            .HasConversion(
                amount => amount.MinorUnits,
                value => Money.FromMinorUnits(value, Currency.Reference))
            .HasColumnName("ReversedReceivableMinorUnits")
            .IsRequired();
        builder.HasMany(split => split.Participants)
            .WithOne()
            .HasForeignKey("ExpenseSplitId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(split => split.Participants)
            .HasField("participants")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(split => split.DomainEvents);
        builder.Ignore(split => split.PartyReceivableTotal);
    }
}
