namespace PharmaAI.Domain.Sales;

/// <summary>How the customer paid for a sale.</summary>
public enum PaymentMethod
{
    Cash = 1,
    Card = 2,

    /// <summary>On the customer's account: whatever is not paid now is added to their balance.</summary>
    Credit = 3,
}
