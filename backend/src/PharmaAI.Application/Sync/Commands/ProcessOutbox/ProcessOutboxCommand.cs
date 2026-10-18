using MediatR;

namespace PharmaAI.Application.Sync.Commands.ProcessOutbox;

/// <summary>One pass of the background sender: build any new receipts, then send the due ones.</summary>
public sealed record ProcessOutboxCommand : IRequest<ProcessOutboxResult>;

public sealed record ProcessOutboxResult(bool Configured, int Built, int Sent, int Rejected, int Failed)
{
    public static readonly ProcessOutboxResult NotConfigured = new(false, 0, 0, 0, 0);
}
