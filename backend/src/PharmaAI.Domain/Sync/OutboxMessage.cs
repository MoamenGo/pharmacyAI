using PharmaAI.Domain.Common;

namespace PharmaAI.Domain.Sync;

/// <summary>
/// Something that must reach an outside system (the tax authority) once the pharmacy is online.
/// It is written in the same transaction as the sale it describes, so a sale can never be
/// saved without its message, and sending it later never blocks the till.
/// </summary>
public sealed class OutboxMessage
{
    public const string EtaReceiptKind = "eta.receipt";
    public const int ErrorMaxLength = 2000;

    /// <summary>Retries back off from 30 seconds, doubling, up to one hour between attempts.</summary>
    public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(1);

    private OutboxMessage()
    {
        Kind = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Kind { get; private set; }

    /// <summary>The record the message is about, e.g. the sale id.</summary>
    public Guid SubjectId { get; private set; }

    /// <summary>Messages are sent strictly in this order (the sale number for receipts).</summary>
    public long Sequence { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public OutboxStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTime NextAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public DateTime? SentAtUtc { get; private set; }

    /// <summary>The document as first built for sending; retries resend exactly this.</summary>
    public string? Document { get; private set; }

    /// <summary>The receiving system's id for the document (the receipt UUID for ETA).</summary>
    public string? ExternalId { get; private set; }

    public static OutboxMessage ForEtaReceipt(Guid saleId, long saleNumber, DateTime createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            Kind = EtaReceiptKind,
            SubjectId = saleId,
            Sequence = saleNumber,
            CreatedAtUtc = createdAtUtc,
            Status = OutboxStatus.Pending,
            NextAttemptAtUtc = createdAtUtc,
        };

    public bool IsDue(DateTime nowUtc) => Status == OutboxStatus.Pending && NextAttemptAtUtc <= nowUtc;

    /// <summary>Freezes the document and its id the first time it is built.</summary>
    public void AttachDocument(string document, string externalId)
    {
        if (Document is not null)
        {
            throw new DomainException("outbox.document_frozen", "The document was already built and cannot change.");
        }

        Document = document;
        ExternalId = externalId;
    }

    public void MarkSent(DateTime nowUtc)
    {
        EnsurePending();
        Attempts++;
        Status = OutboxStatus.Sent;
        SentAtUtc = nowUtc;
        LastError = null;
    }

    public void MarkRejected(string reason, DateTime nowUtc)
    {
        EnsurePending();
        Attempts++;
        Status = OutboxStatus.Rejected;
        LastError = Truncate(reason);
        NextAttemptAtUtc = nowUtc;
    }

    public void MarkFailedTemporarily(string reason, DateTime nowUtc)
    {
        EnsurePending();
        Attempts++;
        LastError = Truncate(reason);
        var delay = TimeSpan.FromTicks(Math.Min(FirstRetryDelay.Ticks * (1L << Math.Min(Attempts - 1, 20)), MaxRetryDelay.Ticks));
        NextAttemptAtUtc = nowUtc + delay;
    }

    /// <summary>Sends a rejected message again (after a person fixed the cause) or a pending one right away.</summary>
    public void RetryNow(DateTime nowUtc, bool rebuildDocument)
    {
        if (Status == OutboxStatus.Sent)
        {
            throw new DomainException("outbox.already_sent", "This message was already accepted.");
        }

        Status = OutboxStatus.Pending;
        NextAttemptAtUtc = nowUtc;
        if (rebuildDocument)
        {
            Document = null;
            ExternalId = null;
        }
    }

    private void EnsurePending()
    {
        if (Status != OutboxStatus.Pending)
        {
            throw new DomainException("outbox.not_pending", $"Message {Id} is {Status}, not pending.");
        }
    }

    private static string Truncate(string reason) => reason.Length <= ErrorMaxLength ? reason : reason[..ErrorMaxLength];
}
