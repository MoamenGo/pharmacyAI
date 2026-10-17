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
            Piasters.FromPounds(sale.Total),
            Piasters.FromPounds(sale.Paid),
            Piasters.FromPounds(sale.Change),
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
                    l.Batches
                        .OrderBy(b => b.ExpiryDate)
                        .Select(b => new SaleLineBatchDto(b.BatchNumber, b.ExpiryDate, b.QuantityInSubUnits))
                        .ToList()))
                .ToList());
}
