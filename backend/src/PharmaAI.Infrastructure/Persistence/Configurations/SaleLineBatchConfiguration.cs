using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Infrastructure.Persistence.Configurations;

internal sealed class SaleLineBatchConfiguration : IEntityTypeConfiguration<SaleLineBatch>
{
    public void Configure(EntityTypeBuilder<SaleLineBatch> builder)
    {
        builder.ToTable("SaleLineBatches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.BatchNumber).HasMaxLength(StockBatch.BatchNumberMaxLength).IsRequired();
        builder.HasOne<StockBatch>().WithMany().HasForeignKey(b => b.StockBatchId).OnDelete(DeleteBehavior.Restrict);
    }
}
