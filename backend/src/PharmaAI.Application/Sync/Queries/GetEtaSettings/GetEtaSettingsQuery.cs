using MediatR;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Sync.Eta;

namespace PharmaAI.Application.Sync.Queries.GetEtaSettings;

public sealed record GetEtaSettingsQuery : IRequest<EtaSettingsDto>;

public sealed class GetEtaSettingsQueryHandler(IPharmaDbContext db) : IRequestHandler<GetEtaSettingsQuery, EtaSettingsDto>
{
    public async Task<EtaSettingsDto> Handle(GetEtaSettingsQuery request, CancellationToken cancellationToken) =>
        EtaSettingsDto.From(await EtaSettingsStore.LoadAsync(db, cancellationToken));
}
