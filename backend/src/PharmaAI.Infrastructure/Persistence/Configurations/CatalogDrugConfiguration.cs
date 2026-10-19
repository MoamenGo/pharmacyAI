using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Catalog;

namespace PharmaAI.Infrastructure.Persistence.Configurations;

internal sealed class CatalogDrugConfiguration : IEntityTypeConfiguration<CatalogDrug>
{
    public void Configure(EntityTypeBuilder<CatalogDrug> builder)
    {
        builder.ToTable("CatalogDrugs");
        builder.HasKey(c => c.Id);
        // Ids are the row numbers of the bundled list, assigned by the importer.
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.NameEn).HasMaxLength(CatalogDrug.NameMaxLength).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(CatalogDrug.NameMaxLength).IsRequired();
        builder.Property(c => c.ActiveIngredient).HasMaxLength(CatalogDrug.ActiveIngredientMaxLength);
        builder.Property(c => c.Manufacturer).HasMaxLength(CatalogDrug.ManufacturerMaxLength);
        builder.Property(c => c.DrugClass).HasMaxLength(CatalogDrug.DrugClassMaxLength);
        builder.Property(c => c.Route).HasMaxLength(CatalogDrug.RouteMaxLength);
        builder.Property(c => c.Price).HasPrecision(18, 2);

        builder.HasIndex(c => c.NameEn);
        builder.HasIndex(c => c.ActiveIngredient);
    }
}
