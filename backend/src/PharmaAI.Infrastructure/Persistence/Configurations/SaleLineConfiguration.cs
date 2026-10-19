using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Infrastructure.Persistence.Configurations;

internal sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> builder)
    {
        builder.ToTable("SaleLines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.DrugNameAr).HasMaxLength(Drug.NameMaxLength).IsRequired();
        builder.Property(l => l.UnitName).HasMaxLength(Drug.UnitNameMaxLength).IsRequired();
        builder.Property(l => l.Unit).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.LineTotal).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.Discount).HasPrecision(18, 2).HasDefaultValue(0m).IsRequired();
        builder.Ignore(l => l.NetTotal);
        builder.Ignore(l => l.ReturnableQuantity);
        builder.Property(l => l.ReturnedAmount).HasPrecision(18, 2).HasDefaultValue(0m).IsRequired();
        // Two returns of the same line at once: the second sees a changed count and retries on fresh data.
        builder.Property(l => l.ReturnedQuantity).HasDefaultValue(0).IsConcurrencyToken();

        builder.HasIndex(l => new { l.SaleId, l.LineNumber }).IsUnique();
        builder.HasOne<Drug>().WithMany().HasForeignKey(l => l.DrugId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Batches)
            .WithOne()
            .HasForeignKey(b => b.SaleLineId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(l => l.Batches).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
