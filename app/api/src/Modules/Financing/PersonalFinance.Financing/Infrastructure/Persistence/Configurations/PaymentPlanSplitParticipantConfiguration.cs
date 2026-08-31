using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Financing.Domain;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Configurations;

internal sealed class PaymentPlanSplitParticipantConfiguration : IEntityTypeConfiguration<PaymentPlanSplitParticipant> {
    public void Configure(EntityTypeBuilder<PaymentPlanSplitParticipant> builder) {
        builder.ToTable("financing_payment_plan_split_participants");
        builder.HasKey(participant => participant.Id);
        builder.Property(participant => participant.Id).ValueGeneratedNever();
        builder.Property(participant => participant.PaymentPlanId).IsRequired();
        builder.Property(participant => participant.PartyId).IsRequired();
        builder.Property(participant => participant.ReceivableAccountId).IsRequired();
        builder.Property(participant => participant.Weight).IsRequired();
        builder.HasIndex(participant => participant.PaymentPlanId);
    }
}
