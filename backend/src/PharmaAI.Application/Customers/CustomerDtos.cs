namespace PharmaAI.Application.Customers;

public sealed record CustomerDto(Guid Id, string Name, string? Phone, long BalancePiasters);

public enum CustomerEntryKind
{
    /// <summary>A credit sale; the amount is the part added to the balance.</summary>
    Sale = 1,
    Payment = 2,
}

public sealed record CustomerEntryDto(
    CustomerEntryKind Kind,
    DateTime AtUtc,
    long AmountPiasters,
    Guid? SaleId,
    long? SaleNumber,
    string? Note);

/// <param name="Entries">The newest credit sales and payments, newest first.</param>
public sealed record CustomerDetailsDto(CustomerDto Customer, IReadOnlyList<CustomerEntryDto> Entries);
