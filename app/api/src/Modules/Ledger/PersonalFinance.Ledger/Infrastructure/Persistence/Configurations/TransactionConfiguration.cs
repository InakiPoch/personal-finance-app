using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Ledger.Domain;

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction> {
    public void Configure(EntityTypeBuilder<Transaction> builder) {
        builder.ToTable("ledger_transactions");
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Id).ValueGeneratedNever();
        builder.Property(transaction => transaction.PostedOnUtc).IsRequired();
        builder.Property(transaction => transaction.OriginalTransactionId);
        builder.HasIndex(transaction => transaction.OriginalTransactionId);
        builder.Property(transaction => transaction.SplitReference)
            .HasConversion(
                reference => reference == null ? (Guid?)null : reference.Value,
                value => value == null ? null : new SplitReference(value.Value))
            .HasColumnName("SplitReferenceId");
        builder.Property(transaction => transaction.InstallmentReference)
            .HasConversion(
                reference => reference == null ? (Guid?)null : reference.Value,
                value => value == null ? null : new InstallmentReference(value.Value))
            .HasColumnName("InstallmentReferenceId");
        builder.Ignore(transaction => transaction.IsReversal);
        builder.Ignore(transaction => transaction.DomainEvents);
        builder.HasMany(transaction => transaction.Entries)
            .WithOne()
            .HasForeignKey("TransactionId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(transaction => transaction.Entries)
            .HasField("entries")
        .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
