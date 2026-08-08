using System.Globalization;
using System.Text;
using Asp.Versioning;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SchoolErp.Api.Authorization;
using SchoolErp.Api.Middleware;
using SchoolErp.Api.Services;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Infrastructure;
using Serilog;

// Bootstrap logger so startup failures are captured before configuration loads.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // --- Logging -----------------------------------------------------------
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // --- Layers ------------------------------------------------------------
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // --- Background jobs (Hangfire) -----------------------------------------
    // Storage uses the OWNER connection: Hangfire creates its own "hangfire"
    // schema, which the restricted runtime role may not. Jobs themselves still
    // resolve AppDbContext on the normal app connection (RLS intact).
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(
            builder.Configuration.GetConnectionString("PostgresMigrations"))));
    builder.Services.AddHangfireServer(options =>
    {
        options.SchedulePollingInterval = TimeSpan.FromSeconds(5);
        options.WorkerCount = 4;
    });

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    // Overrides AddApplication's NullClientContext (last registration wins).
    builder.Services.AddScoped<SchoolErp.Application.Abstractions.IClientContext, HttpClientContext>();

    // --- Authentication & authorization -------------------------------------
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var jwt = builder.Configuration.GetSection("Jwt");
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwt["Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwt["SigningKey"] ?? string.Empty)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

    // --- Web ---------------------------------------------------------------
    builder.Services.AddControllers(options =>
        // Subscription-plan module gating (see RequiresModuleAttribute).
        options.Filters.Add<SchoolErp.Api.Authorization.ModuleGateFilter>());

    builder.Services
        .AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("global", limiter =>
        {
            limiter.PermitLimit = 300;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
        });
        // Credential endpoints get a much tighter budget (brute-force defense).
        options.AddFixedWindowLimiter("auth", limiter =>
        {
            limiter.PermitLimit = 10;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
        });
    });

    builder.Services.AddCors(options => options.AddPolicy("Portal", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    }));

    // --- Health ------------------------------------------------------------
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("Postgres")!,
            name: "postgres",
            tags: ["ready"])
        .AddRedis(
            builder.Configuration.GetConnectionString("Redis")!,
            name: "redis",
            tags: ["ready"]);

    var app = builder.Build();

    // --- Pipeline ----------------------------------------------------------
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    // Minimal secure-header baseline; CSP is added when the portal is wired up.
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        await next();
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        // Local-only by default (Hangfire's built-in dashboard authorization).
        app.UseHangfireDashboard("/jobs");
        await SchoolErp.Infrastructure.Persistence.Seeding.DevSeeder.SeedAsync(app.Services);
    }

    // The outbox delivery loop: every 15 seconds, one batch per run.
    // DI-based manager, NOT the static RecurringJob API — the static storage
    // is only initialized as a dashboard side effect and crashes in
    // production where the dashboard isn't mapped.
    using (var jobScope = app.Services.CreateScope())
    {
        jobScope.ServiceProvider.GetRequiredService<IRecurringJobManager>()
            .AddOrUpdate<SchoolErp.Infrastructure.Notifications.OutboxDispatchJob>(
                "outbox-dispatch", job => job.RunAsync(CancellationToken.None), "*/15 * * * * *");
        // Nightly fee-due reminders at 03:00 UTC (08:30 IST), before school opens.
        jobScope.ServiceProvider.GetRequiredService<IRecurringJobManager>()
            .AddOrUpdate<SchoolErp.Infrastructure.Notifications.FeeReminderJob>(
                "fee-due-reminders", job => job.RunAsync(CancellationToken.None), "0 3 * * *");
    }

    app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseCors("Portal");

    app.UseAuthentication();
    // Tenant resolution runs after authentication so the JWT tenant claim is
    // available, and before authorization/business logic.
    app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseAuthorization();

    app.MapControllers().RequireRateLimiting("global");
    // Liveness must not depend on downstream services — a database blip should
    // fail READINESS (stop routing traffic), not get the process restarted.
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    });

    // The API serves no UI at "/" — send humans to the docs (dev) or identify
    // the service instead of showing a blank 404.
    app.MapGet("/", (IWebHostEnvironment env) =>
        env.IsDevelopment()
            ? Results.Redirect("/swagger")
            : Results.Json(new
            {
                service = "SchoolErp API",
                health = "/health/live",
                api = "/api/v1",
            }));

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown by EF Core design-time tooling and is expected.
    Log.Fatal(ex, "SchoolErp API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
