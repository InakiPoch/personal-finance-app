using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Parties.Infrastructure.Persistence.ReadModels;

namespace PersonalFinance.Parties.Infrastructure.Persistence.Configurations;

internal sealed class CurrentAccountTimelineEntryConfiguration : IEntityTypeConfiguration<CurrentAccountTimelineEntry> {
    public void Configure(EntityTypeBuilder<CurrentAccountTimelineEntry> builder) {
        builder.HasNoKey();
        builder.ToView("vw_current_account_timeline");
    }
}
