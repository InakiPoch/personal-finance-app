using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Ledger.Domain;

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
        builder.ComplexProperty(entry => entry.Amount, amount => {
            amount.Property(money => money.MinorUnits).HasColumnName("AmountMinorUnits").IsRequired();
            amount.ComplexProperty(money => money.Currency, currency => {
                currency.Property(unit => unit.Code).HasColumnName("AmountCurrencyCode").IsRequired();
                currency.Property(unit => unit.DecimalPlaces).HasColumnName("AmountCurrencyDecimals").IsRequired();
            });
        });
    }
}
