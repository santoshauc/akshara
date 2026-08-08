using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application.Common.Behaviors;

namespace SchoolErp.Application;

/// <summary>Composition root for the application layer (CQRS pipeline).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            // After validation: only valid, successfully handled commands are audited.
            cfg.AddOpenBehavior(typeof(Audit.AuditBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<Parent.ParentAccess>();
        services.AddScoped<Transport.DriverAccess>();

        // Non-HTTP scopes (jobs, tests) have no caller IP; the API host
        // registers its HTTP-backed implementation after this (last wins).
        services.AddScoped<Abstractions.IClientContext, Abstractions.NullClientContext>();

        return services;
    }
}
