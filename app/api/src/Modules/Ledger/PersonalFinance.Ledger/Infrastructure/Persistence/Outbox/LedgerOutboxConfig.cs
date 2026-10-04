using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Infrastructure.Outbox;

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Outbox;

internal sealed class LedgerOutboxConfig : IEntityTypeConfiguration<OutboxMessage> {
    public void Configure(EntityTypeBuilder<OutboxMessage> builder) {
        builder.ToTable("ledger_outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedOnAdd();
        builder.Property(message => message.MessageId).IsRequired();
        builder.Property(message => message.Type).IsRequired();
        builder.Property(message => message.Payload).IsRequired();
        builder.Property(message => message.OccurredOnUtc).IsRequired();
        builder.HasIndex(message => message.MessageId).IsUnique();
        builder.HasIndex(message => message.ProcessedOnUtc);
    }
}
