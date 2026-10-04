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
        builder.Property(split => split.TotalMinorUnits).HasColumnName("TotalMinorUnits").IsRequired();
        builder.Property(split => split.HolderShareMinorUnits).HasColumnName("HolderShareMinorUnits").IsRequired();
        builder.Property(split => split.AccruedReceivableMinorUnits).HasColumnName("AccruedReceivableMinorUnits").IsRequired();
        builder.Property(split => split.ReversedReceivableMinorUnits).HasColumnName("ReversedReceivableMinorUnits").IsRequired();
        builder.Property(split => split.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code))
            .HasColumnName("CurrencyCode")
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
        builder.Ignore(split => split.Total);
        builder.Ignore(split => split.HolderShare);
        builder.Ignore(split => split.AccruedReceivable);
        builder.Ignore(split => split.ReversedReceivable);
    }
}
