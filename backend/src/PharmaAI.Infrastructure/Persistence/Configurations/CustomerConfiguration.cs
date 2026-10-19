using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Customers;

namespace PharmaAI.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(Customer.NameMaxLength).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(Customer.PhoneMaxLength);
        builder.Property(c => c.Balance).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.Version).IsConcurrencyToken();

        builder.HasIndex(c => c.Name);
        // One account per phone number; customers without a phone are allowed.
        builder.HasIndex(c => c.Phone).IsUnique().HasFilter("\"Phone\" IS NOT NULL");
    }
}

internal sealed class CustomerPaymentConfiguration : IEntityTypeConfiguration<CustomerPayment>
{
    public void Configure(EntityTypeBuilder<CustomerPayment> builder)
    {
        builder.ToTable("CustomerPayments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.Note).HasMaxLength(CustomerPayment.NoteMaxLength);

        builder.HasIndex(p => new { p.CustomerId, p.PaidAtUtc });
        builder.HasIndex(p => p.BusinessDate);
        builder.HasOne<Customer>().WithMany().HasForeignKey(p => p.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
