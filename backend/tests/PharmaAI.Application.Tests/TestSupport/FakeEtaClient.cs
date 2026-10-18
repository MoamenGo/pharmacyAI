using System.Text.Json;
using PharmaAI.Application.Sync.Eta;

namespace PharmaAI.Application.Tests.TestSupport;

/// <summary>Accepts every receipt unless told to reject a receipt number or to fail the whole call.</summary>
public sealed class FakeEtaClient : IEtaClient
{
    public List<IReadOnlyList<string>> Calls { get; } = [];

    public HashSet<string> RejectReceiptNumbers { get; } = [];

    public string? FailWith { get; set; }

    public Task<EtaSubmitResult> SubmitAsync(EtaSettings settings, IReadOnlyList<string> receipts, CancellationToken cancellationToken)
    {
        Calls.Add(receipts);
        if (FailWith is not null)
        {
            return Task.FromResult(EtaSubmitResult.Failed(FailWith));
        }

        var accepted = new HashSet<string>();
        var rejected = new Dictionary<string, string>();
        foreach (var receipt in receipts)
        {
            using var json = JsonDocument.Parse(receipt);
            var header = json.RootElement.GetProperty("header");
            var uuid = header.GetProperty("uuid").GetString()!;
            if (RejectReceiptNumbers.Contains(header.GetProperty("receiptNumber").GetString()!))
            {
                rejected[uuid] = "CF313 Issuance date time value is out of range";
            }
            else
            {
                accepted.Add(uuid);
            }
        }

        return Task.FromResult(new EtaSubmitResult(accepted, rejected, null));
    }
}
