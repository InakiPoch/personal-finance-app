using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Financing.Domain;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Configurations;

internal sealed class CreditorAccountConfiguration : IEntityTypeConfiguration<CreditorAccount> {
    public void Configure(EntityTypeBuilder<CreditorAccount> builder) {
        builder.ToTable("financing_creditor_accounts");
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedNever();
        builder.Property(account => account.Label).IsRequired();
    }
}
