using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Configurations;

internal sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard> {
    public void Configure(EntityTypeBuilder<CreditCard> builder) {
        builder.ToTable("financing_credit_cards");
        builder.HasKey(card => card.Id);
        builder.Property(card => card.Id).ValueGeneratedNever();
        builder.Property(card => card.Name).IsRequired();
        builder.Property(card => card.CutoffDay).IsRequired();
        builder.Property(card => card.LiabilityAccountId).IsRequired();
        builder.Property(card => card.ExpenseAccountId).IsRequired();
        builder.Property(card => card.CreditAccountId).IsRequired();
        builder.Property(card => card.CarriedCreditBalanceMinorUnits).HasColumnName("CarriedCreditBalanceMinorUnits").IsRequired();
        builder.Property(card => card.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code))
            .HasColumnName("CurrencyCode")
            .IsRequired();
        builder.HasMany(card => card.ClosingOverrides)
            .WithOne()
            .HasForeignKey(closingOverride => closingOverride.CardId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(card => card.ClosingOverrides)
            .HasField("closingOverrides")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(card => card.CarriedCreditBalance);
        builder.Ignore(card => card.DomainEvents);
    }
}
