using MediatR;

namespace PharmaAI.Application.Settings.Commands.UpsertSetting;

public sealed record UpsertSettingCommand(string Key, string Value) : IRequest<SettingDto>;
