using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PharmaAI.Application.Behaviors;
using PharmaAI.Application.Common;

namespace PharmaAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PharmacyCalendar>();

        return services;
    }
}
