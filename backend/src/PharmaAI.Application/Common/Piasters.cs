namespace PharmaAI.Application.Common;

/// <summary>
/// The API exchanges money as whole piasters (1 EGP = 100 piasters) so clients never
/// handle fractional numbers; the domain works in decimal EGP.
/// </summary>
public static class Piasters
{
    public static decimal ToPounds(long piasters) => piasters / 100m;

    public static long FromPounds(decimal pounds) =>
        (long)Math.Round(pounds * 100m, 0, MidpointRounding.AwayFromZero);
}
