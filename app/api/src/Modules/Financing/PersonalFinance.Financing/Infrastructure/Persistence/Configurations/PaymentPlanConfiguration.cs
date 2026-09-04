using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Configurations;

internal sealed class PaymentPlanConfiguration : IEntityTypeConfiguration<PaymentPlan> {
    public void Configure(EntityTypeBuilder<PaymentPlan> builder) {
        builder.ToTable("financing_payment_plans");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();
        builder.Property(plan => plan.CardId).IsRequired();
        builder.Property(plan => plan.PurchaseDate).IsRequired();
        builder.Property(plan => plan.InstallmentCount).IsRequired();
        builder.Property(plan => plan.SplitReferenceId);
        builder.Property(plan => plan.CreditorId);
        builder.Property(plan => plan.CreditorAccountId);
        builder.Property(plan => plan.Total)
            .HasConversion(
                amount => amount.MinorUnits,
                value => Money.FromMinorUnits(value, Currency.Reference))
            .HasColumnName("TotalMinorUnits")
            .IsRequired();
        builder.HasMany(plan => plan.Installments)
            .WithOne()
            .HasForeignKey("PaymentPlanId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(plan => plan.Installments)
            .HasField("installments")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(plan => plan.SplitParticipants)
            .WithOne()
            .HasForeignKey(participant => participant.PaymentPlanId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(plan => plan.SplitParticipants)
            .HasField("splitParticipants")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(plan => plan.DomainEvents);
    }
}
