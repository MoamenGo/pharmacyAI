using PharmaAI.Domain.Common;

namespace PharmaAI.Domain.Customers;

/// <summary>Money a customer paid towards their balance, taken in cash at the till.</summary>
public sealed class CustomerPayment
{
    public const int NoteMaxLength = 200;

    private CustomerPayment()
    {
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public decimal Amount { get; private set; }

    public DateTime PaidAtUtc { get; private set; }

    /// <summary>The pharmacy's local day, so the day's cash count includes it.</summary>
    public DateOnly BusinessDate { get; private set; }

    public string? Note { get; private set; }

    internal static CustomerPayment Create(Guid customerId, decimal amount, DateTime paidAtUtc, DateOnly businessDate, string? note)
    {
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note is { Length: > NoteMaxLength })
        {
            throw new DomainException("customer.invalid_note", $"A note has at most {NoteMaxLength} characters.");
        }

        return new CustomerPayment
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Amount = amount,
            PaidAtUtc = paidAtUtc,
            BusinessDate = businessDate,
            Note = note,
        };
    }
}
