using System.Text.RegularExpressions;

namespace PharmaAI.Application.Catalog;

/// <summary>A first guess at how a catalog product is packed, read from its English trade name. The user can change it.</summary>
public sealed record SuggestedUnits(string PackUnitName, string SubUnitName, int SubUnitsPerPack);

public static partial class CatalogUnits
{
    public static SuggestedUnits Suggest(string nameEn)
    {
        var name = nameEn.ToUpperInvariant();

        if (Count(TabletsPattern(), name) is { } tablets)
        {
            // Strips usually hold 10 tablets, 7 for weekly packs (14, 28) or 12 (24). Anything else is sold as whole boxes.
            var strips = tablets % 10 == 0 ? tablets / 10 : tablets % 7 == 0 ? tablets / 7 : tablets % 12 == 0 ? tablets / 12 : 1;
            return new SuggestedUnits("علبة", "شريط", Math.Clamp(strips, 1, 100));
        }

        if (Count(AmpoulesPattern(), name) is { } ampoules)
        {
            return new SuggestedUnits("علبة", "أمبول", ampoules);
        }

        if (Count(VialsPattern(), name) is { } vials)
        {
            return new SuggestedUnits("علبة", "فيال", vials);
        }

        if (Count(SachetsPattern(), name) is { } sachets)
        {
            return new SuggestedUnits("علبة", "كيس", sachets);
        }

        if (Count(SuppositoriesPattern(), name) is { } suppositories)
        {
            return new SuggestedUnits("علبة", "لبوس", suppositories);
        }

        var container = ContainerPattern().Match(name) is { Success: true } m
            ? m.Value switch
            {
                "CREAM" or "GEL" or "OINT" => "أنبوبة",
                "SPRAY" => "بخاخ",
                "VIAL" => "فيال",
                "AMP" => "أمبول",
                _ => "زجاجة",
            }
            : "علبة";
        return new SuggestedUnits(container, container, 1);
    }

    private static int? Count(Regex pattern, string name) =>
        pattern.Match(name) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var n) && n is >= 1 and <= 1000 ? n : null;

    [GeneratedRegex(@"\b(\d+)\s*(?:[A-Z.\-]+\s*)?(?:TABS?|TABLETS?|CAPS?|CAPSULES?)\b")]
    private static partial Regex TabletsPattern();

    [GeneratedRegex(@"\b(\d+)\s*(?:[A-Z./\-]+\s*)*AMPS?\b")]
    private static partial Regex AmpoulesPattern();

    [GeneratedRegex(@"\b(\d+)\s*(?:[A-Z./\-]+\s*)*VIALS?\b")]
    private static partial Regex VialsPattern();

    [GeneratedRegex(@"\b(\d+)\s*SACHETS?\b")]
    private static partial Regex SachetsPattern();

    [GeneratedRegex(@"\b(\d+)\s*(?:[A-Z.\-]+\s*)?SUPP")]
    private static partial Regex SuppositoriesPattern();

    [GeneratedRegex(@"\b(CREAM|GEL|OINT|SPRAY|SYRUP|SUSP|DROPS|LOTION|SHAMPOO|SOLUTION|VIAL|AMP|ML)\b")]
    private static partial Regex ContainerPattern();
}
