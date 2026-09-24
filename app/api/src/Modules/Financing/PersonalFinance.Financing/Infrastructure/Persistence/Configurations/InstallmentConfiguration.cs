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
        builder.Property(installment => installment.SplitAccruedOnUtc);
        builder.Property(installment => installment.PaidOnUtc);
        builder.Property(installment => installment.StatementId);
        builder.Property(installment => installment.IsReversed)
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(installment => installment.AmountMinorUnits).HasColumnName("AmountMinorUnits").IsRequired();
        builder.Property(installment => installment.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code))
            .HasColumnName("CurrencyCode")
            .IsRequired();
        builder.Ignore(installment => installment.Amount);
        builder.HasOne<MonthlyStatement>()
            .WithMany()
            .HasForeignKey(installment => installment.StatementId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(installment => installment.IsAccrued);
        builder.Ignore(installment => installment.IsSplitAccrued);
        builder.Ignore(installment => installment.IsPaid);
        builder.Ignore(installment => installment.Cycle);
        builder.Ignore(installment => installment.DueCycle);
    }
}
