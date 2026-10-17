using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Infrastructure.Persistence.Configurations;

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.HasKey(s => s.Id);
        // Ids are created in the domain; never let EF guess "existing" from a set key.
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Number).IsRequired();
        builder.Property(s => s.SoldAtUtc).IsRequired();
        builder.Property(s => s.BusinessDate).IsRequired();
        builder.Property(s => s.Total).HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.Paid).HasPrecision(18, 2).IsRequired();
        builder.Ignore(s => s.Change);

        // Two tills racing for the next number: the loser hits this index and retries.
        builder.HasIndex(s => s.Number).IsUnique();
        builder.HasIndex(s => s.BusinessDate);

        builder.HasMany(s => s.Lines)
            .WithOne()
            .HasForeignKey(l => l.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
