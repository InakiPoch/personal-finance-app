using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Configurations;

internal sealed class InstallmentConfiguration : IEntityTypeConfiguration<Installment> {
    public void Configure(EntityTypeBuilder<Installment> builder) {
        builder.ToTable("financing_installments");
        builder.HasKey(installment => installment.Id);
        builder.Property(installment => installment.Id).ValueGeneratedNever();
        builder.Property(installment => installment.PaymentPlanId).IsRequired();
        builder.Property(installment => installment.Sequence).IsRequired();
        builder.Property(installment => installment.CycleYear).IsRequired();
        builder.Property(installment => installment.CycleMonth).IsRequired();
        builder.Property(installment => installment.AccruedOnUtc);
        builder.Property(installment => installment.StatementId);
        builder.Property(installment => installment.IsReversed)
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(installment => installment.Amount)
            .HasConversion(
                amount => amount.MinorUnits,
                value => Money.FromMinorUnits(value, Currency.Reference))
            .HasColumnName("AmountMinorUnits")
            .IsRequired();
        builder.HasOne<MonthlyStatement>()
            .WithMany()
            .HasForeignKey(installment => installment.StatementId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(installment => installment.IsAccrued);
        builder.Ignore(installment => installment.Cycle);
    }
}
