using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PharmaAI.Application.Sync.Eta;

/// <summary>
/// ETA's document serialization: every property becomes its upper-cased name in quotes followed
/// by its value; strings and numbers are quoted exactly as written; arrays repeat the property
/// name before each element; nothing separates the tokens. The receipt UUID is the lower-case hex
/// SHA-256 of that text, computed with the uuid field empty.
/// See https://sdk.invoicing.eta.gov.eg/document-serialization-approach/.
/// </summary>
public static class EtaSerializer
{
    public static string Serialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var builder = new StringBuilder();
        Write(builder, document.RootElement);
        return builder.ToString();
    }

    public static string Hash(string serialized) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized))).ToLowerInvariant();

    private static void Write(StringBuilder builder, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var name = Quote(property.Name.ToUpperInvariant());
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        builder.Append(name);
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            builder.Append(name);
                            Write(builder, item);
                        }
                    }
                    else
                    {
                        builder.Append(name);
                        Write(builder, property.Value);
                    }
                }

                break;
            case JsonValueKind.String:
                builder.Append(Quote(element.GetString()!));
                break;
            case JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False:
                builder.Append(Quote(element.GetRawText()));
                break;
            default:
                throw new InvalidOperationException($"ETA documents must not contain {element.ValueKind} values.");
        }
    }

    private static string Quote(string value) => $"\"{value}\"";
}
