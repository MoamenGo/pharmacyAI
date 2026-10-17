using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Domain.Entities;

namespace PharmaAI.Application.Settings.Commands.UpsertSetting;

public sealed class UpsertSettingCommandHandler(IPharmaDbContext db, TimeProvider clock)
    : IRequestHandler<UpsertSettingCommand, SettingDto>
{
    public async Task<SettingDto> Handle(UpsertSettingCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var setting = await db.AppSettings.SingleOrDefaultAsync(s => s.Key == request.Key, cancellationToken);

        if (setting is null)
        {
            setting = new AppSetting(request.Key, request.Value, now);
            db.AppSettings.Add(setting);
        }
        else
        {
            setting.Update(request.Value, now);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new SettingDto(setting.Key, setting.Value, setting.UpdatedAtUtc);
    }
}
