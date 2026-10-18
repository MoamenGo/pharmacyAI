using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Sync.Eta;
using PharmaAI.Domain.Sync;

namespace PharmaAI.Application.Sync.Queries.GetSyncStatus;

public sealed record GetSyncStatusQuery : IRequest<SyncStatusDto>;

/// <summary>A receipt the pharmacist should look at: rejected, or failing to send.</summary>
public sealed record SyncProblemDto(Guid Id, long SaleNumber, OutboxStatus Status, int Attempts, string? LastError, DateTime CreatedAtUtc, DateTime NextAttemptAtUtc);

public sealed record SyncStatusDto(
    bool Enabled,
    IReadOnlyList<string> MissingFields,
    int Pending,
    int Sent,
    int Rejected,
    DateTime? OldestPendingAtUtc,
    DateTime? LastSentAtUtc,
    IReadOnlyList<SyncProblemDto> Problems);

public sealed class GetSyncStatusQueryHandler(IPharmaDbContext db) : IRequestHandler<GetSyncStatusQuery, SyncStatusDto>
{
    public const int MaxProblems = 50;

    public async Task<SyncStatusDto> Handle(GetSyncStatusQuery request, CancellationToken cancellationToken)
    {
        var settings = await EtaSettingsStore.LoadAsync(db, cancellationToken);
        var messages = db.OutboxMessages.AsNoTracking().Where(m => m.Kind == OutboxMessage.EtaReceiptKind);

        var counts = await messages
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        var problems = await messages
            .Where(m => m.Status == OutboxStatus.Rejected || (m.Status == OutboxStatus.Pending && m.LastError != null))
            .OrderBy(m => m.Sequence)
            .Take(MaxProblems)
            .Select(m => new SyncProblemDto(m.Id, m.Sequence, m.Status, m.Attempts, m.LastError, m.CreatedAtUtc, m.NextAttemptAtUtc))
            .ToListAsync(cancellationToken);

        return new SyncStatusDto(
            settings.Enabled,
            settings.MissingFields(),
            counts.GetValueOrDefault(OutboxStatus.Pending),
            counts.GetValueOrDefault(OutboxStatus.Sent),
            counts.GetValueOrDefault(OutboxStatus.Rejected),
            await messages.Where(m => m.Status == OutboxStatus.Pending).MinAsync(m => (DateTime?)m.CreatedAtUtc, cancellationToken),
            await messages.Where(m => m.Status == OutboxStatus.Sent).MaxAsync(m => m.SentAtUtc, cancellationToken),
            problems);
    }
}
