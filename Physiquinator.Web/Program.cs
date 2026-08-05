using MudBlazor.Services;
using Physiquinator.Core.Services;
using Physiquinator.Web.Components;
using Physiquinator.Web.Services;

var builder = WebApplication.CreateBuilder(args);

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
    new WebDatabasePathProvider());

builder.Services.AddSingleton<INotificationService, NoopNotificationService>();
builder.Services.AddSingleton<IVibrationService, NoopVibrationService>();
builder.Services.AddSingleton<IFileTransferService, WebFileTransferService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Physiquinator.UI.Routes).Assembly);

await app.RunAsync();
