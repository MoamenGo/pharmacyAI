using System.IO.Compression;
using System.Text;
using PharmaAI.Application.Catalog;

namespace PharmaAI.Infrastructure.Catalog;

internal static class EmbeddedDrugCatalog
{
    private const string ResourceName = "PharmaAI.Infrastructure.Catalog.egyptian-drugs.csv.gz";

    public static DrugCatalog Load()
    {
        using var stream = typeof(EmbeddedDrugCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return new DrugCatalog(CatalogCsv.Parse(reader).ToList());
    }
}
