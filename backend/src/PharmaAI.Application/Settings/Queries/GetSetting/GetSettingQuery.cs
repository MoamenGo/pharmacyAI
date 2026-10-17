using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;

namespace PharmaAI.Application.Settings.Queries.GetSetting;

public sealed record GetSettingQuery(string Key) : IRequest<SettingDto?>;

public sealed class GetSettingQueryHandler(IPharmaDbContext db) : IRequestHandler<GetSettingQuery, SettingDto?>
{
    public Task<SettingDto?> Handle(GetSettingQuery request, CancellationToken cancellationToken) =>
        db.AppSettings
            .AsNoTracking()
            .Where(s => s.Key == request.Key)
            .Select(s => new SettingDto(s.Key, s.Value, s.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
}
