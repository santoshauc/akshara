using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Auth;
using SchoolErp.Infrastructure.Identity;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Persistence.Interceptors;
using SchoolErp.Infrastructure.Tenancy;

namespace SchoolErp.Infrastructure;

/// <summary>Composition root for the infrastructure layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' is not configured.");

        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        // --- Tenancy -------------------------------------------------------
        // One TenantContext instance per scope, exposed under both interfaces.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantLookup, CachedTenantLookup>();

        // --- Persistence ---------------------------------------------------
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<RlsSessionInterceptor>();

        // The factory is SCOPED (not the default singleton) because AppDbContext
        // and its interceptors depend on scoped tenancy services.
        services.AddDbContextFactory<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.EnableRetryOnFailure(maxRetryCount: 3));
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<RlsSessionInterceptor>());
        }, ServiceLifetime.Scoped);

        // The ambient per-request context, created through the same factory so
        // both paths share identical configuration. The DI scope disposes it.
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // --- Identity & authentication -------------------------------------
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = false; // uniqueness is per tenant
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= 32,
                "Jwt:SigningKey must be configured and at least 32 characters long.")
            .ValidateOnStart();

        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISmsSender, DevSmsSender>();

        // Outbox delivery core; the polling BackgroundService is registered by
        // the API host so tests can drive the processor synchronously.
        services.AddScoped<Notifications.OutboxProcessor>();

        // --- Payments ------------------------------------------------------
        services.AddScoped<IPaymentGateway, Payments.DevPaymentGateway>();
        services.AddScoped<Payments.GatewayWebhookProcessor>();

        return services;
    }
}
