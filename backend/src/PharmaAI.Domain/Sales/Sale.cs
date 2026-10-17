using PharmaAI.Domain.Common;

namespace PharmaAI.Domain.Sales;

/// <summary>
/// A completed cash sale. It is created in one step: every line takes its stock FEFO,
/// or the whole sale fails and no stock moves.
/// </summary>
public sealed class Sale
{
    public const int MaxLines = 200;

    private readonly List<SaleLine> _lines = [];

    private Sale()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Sequential receipt number shown to the customer.</summary>
    public long Number { get; private set; }

    public DateTime SoldAtUtc { get; private set; }

    /// <summary>The pharmacy's local (Cairo) day of the sale, used for daily totals.</summary>
    public DateOnly BusinessDate { get; private set; }

    public decimal Total { get; private set; }

    public decimal Paid { get; private set; }

    public decimal Change => Paid - Total;

    public IReadOnlyCollection<SaleLine> Lines => _lines;

    public static Sale Create(long number, DateTime soldAtUtc, DateOnly businessDate, IReadOnlyList<SaleItem> items, decimal paid)
    {
        if (items.Count is 0 or > MaxLines)
        {
            throw new DomainException("sale.invalid_lines", $"A sale needs between 1 and {MaxLines} lines.");
        }

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            Number = number,
            SoldAtUtc = soldAtUtc,
            BusinessDate = businessDate,
        };

        for (var i = 0; i < items.Count; i++)
        {
            sale._lines.Add(SaleLine.Create(sale.Id, i + 1, items[i], businessDate));
        }

        sale.Total = sale._lines.Sum(l => l.LineTotal);

        if (paid < sale.Total)
        {
            throw new DomainException("sale.insufficient_payment", $"Paid {paid} is less than the total {sale.Total}.");
        }

        sale.Paid = paid;
        return sale;
    }
}
