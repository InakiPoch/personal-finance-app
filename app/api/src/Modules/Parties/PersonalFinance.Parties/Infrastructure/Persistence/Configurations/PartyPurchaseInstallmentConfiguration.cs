using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Infrastructure.Persistence.Configurations;

internal sealed class PartyPurchaseInstallmentConfiguration : IEntityTypeConfiguration<PartyPurchaseInstallment> {
    public void Configure(EntityTypeBuilder<PartyPurchaseInstallment> builder) {
        builder.ToTable("parties_purchase_installments");
        builder.HasKey(installment => installment.Id);
        builder.Property(installment => installment.Id).ValueGeneratedNever();
        builder.Property(installment => installment.Number).IsRequired();
        builder.Property(installment => installment.DueOn).IsRequired();
        builder.Property(installment => installment.AmountMinorUnits).HasColumnName("AmountMinorUnits").IsRequired();
        builder.Property(installment => installment.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code))
            .HasColumnName("CurrencyCode")
        .IsRequired();
        builder.HasIndex(installment => installment.LedgerTransactionId);
    }
}
