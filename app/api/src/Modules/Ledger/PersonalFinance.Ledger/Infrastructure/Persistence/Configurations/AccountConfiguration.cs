using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Ledger.Domain;

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account> {
    public void Configure(EntityTypeBuilder<Account> builder) {
        builder.ToTable("ledger_accounts");
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedNever();
        builder.Property(account => account.Name).IsRequired();
        builder.Property(account => account.Type).HasConversion<string>().IsRequired();
        builder.Property(account => account.Kind).HasConversion<string>().IsRequired();
        builder.Ignore(account => account.DomainEvents);
    }
}
