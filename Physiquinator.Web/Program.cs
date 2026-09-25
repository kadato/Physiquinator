using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using Physiquinator.Core.Data;
using Physiquinator.Core.Services;
using Physiquinator.UI.Services;
using Physiquinator.Web.Components;
using Physiquinator.Web.Mcp;
using Physiquinator.Web.Services;
using SQLitePCL;
using System.IO;
using System.Threading.RateLimiting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Container platforms (Render, Fly, ...) inject a PORT env var and route traffic
// to it. Bind there so the platform proxy can reach Kestrel. Fall back to
// ASPNETCORE_URLS when PORT is unset.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port) && int.TryParse(port, out var containerPort))
{
    builder.WebHost.UseKestrel(options => options.ListenAnyIP(containerPort));
}

Batteries_V2.Init();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    // JS interop results travel inbound over the circuit and default to a 32 KB
    // cap. The session share card returns a base64 PNG data URL several times
    // that size, and JSON backup imports can reach megabytes, so raise it.
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 8 * 1024 * 1024);

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
        // Production uses Always so the session cookie never travels over plain
        // HTTP. Development uses SameAsRequest so sign-in works over plain
        // http://localhost without a dev certificate.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
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
builder.Services.AddPhysiquinatorUiServices();

// Per-account database files: registered after Core's registration so they win.
builder.Services.AddScoped<IDatabasePathProvider, WebUserDatabasePathProvider>();

// Sign-out is web-only. Registered after Core's no-op default so it wins.
builder.Services.AddScoped<IAccountService, WebAccountService>();

builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<INotificationService, NoopNotificationService>();
builder.Services.AddSingleton<IVibrationService, NoopVibrationService>();
// Scoped: the file picker and downloads need the circuit's JS runtime.
builder.Services.AddScoped<IFileTransferService, WebFileTransferService>();

builder.Services.AddSingleton<IAppUpdateService, NoopAppUpdateService>();

builder.Services.AddScoped<WebDbSyncService>();

builder.Services.AddHttpLogging(options =>
    // No headers and cookies in logs. The auth cookie must never be written to logs.
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.RequestQuery
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration);

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

// The HSTS checklist item wants a long max-age with subdomains and no preload list.
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = false;
});

builder.Services.AddPhysiquinatorMcpServer(builder.Configuration);

WebApplication app = builder.Build();

// PaaS routers (Render, Fly, ...) terminate TLS and forward the original
// scheme via X-Forwarded-Proto, so trust those headers. Leaving the
// known-proxy lists empty matches the documented pattern for routers with
// dynamic IPs.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpLogging();
app.UsePhysiquinatorSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // Missing static files and unknown API paths re-execute the error page,
    // which echoes the original status code. Blazor-routed paths render
    // Routes.razor NotFound client-side with a 200 status instead. The
    // interactive circuit has no HttpContext to set another status from. The
    // WASM demo documents the same SPA tradeoff in wwwroot/_redirects.
    app.UseStatusCodePagesWithReExecute("/Error", "?statusCode={0}");
    app.UseHsts();
}

app.UseStaticFiles();
if (app.Environment.IsDevelopment())
{
    var mudBlazorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages/mudblazor/9.7.0/staticwebassets");
    if (Directory.Exists(mudBlazorPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(mudBlazorPath),
            RequestPath = "/_content/MudBlazor"
        });
    }
}
app.MapStaticAssets();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UsePhysiquinatorMcpApiKey();

// Add aggressive caching for fingerprinted static assets (JS, CSS, fonts, images)
// MapStaticAssets already fingerprints URLs, so long cache lifetimes are safe.
app.Use(async (context, next) =>
{
    await next();
    var path = context.Request.Path.Value ?? string.Empty;
    var isStaticAsset = path.StartsWith("/_content/", StringComparison.Ordinal)
        || path.StartsWith("/css/", StringComparison.Ordinal)
        || path.StartsWith("/js/", StringComparison.Ordinal);
    if (isStaticAsset && context.Response.StatusCode == 200 && context.Response.Headers.CacheControl.Count == 0)
    {
        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    }
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Physiquinator.UI.Routes).Assembly);

app.MapPhysiquinatorMcp(builder.Configuration);
app.MapPhysiquinatorBrowserDbRestore();
app.MapPhysiquinatorAuth();
app.MapHealthChecks("/healthz");

// The api-catalog file has no extension, so static-file middleware skips it.
// Serve the file explicitly as JSON per RFC 9727.
app.MapGet("/.well-known/api-catalog", async context =>
{
    context.Response.ContentType = "application/json";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, ".well-known", "api-catalog"));
});

if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["Mcp:ApiKey"]))
{
    app.Logger.LogWarning("Mcp:ApiKey is not configured: the /mcp agent endpoint will reject all requests in production.");
}

await app.RunAsync();
