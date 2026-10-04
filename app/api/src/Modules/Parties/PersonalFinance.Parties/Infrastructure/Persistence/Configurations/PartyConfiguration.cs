using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Parties.Domain;

namespace PersonalFinance.Parties.Infrastructure.Persistence.Configurations;

internal sealed class PartyConfiguration : IEntityTypeConfiguration<Party> {
    public void Configure(EntityTypeBuilder<Party> builder) {
        builder.ToTable("parties_parties");
        builder.HasKey(party => party.Id);
        builder.Property(party => party.Id).ValueGeneratedNever();
        builder.Property(party => party.Name).IsRequired();
        builder.Property(party => party.ReceivableAccountId).IsRequired();
        builder.Ignore(party => party.DomainEvents);
    }
}
