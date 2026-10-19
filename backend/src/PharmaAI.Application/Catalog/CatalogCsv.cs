using System.Globalization;
using System.Text;

namespace PharmaAI.Application.Catalog;

/// <summary>
/// Reads the bundled CSV (commercial_name_en, commercial_name_ar, scientific_name, manufacturer, drug_class, route,
/// price_egp). Rows without a usable price and products marked cancelled are skipped.
/// </summary>
public static class CatalogCsv
{
    private static readonly string[] Columns =
        ["commercial_name_en", "commercial_name_ar", "scientific_name", "manufacturer", "drug_class", "route", "price_egp"];

    public static IEnumerable<CatalogDrug> Parse(TextReader reader)
    {
        var header = ReadRecord(reader) ?? throw new InvalidDataException("The drug catalog is empty.");
        var index = Columns.Select(c => header.IndexOf(c)).ToArray();
        if (index.Any(i => i < 0))
        {
            throw new InvalidDataException("The drug catalog is missing a column: " + string.Join(", ", Columns.Where((_, i) => index[i] < 0)));
        }

        while (ReadRecord(reader) is { } row)
        {
            string Field(int column) => index[column] < row.Count ? row[index[column]].Trim() : string.Empty;

            var nameEn = Field(0);
            if (nameEn.Length == 0
                || nameEn.Contains("CANCELLED", StringComparison.OrdinalIgnoreCase)
                || !decimal.TryParse(Field(6), NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
                || price <= 0)
            {
                continue;
            }

            // Some Arabic names carry leftover punctuation from the source ("أوجمينتين . ./").
            var nameAr = Field(1).TrimEnd(' ', '.', '/', '-', ',');
            yield return new CatalogDrug(nameEn, nameAr, Field(2), Field(3), Field(4), Field(5), Math.Round(price, 2));
        }
    }

    /// <summary>One RFC 4180 record: quoted fields may hold commas, doubled quotes and line breaks.</summary>
    private static List<string>? ReadRecord(TextReader reader)
    {
        if (reader.Peek() < 0)
        {
            return null;
        }

        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        while (true)
        {
            var next = reader.Read();
            if (next < 0)
            {
                fields.Add(field.ToString());
                return fields;
            }

            var c = (char)next;
            if (quoted)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && reader.Peek() == '\n')
                {
                    reader.Read();
                }

                fields.Add(field.ToString());
                return fields;
            }
            else
            {
                field.Append(c);
            }
        }
    }
}
