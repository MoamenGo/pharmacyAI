using System.Globalization;
using System.Text;

namespace PharmaAI.Application.Catalog;

/// <summary>
/// The bundled Egyptian drug list, held in memory and searched by trade name (Arabic or English)
/// or active ingredient. It is reference data only: picking a product fills the new-drug form.
/// </summary>
public sealed class DrugCatalog
{
    private readonly Entry[] _entries;

    public DrugCatalog(IEnumerable<CatalogDrug> drugs)
    {
        _entries = drugs
            .Select(d =>
            {
                var ar = Normalize(d.NameAr);
                return new Entry(d, Normalize(d.NameEn), ar, Normalize(d.ScientificName), Skeleton(ar));
            })
            .ToArray();
    }

    public int Count => _entries.Length;

    /// <summary>
    /// Every word of <paramref name="term"/> must appear in a name or the active ingredient. Names that start with
    /// the term come first, then names that contain it, then everything else (ingredient matches).
    /// </summary>
    public IReadOnlyList<CatalogDrug> Search(string? term, int limit)
    {
        var normalized = Normalize(term ?? string.Empty);
        if (normalized.Length == 0)
        {
            return [];
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => (Word: w, Skeleton: Skeleton(w)))
            .ToList();
        return _entries
            .Where(e => words.All(w => e.En.Contains(w.Word, StringComparison.Ordinal)
                || e.Ar.Contains(w.Word, StringComparison.Ordinal)
                || e.Scientific.Contains(w.Word, StringComparison.Ordinal)
                || (w.Skeleton.Length >= 3 && e.ArSkeleton.Contains(w.Skeleton, StringComparison.Ordinal))))
            .OrderBy(e => Rank(e, normalized))
            .ThenBy(e => e.Drug.NameEn, StringComparer.Ordinal)
            .Take(limit)
            .Select(e => e.Drug)
            .ToList();
    }

    private static int Rank(Entry e, string term) =>
        e.En.StartsWith(term, StringComparison.Ordinal) || e.Ar.StartsWith(term, StringComparison.Ordinal) ? 0
        : e.En.Contains(term, StringComparison.Ordinal) || e.Ar.Contains(term, StringComparison.Ordinal) ? 1
        : 2;

    /// <summary>
    /// Folds the spellings people type interchangeably: Arabic alef/hamza forms, taa marbuta and alef maqsura, diacritics
    /// and tatweel, letter case, and punctuation (so "F.C.TABS" matches "fc tabs").
    /// </summary>
    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSpace = true;
        foreach (var raw in value.Normalize(NormalizationForm.FormC))
        {
            var c = raw switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ة' => 'ه',
                'ى' => 'ي',
                'ؤ' => 'و',
                'ئ' => 'ي',
                _ => char.ToLowerInvariant(raw),
            };

            if (c == 'ـ' || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c) || c == '%')
            {
                builder.Append(c);
                lastWasSpace = false;
            }
            else if (char.IsWhiteSpace(c) && !lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Arabic trade names are transliterations, spelled with or without long vowels ("أوجمنتين" or "أوجمينتين"), so
    /// as a fallback Arabic words are also compared with their alef, waw and yaa removed.
    /// </summary>
    private static string Skeleton(string normalized) =>
        new(normalized.Where(c => c is >= '\u0621' and <= '\u064A' && c is not ('ا' or 'و' or 'ي')).ToArray());

    private sealed record Entry(CatalogDrug Drug, string En, string Ar, string Scientific, string ArSkeleton);
}
