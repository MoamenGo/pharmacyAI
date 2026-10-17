namespace PharmaAI.Application.Common;

/// <summary>
/// The pharmacy's local date (Cairo time). Expiry checks must use the local day:
/// a batch expiring today is still sellable until local midnight, not UTC midnight.
/// </summary>
public sealed class PharmacyCalendar(TimeProvider clock)
{
    private static readonly TimeZoneInfo Cairo = ResolveCairo();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Cairo).DateTime);

    private static TimeZoneInfo ResolveCairo()
    {
        foreach (var id in new[] { "Africa/Cairo", "Egypt Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            {
                return zone;
            }
        }

        throw new InvalidOperationException("The Cairo time zone is not available on this machine.");
    }
}
