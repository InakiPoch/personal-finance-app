using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseSplitParticipantConfiguration : IEntityTypeConfiguration<ExpenseSplitParticipant> {
    public void Configure(EntityTypeBuilder<ExpenseSplitParticipant> builder) {
        builder.ToTable("parties_expense_split_participants");
        builder.HasKey(participant => participant.Id);
        builder.Property(participant => participant.Id).ValueGeneratedNever();
        builder.Property(participant => participant.ExpenseSplitId).IsRequired();
        builder.Property(participant => participant.PartyId).IsRequired();
        builder.Property(participant => participant.Share)
            .HasConversion(
                amount => amount.MinorUnits,
                value => Money.FromMinorUnits(value, Currency.Reference))
            .HasColumnName("ShareMinorUnits")
        .IsRequired();
    }
}
