using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Domain;

namespace PersonalFinance.Subscriptions.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionTemplateConfiguration : IEntityTypeConfiguration<SubscriptionTemplate> {
    public void Configure(EntityTypeBuilder<SubscriptionTemplate> builder) {
        builder.ToTable("subscriptions_templates");
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Id).ValueGeneratedNever();
        builder.Property(template => template.Name).IsRequired();
        builder.Property(template => template.Category).IsRequired();
        builder.Property(template => template.ExpenseAccountId).IsRequired();
        builder.Property(template => template.FundingAccountId).IsRequired();
        builder.Property(template => template.Frequency)
            .HasConversion<string>()
            .IsRequired();
        builder.Property(template => template.AnchorDay).IsRequired();
        builder.Property(template => template.NextDueDate).IsRequired();
        builder.Property(template => template.IsActive)
            .HasDefaultValue(true)
            .IsRequired();
        builder.Property(template => template.LastPaidPeriod);
        builder.Property(template => template.LastPaidTransactionId);
        builder.Property(template => template.Amount)
            .HasConversion(
                amount => amount.MinorUnits,
                value => Money.FromMinorUnits(value, Currency.Reference))
            .HasColumnName("AmountMinorUnits")
            .IsRequired();
        builder.Ignore(template => template.DomainEvents);
        builder.Ignore(template => template.Recurrence);
        builder.Ignore(template => template.Schedule);
    }
}
