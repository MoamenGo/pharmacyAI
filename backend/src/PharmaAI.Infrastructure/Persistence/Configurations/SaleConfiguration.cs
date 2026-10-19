using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Customers;
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
        builder.Property(s => s.Discount).HasPrecision(18, 2).HasDefaultValue(0m).IsRequired();
        builder.Property(s => s.PaymentMethod).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(s => s.CustomerName).HasMaxLength(Customer.NameMaxLength);
        builder.Property(s => s.CardReference).HasMaxLength(Sale.CardReferenceMaxLength);
        builder.Ignore(s => s.Subtotal);
        builder.Ignore(s => s.Change);
        builder.Ignore(s => s.Due);

        // Two tills racing for the next number: the loser hits this index and retries.
        builder.HasIndex(s => s.Number).IsUnique();
        builder.HasIndex(s => s.BusinessDate);
        builder.HasIndex(s => s.CustomerId);
        builder.HasOne<Customer>().WithMany().HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Lines)
            .WithOne()
            .HasForeignKey(l => l.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
