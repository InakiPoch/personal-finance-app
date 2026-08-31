using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Infrastructure.Idempotency;

namespace PersonalFinance.Subscriptions.Infrastructure.Persistence.Inbox;

internal sealed class SubscriptionsInboxConfig : IEntityTypeConfiguration<InboxConsumedMessage> {
    public void Configure(EntityTypeBuilder<InboxConsumedMessage> builder) {
        builder.ToTable("subscriptions_inbox_consumed");
        builder.HasKey(row => new { row.MessageId, row.Consumer });
        builder.Property(row => row.Consumer).HasMaxLength(200);
        builder.Property(row => row.ConsumedOnUtc).IsRequired();
    }
}
