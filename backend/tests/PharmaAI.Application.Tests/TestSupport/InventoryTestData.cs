using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Tests.TestSupport;

public static class InventoryTestData
{
    public static readonly DateTime ReceivedAt = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>A box of 3 strips at EGP 45.00.</summary>
    public static Drug Panadol(string barcode = "6221234567890") =>
        Drug.Create("بانادول اكسترا", "Panadol Extra", barcode, "علبة", "شريط", 3, 45.00m, ReceivedAt);
}
