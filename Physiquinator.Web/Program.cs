using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using MudBlazor.Services;
using Physiquinator.Core.Data;
using Physiquinator.Core.Services;
using Physiquinator.Web.Components;
using Physiquinator.Web.Mcp;
using Physiquinator.Web.Services;
using SQLitePCL;
using System.Threading.RateLimiting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Container platforms (Render, Fly, ...) inject a PORT env var and route traffic
// to it. Bind there so the platform proxy can reach Kestrel; fall back to
// ASPNETCORE_URLS when PORT is unset.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port) && int.TryParse(port, out var containerPort))
{
    builder.WebHost.UseKestrel(options => options.ListenAnyIP(containerPort));
}

Batteries_V2.Init();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomLeft;
    config.SnackbarConfiguration.VisibleStateDuration = 3000;
    config.SnackbarConfiguration.HideTransitionDuration = 100;
    config.SnackbarConfiguration.ShowTransitionDuration = 100;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "physiquinator-auth";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton(new WebUserStore(
    Path.Combine(WebDatabasePathProvider.ResolveDatabaseDirectory(), "physiquinator-users.db3")));
builder.Services.AddScoped<WebUserContext>();

builder.Services.AddPhysiquinatorServices(
    new WebAppPreferences(),
    new WebDatabasePathProvider(),
    scopeStatefulServicesPerCircuit: true);

// Per-account database files: registered after Core's registration so they win.
builder.Services.AddScoped<IDatabasePathProvider, WebUserDatabasePathProvider>();

// Sign-out is web-only; registered after Core's no-op default so it wins.
builder.Services.AddScoped<IAccountService, WebAccountService>();

builder.Services.AddSingleton(_ => new HttpClient());
builder.Services.AddSingleton<INotificationService, NoopNotificationService>();
builder.Services.AddSingleton<IVibrationService, NoopVibrationService>();
builder.Services.AddSingleton<IFileTransferService, WebFileTransferService>();

builder.Services.AddSingleton<IAppUpdateService, NoopAppUpdateService>();

builder.Services.AddScoped<WebDbSyncService>();

builder.Services.AddHttpLogging(options =>
{
    // No headers/cookies in logs - the auth cookie must never be written to logs.
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.RequestQuery
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("mcp", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("restore", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddHealthChecks().AddCheck<WebStorageHealthCheck>("storage");

builder.Services.AddPhysiquinatorMcpServer(builder.Configuration);

WebApplication app = builder.Build();

// PaaS routers (Render, Fly, ...) terminate TLS and forward the original
// scheme via X-Forwarded-Proto, so trust those headers. Known-proxy lists stay
// empty (the documented pattern for routers with dynamic IPs).
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpLogging();
app.UsePhysiquinatorSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UsePhysiquinatorMcpApiKey();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Physiquinator.UI.Routes).Assembly);

app.MapPhysiquinatorMcp(builder.Configuration);
app.MapPhysiquinatorBrowserDbRestore();
app.MapPhysiquinatorAuth();
app.MapHealthChecks("/healthz");

if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["Mcp:ApiKey"]))
{
    app.Logger.LogWarning("Mcp:ApiKey is not configured: the /mcp agent endpoint will reject all requests in production.");
}

await app.RunAsync();
