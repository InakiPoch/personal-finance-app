using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Infrastructure.Idempotency;

namespace PersonalFinance.Parties.Infrastructure.Persistence.Inbox;

internal sealed class PartiesInboxConfig : IEntityTypeConfiguration<InboxConsumedMessage> {
    public void Configure(EntityTypeBuilder<InboxConsumedMessage> builder) {
        builder.ToTable("parties_inbox_consumed");
        builder.HasKey(row => new { row.MessageId, row.Consumer });
        builder.Property(row => row.Consumer).HasMaxLength(200);
        builder.Property(row => row.ConsumedOnUtc).IsRequired();
    }
}
