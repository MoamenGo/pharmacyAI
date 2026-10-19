using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Infrastructure.Persistence.Configurations;

internal sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("StockAdjustments", t =>
            t.HasCheckConstraint("CK_StockAdjustments_Quantity_Positive", "\"QuantityInSubUnits\" > 0"));
        builder.HasKey(a => a.Id);
        // Ids are created in the domain; never let EF guess "existing" from a set key.
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.BatchNumber).HasMaxLength(StockBatch.BatchNumberMaxLength).IsRequired();
        builder.Property(a => a.Reason).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.Note).HasMaxLength(StockAdjustment.NoteMaxLength);

        builder.HasOne<Drug>().WithMany().HasForeignKey(a => a.DrugId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockBatch>().WithMany().HasForeignKey(a => a.StockBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.CreatedAtUtc);
    }
}
