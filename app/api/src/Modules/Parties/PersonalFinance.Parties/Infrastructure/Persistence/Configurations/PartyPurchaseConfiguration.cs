using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Infrastructure.Persistence.Configurations;

internal sealed class PartyPurchaseConfiguration : IEntityTypeConfiguration<PartyPurchase> {
    public void Configure(EntityTypeBuilder<PartyPurchase> builder) {
        builder.ToTable("parties_purchases");
        builder.HasKey(purchase => purchase.Id);
        builder.Property(purchase => purchase.Id).ValueGeneratedNever();
        builder.Property(purchase => purchase.PartyId).IsRequired();
        builder.Property(purchase => purchase.Description).IsRequired();
        builder.Property(purchase => purchase.CategoryName).IsRequired();
        builder.Property(purchase => purchase.Kind)
            .HasConversion<string>()
        .IsRequired();
        builder.Property(purchase => purchase.PurchaseDate).IsRequired();
        builder.Property(purchase => purchase.IsCancelled).HasDefaultValue(false).IsRequired();
        builder.Property(purchase => purchase.ShareMinorUnits).HasColumnName("ShareMinorUnits").IsRequired();
        builder.Property(purchase => purchase.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code))
            .HasColumnName("CurrencyCode")
        .IsRequired();
        builder.HasMany(purchase => purchase.Installments)
            .WithOne()
            .HasForeignKey(installment => installment.PartyPurchaseId)
            .IsRequired()
        .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(purchase => purchase.Installments)
            .HasField("installments")
        .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(purchase => purchase.PartyId);
        builder.Ignore(purchase => purchase.DomainEvents);
    }
}
