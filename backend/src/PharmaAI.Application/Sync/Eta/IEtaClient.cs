namespace PharmaAI.Application.Sync.Eta;

/// <summary>The tax authority's answer to one submission, per receipt UUID.</summary>
/// <param name="BatchError">Set when the whole call failed (offline, login refused, server error): every receipt is retried later.</param>
public sealed record EtaSubmitResult(
    IReadOnlySet<string> Accepted,
    IReadOnlyDictionary<string, string> Rejected,
    string? BatchError)
{
    public static EtaSubmitResult Failed(string reason) =>
        new(new HashSet<string>(), new Dictionary<string, string>(), reason);
}

public interface IEtaClient
{
    /// <param name="receipts">Receipt JSON texts, sent byte-for-byte as hashed.</param>
    Task<EtaSubmitResult> SubmitAsync(EtaSettings settings, IReadOnlyList<string> receipts, CancellationToken cancellationToken);
}
