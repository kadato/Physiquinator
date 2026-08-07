using MudBlazor.Services;
using Physiquinator.Core.Services;
using Physiquinator.Web.Components;
using Physiquinator.Web.Mcp;
using Physiquinator.Web.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddPhysiquinatorServices(
    new WebAppPreferences(),
    new WebDatabasePathProvider(),
    scopeStatefulServicesPerCircuit: true);

builder.Services.AddSingleton(_ => new HttpClient());
builder.Services.AddSingleton<INotificationService, NoopNotificationService>();
builder.Services.AddSingleton<IVibrationService, NoopVibrationService>();
builder.Services.AddSingleton<IFileTransferService, WebFileTransferService>();

builder.Services.AddSingleton<IAppUpdateService, NoopAppUpdateService>();

builder.Services.AddPhysiquinatorMcpServer(builder.Configuration);
builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAntiforgery();
app.UsePhysiquinatorMcpApiKey();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Physiquinator.UI.Routes).Assembly);

app.MapPhysiquinatorMcp(builder.Configuration);
app.MapHealthChecks("/healthz");

await app.RunAsync();
