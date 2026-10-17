using PharmaAI.Domain.Inventory;

namespace PharmaAI.Domain.Sales;

/// <summary>One requested line of a sale, before stock is allocated.</summary>
public sealed record SaleItem(Drug Drug, SaleUnit Unit, int Quantity);
