using PharmaAI.Application.Common;
using PharmaAI.Application.Tests.TestSupport;

namespace PharmaAI.Application.Tests.Common;

public class PharmacyCalendarTests
{
    [Fact]
    public void Today_follows_cairo_time_not_utc()
    {
        // 23:00 UTC on 15 Jan is 01:00 on 16 Jan in Cairo (UTC+2 in winter).
        var calendar = new PharmacyCalendar(new FixedTimeProvider(new DateTimeOffset(2026, 1, 15, 23, 0, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2026, 1, 16), calendar.Today);
    }

    [Fact]
    public void Same_day_before_cairo_midnight()
    {
        var calendar = new PharmacyCalendar(new FixedTimeProvider(new DateTimeOffset(2026, 1, 15, 21, 0, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2026, 1, 15), calendar.Today);
    }

    [Theory]
    [InlineData(4500, 45.00)]
    [InlineData(333, 3.33)]
    [InlineData(1, 0.01)]
    public void Piasters_round_trip(long piasters, decimal pounds)
    {
        Assert.Equal(pounds, Piasters.ToPounds(piasters));
        Assert.Equal(piasters, Piasters.FromPounds(pounds));
    }
}
