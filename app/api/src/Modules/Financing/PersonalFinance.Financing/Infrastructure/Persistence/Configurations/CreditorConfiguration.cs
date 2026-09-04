using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Financing.Domain;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Configurations;

internal sealed class CreditorConfiguration : IEntityTypeConfiguration<Creditor> {
    public void Configure(EntityTypeBuilder<Creditor> builder) {
        builder.ToTable("financing_creditors");
        builder.HasKey(creditor => creditor.Id);
        builder.Property(creditor => creditor.Id).ValueGeneratedNever();
        builder.Property(creditor => creditor.Name).IsRequired();
        builder.HasMany(creditor => creditor.Accounts)
            .WithOne()
            .HasForeignKey(account => account.CreditorId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(creditor => creditor.Accounts)
            .HasField("accounts")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(creditor => creditor.DomainEvents);
    }
}
