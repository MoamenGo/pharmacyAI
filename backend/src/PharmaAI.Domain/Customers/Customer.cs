using PharmaAI.Domain.Common;

namespace PharmaAI.Domain.Customers;

/// <summary>
/// A customer who can buy on credit. <see cref="Balance"/> is what they owe; credit sales raise it
/// and payments lower it. <see cref="Version"/> stops two tills from both changing it from the same value.
/// </summary>
public sealed class Customer
{
    public const int NameMaxLength = 100;
    public const int PhoneMaxLength = 20;

    private Customer()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? Phone { get; private set; }

    public decimal Balance { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public int Version { get; private set; }

    public static Customer Create(string name, string? phone, DateTime nowUtc)
    {
        name = name.Trim();
        if (name.Length is 0 or > NameMaxLength)
        {
            throw new DomainException("customer.invalid_name", $"A customer name needs 1 to {NameMaxLength} characters.");
        }

        phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (phone is { Length: > PhoneMaxLength })
        {
            throw new DomainException("customer.invalid_phone", $"A phone number has at most {PhoneMaxLength} characters.");
        }

        return new Customer { Id = Guid.NewGuid(), Name = name, Phone = phone, CreatedAtUtc = nowUtc };
    }

    /// <summary>Adds the unpaid part of a credit sale to what the customer owes.</summary>
    public void Charge(decimal amount)
    {
        if (amount < 0)
        {
            throw new DomainException("customer.invalid_amount", "A charge cannot be negative.");
        }

        Balance += amount;
        Version++;
    }

    public CustomerPayment RecordPayment(decimal amount, DateTime paidAtUtc, DateOnly businessDate, string? note)
    {
        if (amount <= 0)
        {
            throw new DomainException("customer.invalid_amount", "A payment must be more than zero.");
        }

        if (amount > Balance)
        {
            throw new DomainException("customer.overpayment", $"The payment {amount} is more than the balance {Balance}.");
        }

        Balance -= amount;
        Version++;
        return CustomerPayment.Create(Id, amount, paidAtUtc, businessDate, note);
    }
}
