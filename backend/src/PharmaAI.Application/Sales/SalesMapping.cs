using PharmaAI.Application.Common;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Sales;

internal static class SalesMapping
{
    public static SaleDto ToDto(this Sale sale) =>
        new(
            sale.Id,
            sale.Number,
            sale.SoldAtUtc,
            sale.BusinessDate,
            sale.PaymentMethod,
            Piasters.FromPounds(sale.Subtotal),
            Piasters.FromPounds(sale.Discount),
            Piasters.FromPounds(sale.Total),
            Piasters.FromPounds(sale.Paid),
            Piasters.FromPounds(sale.Change),
            Piasters.FromPounds(sale.Due),
            sale.CustomerId,
            sale.CustomerName,
            sale.CardReference,
            sale.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new SaleLineDto(
                    l.LineNumber,
                    l.DrugId,
                    l.DrugNameAr,
                    l.Unit,
                    l.UnitName,
                    l.Quantity,
                    l.QuantityInSubUnits,
                    Piasters.FromPounds(l.UnitPrice),
                    Piasters.FromPounds(l.LineTotal),
                    Piasters.FromPounds(l.Discount),
                    l.Batches
                        .OrderBy(b => b.ExpiryDate)
                        .Select(b => new SaleLineBatchDto(b.BatchNumber, b.ExpiryDate, b.QuantityInSubUnits))
                        .ToList()))
                .ToList());
}
