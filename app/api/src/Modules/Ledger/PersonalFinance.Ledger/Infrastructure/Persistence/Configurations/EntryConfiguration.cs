using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Configurations;

internal sealed class EntryConfiguration : IEntityTypeConfiguration<Entry> {
    public void Configure(EntityTypeBuilder<Entry> builder) {
        builder.ToTable("ledger_entries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.AccountId).IsRequired();
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(entry => entry.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(entry => entry.Direction).HasConversion<string>().IsRequired();
        builder.Property(entry => entry.AmountMinorUnits).HasColumnName("AmountMinorUnits").IsRequired();
        builder.Property(entry => entry.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code))
            .HasColumnName("CurrencyCode")
            .IsRequired();
        builder.Ignore(entry => entry.Amount);
    }
}
