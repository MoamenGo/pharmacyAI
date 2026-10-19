using System.Data.Common;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PharmaAI.Domain.Catalog;
using PharmaAI.Infrastructure.Persistence;

namespace PharmaAI.Infrastructure.Catalog;

/// <summary>
/// Loads the Egyptian drug list that ships inside the API (egyptian-drugs.csv.gz, CC0, from
/// github.com/karem505/egyptian-drug-database) into the CatalogDrugs table. It runs at startup and
/// only does work when the bundled list is newer than the one in the database, so a pharmacy that
/// updates the app gets the new prices without touching its own stock.
/// </summary>
public static class CatalogImporter
{
    /// <summary>Bump this whenever the bundled file changes.</summary>
    public const string Version = "karem505-2026-06";
    public const string VersionSettingKey = "catalog.version";

    private const string ResourceName = "PharmaAI.Infrastructure.Catalog.egyptian-drugs.csv.gz";
    private static readonly string[] Columns =
        ["commercial_name_en", "commercial_name_ar", "scientific_name", "manufacturer", "drug_class", "route", "price_egp"];

    /// <summary>Returns the number of rows imported, or 0 when the database already has this version.</summary>
    public static async Task<int> ImportIfOutdatedAsync(PharmaDbContext db, CancellationToken cancellationToken = default)
    {
        var current = await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == VersionSettingKey)
            .Select(s => s.Value)
            .SingleOrDefaultAsync(cancellationToken);
        if (current == Version)
        {
            return 0;
        }

        var drugs = ReadBundled();
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            // One transaction: other connections keep seeing the old list until the new one is complete.
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var tx = transaction.GetDbTransaction();

            await ExecuteAsync(connection, tx, "DELETE FROM \"CatalogDrugs\";", cancellationToken);
            await InsertAsync(connection, tx, drugs, cancellationToken);
            await UpsertVersionAsync(connection, tx, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        return drugs.Count;
    }

    /// <summary>Parses the bundled CSV. Rows without an English name are skipped; ids are 1-based row numbers.</summary>
    public static IReadOnlyList<CatalogDrug> ReadBundled()
    {
        using var stream = typeof(CatalogImporter).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The drug catalog resource {ResourceName} is missing from the build.");
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return Parse(reader);
    }

    internal static IReadOnlyList<CatalogDrug> Parse(TextReader reader)
    {
        var rows = Csv.Read(reader).GetEnumerator();
        if (!rows.MoveNext())
        {
            return [];
        }

        var header = rows.Current;
        var index = Columns.Select(c => Array.IndexOf(header, c)).ToArray();
        if (index.Any(i => i < 0))
        {
            throw new InvalidDataException($"The drug catalog must have the columns {string.Join(", ", Columns)}.");
        }

        var drugs = new List<CatalogDrug>(26_000);
        while (rows.MoveNext())
        {
            var row = rows.Current;
            string? Field(int column) => index[column] < row.Length && !string.IsNullOrWhiteSpace(row[index[column]])
                ? row[index[column]].Trim()
                : null;

            var nameEn = Field(0);
            if (nameEn is null)
            {
                continue;
            }

            var price = decimal.TryParse(Field(6), NumberStyles.Number, CultureInfo.InvariantCulture, out var p) && p > 0 ? p : (decimal?)null;
            drugs.Add(new CatalogDrug(
                drugs.Count + 1,
                Clip(nameEn, CatalogDrug.NameMaxLength)!,
                Clip(Field(1) ?? nameEn, CatalogDrug.NameMaxLength)!,
                Clip(Field(2), CatalogDrug.ActiveIngredientMaxLength),
                Clip(Field(3), CatalogDrug.ManufacturerMaxLength),
                Clip(Field(4), CatalogDrug.DrugClassMaxLength),
                Clip(Field(5) is "UNKNOWN" ? null : Field(5), CatalogDrug.RouteMaxLength),
                price));
        }

        return drugs;
    }

    private static string? Clip(string? value, int max) => value is null || value.Length <= max ? value : value[..max];

    private static async Task InsertAsync(DbConnection connection, DbTransaction tx, IReadOnlyList<CatalogDrug> drugs, CancellationToken cancellationToken)
    {
        await using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO "CatalogDrugs" ("Id", "NameEn", "NameAr", "ActiveIngredient", "Manufacturer", "DrugClass", "Route", "Price")
            VALUES ($id, $en, $ar, $active, $maker, $class, $route, $price);
            """;
        var parameters = new[] { "$id", "$en", "$ar", "$active", "$maker", "$class", "$route", "$price" }
            .Select(name =>
            {
                var parameter = insert.CreateParameter();
                parameter.ParameterName = name;
                insert.Parameters.Add(parameter);
                return parameter;
            })
            .ToArray();
        insert.Prepare();

        foreach (var d in drugs)
        {
            parameters[0].Value = d.Id;
            parameters[1].Value = d.NameEn;
            parameters[2].Value = d.NameAr;
            parameters[3].Value = (object?)d.ActiveIngredient ?? DBNull.Value;
            parameters[4].Value = (object?)d.Manufacturer ?? DBNull.Value;
            parameters[5].Value = (object?)d.DrugClass ?? DBNull.Value;
            parameters[6].Value = (object?)d.Route ?? DBNull.Value;
            // EF stores decimals as exact TEXT on SQLite; write them the same way.
            parameters[7].Value = d.Price is { } price ? price.ToString(CultureInfo.InvariantCulture) : DBNull.Value;
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpsertVersionAsync(DbConnection connection, DbTransaction tx, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            INSERT INTO "AppSettings" ("Key", "Value", "UpdatedAtUtc") VALUES ($key, $value, $now)
            ON CONFLICT ("Key") DO UPDATE SET "Value" = excluded."Value", "UpdatedAtUtc" = excluded."UpdatedAtUtc";
            """;
        AddParameter(command, "$key", VersionSettingKey);
        AddParameter(command, "$value", Version);
        AddParameter(command, "$now", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(DbConnection connection, DbTransaction tx, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

/// <summary>A small RFC 4180 reader: quoted fields may hold commas, doubled quotes and line breaks.</summary>
internal static class Csv
{
    public static IEnumerable<string[]> Read(TextReader reader)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var any = false;

        int c;
        while ((c = reader.Read()) != -1)
        {
            var ch = (char)c;
            any = true;
            if (quoted)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        field.Append('"');
                        reader.Read();
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    yield return fields.ToArray();
                    fields.Clear();
                    any = false;
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (any)
        {
            fields.Add(field.ToString());
            yield return fields.ToArray();
        }
    }
}
