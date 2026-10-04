using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Financing.Domain;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Configurations;

internal sealed class CreditorInstallmentPaymentConfiguration : IEntityTypeConfiguration<CreditorInstallmentPayment> {
    public void Configure(EntityTypeBuilder<CreditorInstallmentPayment> builder) {
        builder.ToTable("financing_creditor_installment_payments");
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.InstallmentId).IsRequired();
        builder.Property(payment => payment.AmountMinorUnits).IsRequired();
        builder.Property(payment => payment.PaidOnUtc).IsRequired();
        builder.Property(payment => payment.PartyId);
        builder.Property(payment => payment.SettlementTransactionId);
        builder.HasIndex(payment => payment.InstallmentId);
    }
}
