using global::Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Physiquinator.Core.Data;
using Physiquinator.Core.Services;
using Physiquinator.UI.Services;
using Physiquinator.Wasm;
using Physiquinator.Wasm.Services;
using SQLitePCL;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// The root component gates rendering until database files are restored from
// Cache Storage, then hosts the shared UI (same tree as MAUI and the server host).
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomLeft;
    config.SnackbarConfiguration.VisibleStateDuration = 3000;
    config.SnackbarConfiguration.HideTransitionDuration = 100;
    config.SnackbarConfiguration.ShowTransitionDuration = 100;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
});

// Preferences are constructed now (sync API) but touch localStorage only after
// Initialize runs below, once the wasm JS runtime is guaranteed available.
var preferences = new WasmAppPreferences();
IDatabasePathProvider databasePathProvider = new WasmDatabasePathProvider();

// Deferred AppDatabase: construction happens at first resolve, i.e. after
// App.razor's restore gate has put saved database bytes back into the wasm
// filesystem. Passing the factory also prevents Core from eagerly constructing
// a throwaway AppDatabase during registration (its TryAdd argument).
builder.Services.AddPhysiquinatorServices(
    preferences,
    databasePathProvider,
    appDatabaseFactory: sp =>
    {
        Batteries_V2.Init();
        var path = databasePathProvider.GetDatabasePath(
            PhysiquinatorServiceCollectionExtensions.GetActiveProfileId(sp.GetRequiredService<IAppPreferences>()));
        return new AppDatabase(path);
    });
builder.Services.AddPhysiquinatorUiServices();

// Browser platform implementations.
builder.Services.AddSingleton<WasmDbPersistence>();
builder.Services.AddSingleton<INotificationService, NoopNotificationService>();
builder.Services.AddSingleton<IVibrationService, NoopVibrationService>();
builder.Services.AddSingleton<IAppUpdateService, WasmNoopUpdateService>();
builder.Services.AddSingleton<IFileTransferService, WasmFileTransferService>();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var host = builder.Build();

preferences.Initialize(host.Services.GetRequiredService<IJSRuntime>());

await host.RunAsync();
