using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Infrastructure.Persistence.Configurations;

internal sealed class StockBatchConfiguration : IEntityTypeConfiguration<StockBatch>
{
    public void Configure(EntityTypeBuilder<StockBatch> builder)
    {
        builder.ToTable("StockBatches", t =>
            t.HasCheckConstraint("CK_StockBatches_Quantity_NonNegative", "\"QuantityInSubUnits\" >= 0"));
        builder.HasKey(b => b.Id);
        // Ids are created in the domain; never let EF guess "existing" from a set key.
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.BatchNumber).HasMaxLength(StockBatch.BatchNumberMaxLength).IsRequired();
        builder.Property(b => b.ExpiryDate).IsRequired();
        builder.Property(b => b.QuantityInSubUnits).IsRequired();
        builder.Property(b => b.ReceivedAtUtc).IsRequired();
        builder.Property(b => b.Version).IsConcurrencyToken();

        builder.HasIndex(b => new { b.DrugId, b.BatchNumber, b.ExpiryDate }).IsUnique();
        builder.HasIndex(b => new { b.DrugId, b.ExpiryDate });
    }
}
