using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Sync.Eta;
using PharmaAI.Domain.Sync;

namespace PharmaAI.Application.Sync.Commands.ProcessOutbox;

/// <summary>
/// Builds receipts strictly in sale order, because each one names the one before it (previousUUID),
/// then submits the due ones in one batch. Only the background sender runs this, one pass at a time.
/// </summary>
public sealed class ProcessOutboxCommandHandler(IPharmaDbContext db, IEtaClient eta, TimeProvider clock)
    : IRequestHandler<ProcessOutboxCommand, ProcessOutboxResult>
{
    /// <summary>ETA accepts up to 500 receipts or 1.5 MB per call; 100 small pharmacy receipts stays well inside both.</summary>
    public const int BatchSize = 100;

    public async Task<ProcessOutboxResult> Handle(ProcessOutboxCommand request, CancellationToken cancellationToken)
    {
        var settings = await EtaSettingsStore.LoadAsync(db, cancellationToken);
        if (!settings.CanSend)
        {
            return ProcessOutboxResult.NotConfigured;
        }

        var built = await BuildPendingAsync(settings, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        var due = await db.OutboxMessages
            .Where(m => m.Kind == OutboxMessage.EtaReceiptKind
                && m.Status == OutboxStatus.Pending
                && m.Document != null
                && m.NextAttemptAtUtc <= now)
            .OrderBy(m => m.Sequence)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return new ProcessOutboxResult(true, built, 0, 0, 0);
        }

        var result = await eta.SubmitAsync(settings, due.Select(m => m.Document!).ToList(), cancellationToken);
        now = clock.GetUtcNow().UtcDateTime;
        int sent = 0, rejected = 0, failed = 0;

        foreach (var message in due)
        {
            if (result.BatchError is { } batchError)
            {
                message.MarkFailedTemporarily(batchError, now);
                failed++;
            }
            else if (result.Accepted.Contains(message.ExternalId!))
            {
                message.MarkSent(now);
                sent++;
            }
            else if (result.Rejected.TryGetValue(message.ExternalId!, out var reason))
            {
                message.MarkRejected(reason, now);
                rejected++;
            }
            else
            {
                message.MarkFailedTemporarily("ETA did not acknowledge this receipt.", now);
                failed++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ProcessOutboxResult(true, built, sent, rejected, failed);
    }

    /// <summary>
    /// Freezes a document for every pending message that has none, oldest first. A new message
    /// chains to the receipt before it; a rebuilt (corrected) one keeps its original predecessor.
    /// Each is saved before the next is built, so the next one can read its UUID.
    /// </summary>
    private async Task<int> BuildPendingAsync(EtaSettings settings, CancellationToken cancellationToken)
    {
        var unbuilt = await db.OutboxMessages
            .Where(m => m.Kind == OutboxMessage.EtaReceiptKind && m.Status == OutboxStatus.Pending && m.Document == null)
            .OrderBy(m => m.Sequence)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var built = 0;
        foreach (var message in unbuilt)
        {
            var previous = message.PreviousExternalId ?? await ChainPredecessorAsync(message.Sequence, cancellationToken);
            if (previous is null)
            {
                break;
            }

            var sale = await db.Sales
                .AsNoTracking()
                .Include(s => s.Lines)
                .SingleAsync(s => s.Id == message.SubjectId, cancellationToken);
            var drugIds = sale.Lines.Select(l => l.DrugId).Distinct().ToList();
            var drugs = await db.Drugs.AsNoTracking().Where(d => drugIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken);

            var receipt = EtaReceiptBuilder.Build(sale, drugs, settings, previous, message.ReplacedExternalId);
            message.AttachDocument(receipt.Json, receipt.Uuid, previous);
            await db.SaveChangesAsync(cancellationToken);
            built++;
        }

        return built;
    }

    /// <summary>
    /// The UUID the receipt before <paramref name="sequence"/> was first issued under; empty when
    /// there is none (this device's first receipt); null when that receipt is not built yet.
    /// </summary>
    private async Task<string?> ChainPredecessorAsync(long sequence, CancellationToken cancellationToken)
    {
        var predecessor = await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Kind == OutboxMessage.EtaReceiptKind && m.Sequence < sequence)
            .OrderByDescending(m => m.Sequence)
            .Select(m => new { m.FirstExternalId })
            .FirstOrDefaultAsync(cancellationToken);

        return predecessor is null ? string.Empty : predecessor.FirstExternalId;
    }
}
