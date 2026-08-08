using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using SchoolErp.AdminPortal;
using SchoolErp.AdminPortal.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();

// --- Auth plumbing ---------------------------------------------------------
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<ApiAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<ApiAuthenticationStateProvider>());

var apiBaseUrl = new Uri(builder.Configuration["ApiBaseUrl"]
    ?? builder.HostEnvironment.BaseAddress);

// Bare client for auth endpoints — must not recurse into the refresh handler.
builder.Services.AddScoped(sp => new AuthApiClient(
    new HttpClient { BaseAddress = apiBaseUrl },
    sp.GetRequiredService<TokenStore>(),
    sp.GetRequiredService<ApiAuthenticationStateProvider>()));

// Authenticated client for everything else.
builder.Services.AddScoped(sp => new HttpClient(
    new AuthMessageHandler(
        sp.GetRequiredService<TokenStore>(),
        sp.GetRequiredService<AuthApiClient>())
    {
        InnerHandler = new HttpClientHandler(),
    })
{
    BaseAddress = apiBaseUrl,
});

builder.Services.AddScoped<TenantsClient>();
builder.Services.AddScoped<AcademicsClient>();
builder.Services.AddScoped<StudentsClient>();
builder.Services.AddScoped<AttendanceClient>();
builder.Services.AddScoped<ExamsClient>();
builder.Services.AddScoped<FeesClient>();
builder.Services.AddScoped<CommsClient>();
builder.Services.AddScoped<TransportClient>();
builder.Services.AddScoped<TimetableClient>();
builder.Services.AddScoped<StaffClient>();
builder.Services.AddScoped<AuditClient>();
builder.Services.AddScoped<SessionsClient>();
builder.Services.AddScoped<MfaClient>();
builder.Services.AddScoped<LibraryClient>();
builder.Services.AddScoped<HostelClient>();
builder.Services.AddScoped<UserAdminClient>();

await builder.Build().RunAsync();
