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
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddAutoMapper(assembly);

        services.AddScoped<Parent.ParentAccess>();
        services.AddScoped<Transport.DriverAccess>();

        return services;
    }
}
