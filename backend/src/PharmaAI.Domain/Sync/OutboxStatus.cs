namespace PharmaAI.Domain.Sync;

public enum OutboxStatus
{
    /// <summary>Waiting to be sent, or waiting for its next retry after a temporary failure.</summary>
    Pending = 1,

    /// <summary>Accepted by the receiving system. Final.</summary>
    Sent = 2,

    /// <summary>Refused by the receiving system for a reason a retry cannot fix; needs a person.</summary>
    Rejected = 3,
}
