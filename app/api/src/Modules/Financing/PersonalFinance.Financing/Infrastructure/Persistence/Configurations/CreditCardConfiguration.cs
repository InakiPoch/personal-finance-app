using Microsoft.EntityFrameworkCore;
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
        builder.Property(card => card.CarriedCreditBalance)
            .HasConversion(
                amount => amount.MinorUnits,
                value => Money.FromMinorUnits(value, Currency.Reference))
            .HasColumnName("CarriedCreditBalanceMinorUnits")
            .IsRequired();
        builder.Ignore(card => card.DomainEvents);
    }
}
