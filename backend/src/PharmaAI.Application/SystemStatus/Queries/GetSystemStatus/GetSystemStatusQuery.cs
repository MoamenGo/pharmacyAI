using MediatR;
using PharmaAI.Application.Abstractions;

namespace PharmaAI.Application.SystemStatus.Queries.GetSystemStatus;

public sealed record SystemStatusDto(string Status, string JournalMode, DateTime ServerTimeUtc);

public sealed record GetSystemStatusQuery : IRequest<SystemStatusDto>;

public sealed class GetSystemStatusQueryHandler(IDatabaseDiagnostics diagnostics, TimeProvider clock)
    : IRequestHandler<GetSystemStatusQuery, SystemStatusDto>
{
    public async Task<SystemStatusDto> Handle(GetSystemStatusQuery request, CancellationToken cancellationToken)
    {
        var journalMode = await diagnostics.GetJournalModeAsync(cancellationToken);
        var status = string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase) ? "ok" : "degraded";

        return new SystemStatusDto(status, journalMode, clock.GetUtcNow().UtcDateTime);
    }
}
