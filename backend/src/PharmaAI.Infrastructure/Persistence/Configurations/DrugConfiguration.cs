using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Infrastructure.Persistence.Configurations;

internal sealed class DrugConfiguration : IEntityTypeConfiguration<Drug>
{
    public void Configure(EntityTypeBuilder<Drug> builder)
    {
        builder.ToTable("Drugs");
        builder.HasKey(d => d.Id);
        // Ids are created in the domain; never let EF guess "existing" from a set key.
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.NameAr).HasMaxLength(Drug.NameMaxLength).IsRequired();
        builder.Property(d => d.NameEn).HasMaxLength(Drug.NameMaxLength);
        builder.Property(d => d.Barcode).HasMaxLength(Drug.BarcodeMaxLength).IsRequired();
        builder.Property(d => d.PackUnitName).HasMaxLength(Drug.UnitNameMaxLength).IsRequired();
        builder.Property(d => d.SubUnitName).HasMaxLength(Drug.UnitNameMaxLength).IsRequired();
        builder.Property(d => d.SubUnitsPerPack).IsRequired();
        // SQLite has no decimal type; EF stores decimal as exact TEXT, never as a binary float.
        builder.Property(d => d.PublicPrice).HasPrecision(18, 2).IsRequired();
        builder.Ignore(d => d.SubUnitPrice);

        builder.HasIndex(d => d.Barcode).IsUnique();
        builder.HasIndex(d => d.NameAr);

        builder.HasMany(d => d.Batches)
            .WithOne()
            .HasForeignKey(b => b.DrugId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(d => d.Batches).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
