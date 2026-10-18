using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PharmaAI.Application.Sync.Eta;

namespace PharmaAI.Infrastructure.Sync;

/// <summary>
/// Talks to the ETA e-receipt API: a POS client-credentials login (token cached until shortly
/// before it expires, in <see cref="EtaTokenCache"/>), then POST /api/v1/receiptsubmissions. Anything that is not a per-receipt
/// verdict (offline, login refused, throttling, server error) is reported as a batch error so
/// every receipt in the batch is retried later.
/// See https://sdk.invoicing.eta.gov.eg/ereceiptapi/01-authenticate-pos/ and 02-submit-receipt/.
/// </summary>
public sealed class EtaHttpClient(HttpClient http, EtaTokenCache tokens, TimeProvider clock, ILogger<EtaHttpClient> logger) : IEtaClient
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    public async Task<EtaSubmitResult> SubmitAsync(EtaSettings settings, IReadOnlyList<string> receipts, CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetTokenAsync(settings, cancellationToken);
            if (token.Error is { } loginError)
            {
                return EtaSubmitResult.Failed(loginError);
            }

            // The receipts are inserted as raw text: re-serializing could change how a number is written and break its UUID.
            var body = $"{{\"receipts\":[{string.Join(",", receipts)}]}}";
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(EtaEndpoints.Api(settings.Environment), "api/v1/receiptsubmissions"))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("ar"));

            using var response = await http.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                tokens.Current = null;
                return EtaSubmitResult.Failed("ETA rejected the login token; it will log in again on the next attempt.");
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("ETA submission failed with {Status}: {Body}", (int)response.StatusCode, text);
                return EtaSubmitResult.Failed($"ETA returned {(int)response.StatusCode}: {Describe(text)}");
            }

            return ParseSubmission(text);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException { CancellationToken.IsCancellationRequested: false } or JsonException)
        {
            logger.LogInformation(ex, "ETA is unreachable");
            return EtaSubmitResult.Failed($"Could not reach ETA: {ex.Message}");
        }
    }

    private async Task<CachedToken> GetTokenAsync(EtaSettings settings, CancellationToken cancellationToken)
    {
        var key = $"{settings.Environment}|{settings.ClientId}|{settings.PosSerial}";
        await tokens.Lock.WaitAsync(cancellationToken);
        try
        {
            if (tokens.Current is { } cached && cached.Key == key && cached.ExpiresAt - RefreshMargin > clock.GetUtcNow())
            {
                return cached;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(EtaEndpoints.Identity(settings.Environment), "connect/token"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = settings.ClientId,
                    ["client_secret"] = settings.ClientSecret,
                }),
            };
            request.Headers.Add("posserial", settings.PosSerial);
            request.Headers.Add("pososversion", settings.PosOsVersion);
            request.Headers.Add("posmodelframework", settings.PosModelFramework);
            request.Headers.Add("presharedkey", settings.PresharedKey);

            using var response = await http.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new CachedToken(key, string.Empty, default, $"ETA login failed ({(int)response.StatusCode}): {Describe(text)}");
            }

            using var json = JsonDocument.Parse(text);
            var accessToken = json.RootElement.GetProperty("access_token").GetString()!;
            var lifetime = json.RootElement.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 3600;
            tokens.Current = new CachedToken(key, accessToken, clock.GetUtcNow().AddSeconds(lifetime), null);
            return tokens.Current;
        }
        finally
        {
            tokens.Lock.Release();
        }
    }

    private static EtaSubmitResult ParseSubmission(string text)
    {
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;

        var accepted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("acceptedDocuments", out var acceptedDocuments) && acceptedDocuments.ValueKind == JsonValueKind.Array)
        {
            foreach (var document in acceptedDocuments.EnumerateArray())
            {
                accepted.Add(document.GetProperty("uuid").GetString()!);
            }
        }

        var rejected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("rejectedDocuments", out var rejectedDocuments) && rejectedDocuments.ValueKind == JsonValueKind.Array)
        {
            foreach (var document in rejectedDocuments.EnumerateArray())
            {
                var uuid = document.GetProperty("uuid").GetString()!;
                rejected[uuid] = document.TryGetProperty("error", out var error) ? DescribeError(error) : "Rejected by ETA.";
            }
        }

        return new EtaSubmitResult(accepted, rejected, null);
    }

    /// <summary>"Validation Error: CF313 Issuance date time value is out of range (DatetimeIssued)".</summary>
    private static string DescribeError(JsonElement error)
    {
        var parts = new List<string>();
        if (error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
        {
            parts.Add(message.GetString()!);
        }

        if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
        {
            foreach (var detail in details.EnumerateArray())
            {
                var code = detail.TryGetProperty("code", out var c) ? c.GetString() : null;
                var text = detail.TryGetProperty("message", out var m) ? m.GetString() : null;
                var target = detail.TryGetProperty("target", out var t) ? t.GetString() : null;
                parts.Add($"{code} {text}{(target is null ? string.Empty : $" ({target})")}".Trim());
            }
        }

        return parts.Count == 0 ? error.GetRawText() : string.Join("; ", parts);
    }

    private static string Describe(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "no details";
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                return error.ValueKind == JsonValueKind.String ? error.GetString()! : DescribeError(error);
            }
        }
        catch (JsonException)
        {
            // Not JSON (an HTML error page): fall through to the trimmed text.
        }

        return body.Length <= 300 ? body : body[..300];
    }

}

public sealed record CachedToken(string Key, string AccessToken, DateTimeOffset ExpiresAt, string? Error);

/// <summary>Keeps the ETA login token for the app's lifetime (the HTTP client itself is short-lived).</summary>
public sealed class EtaTokenCache
{
    public SemaphoreSlim Lock { get; } = new(1, 1);

    public CachedToken? Current { get; set; }
}
