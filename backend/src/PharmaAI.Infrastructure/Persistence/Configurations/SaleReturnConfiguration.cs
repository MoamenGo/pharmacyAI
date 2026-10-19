using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Customers;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Infrastructure.Persistence.Configurations;

internal sealed class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> builder)
    {
        builder.ToTable("SaleReturns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.ReturnedAtUtc).IsRequired();
        builder.Property(r => r.BusinessDate).IsRequired();
        builder.Property(r => r.PaymentMethod).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(r => r.Total).HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.CreditedToAccount).HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(SaleReturn.ReasonMaxLength);
        builder.Ignore(r => r.Refunded);

        // Two tills racing for the next return number: the loser hits this index and retries.
        builder.HasIndex(r => r.Number).IsUnique();
        builder.HasIndex(r => r.BusinessDate);
        builder.HasIndex(r => r.SaleId);
        builder.HasOne<Sale>().WithMany().HasForeignKey(r => r.SaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Lines)
            .WithOne()
            .HasForeignKey(l => l.SaleReturnId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SaleReturnLineConfiguration : IEntityTypeConfiguration<SaleReturnLine>
{
    public void Configure(EntityTypeBuilder<SaleReturnLine> builder)
    {
        builder.ToTable("SaleReturnLines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.DrugNameAr).HasMaxLength(Drug.NameMaxLength).IsRequired();
        builder.Property(l => l.UnitName).HasMaxLength(Drug.UnitNameMaxLength).IsRequired();
        builder.Property(l => l.Unit).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(l => l.Amount).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(l => l.SaleLineId);
        builder.HasOne<SaleLine>().WithMany().HasForeignKey(l => l.SaleLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Drug>().WithMany().HasForeignKey(l => l.DrugId).OnDelete(DeleteBehavior.Restrict);
    }
}
