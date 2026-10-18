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

    /// <summary>The document as built for sending; retries resend exactly this text.</summary>
    public string? Document { get; private set; }

    /// <summary>The receiving system's id for the current document (the receipt UUID for ETA).</summary>
    public string? ExternalId { get; private set; }

    /// <summary>The id of the message before this one in the chain (ETA previousUUID); empty for the first.</summary>
    public string? PreviousExternalId { get; private set; }

    /// <summary>The id this message was first issued under. Later messages chain to it, even after a correction.</summary>
    public string? FirstExternalId { get; private set; }

    /// <summary>When a rejected document is rebuilt, the id it replaces (ETA referenceOldUUID).</summary>
    public string? ReplacedExternalId { get; private set; }

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

    /// <summary>
    /// Freezes the document and its id. The chain position is fixed by the first build:
    /// a rebuilt document must name the same predecessor.
    /// </summary>
    public void AttachDocument(string document, string externalId, string previousExternalId)
    {
        if (Document is not null)
        {
            throw new DomainException("outbox.document_frozen", "The document was already built and cannot change.");
        }

        if (PreviousExternalId is not null && PreviousExternalId != previousExternalId)
        {
            throw new DomainException("outbox.chain_changed", "A rebuilt document must keep its place in the chain.");
        }

        Document = document;
        ExternalId = externalId;
        PreviousExternalId = previousExternalId;
        FirstExternalId ??= externalId;
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

    /// <summary>
    /// Sends the message again right away. A rejected document is rebuilt (after a person fixed the
    /// cause) under a new id that references the rejected one; a pending one is resent as it is.
    /// </summary>
    public void RetryNow(DateTime nowUtc)
    {
        if (Status == OutboxStatus.Sent)
        {
            throw new DomainException("outbox.already_sent", "This message was already accepted.");
        }

        if (Status == OutboxStatus.Rejected && Document is not null)
        {
            ReplacedExternalId = ExternalId;
            Document = null;
            ExternalId = null;
        }

        Status = OutboxStatus.Pending;
        NextAttemptAtUtc = nowUtc;
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
