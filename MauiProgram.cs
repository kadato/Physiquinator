using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using Physiquinator.Core.Data;
using Physiquinator.Core.Services;
using Physiquinator.Services;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models.AndroidOption;
#if ANDROID
using Physiquinator.Platforms.Android.Services;
#endif

namespace Physiquinator;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Load the native SQLite library on a background thread so it does not
        // block the main thread during cold start.  The AppDatabase will await
        // this task before running its own async initialization.
        var sqliteBatteriesReady = Task.Run(static () => SQLitePCL.Batteries_V2.Init());

        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseLocalNotification(config => config.AddAndroid(android =>
                android.AddChannel(new AndroidNotificationChannelRequest
                {
                    Id = RestNotificationService.AndroidChannelId,
                    Name = "Rest timer",
                    Description = "Alerts when rest periods end",
                    Importance = AndroidImportance.High
                })))
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();

#if WINDOWS
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebView, Physiquinator.Platforms.Windows.ScreenshotWebViewHandler>();
        });
#endif

        builder.Services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
            config.SnackbarConfiguration.VisibleStateDuration = 3000;
            config.SnackbarConfiguration.HideTransitionDuration = 100;
            config.SnackbarConfiguration.ShowTransitionDuration = 100;
            config.SnackbarConfiguration.PreventDuplicates = true;
            config.SnackbarConfiguration.ShowCloseIcon = true;
        });

        var prefs = new AppPreferences();
        var dbPathProvider = new DatabasePathProvider();

        // Register AppDatabase eagerly so it can start init while other
        // services are being wired up.  TryAddSingleton lets the shared
        // AddPhysiquinatorServices helper skip its own registration.
        builder.Services.AddSingleton(new AppDatabase(
            dbPathProvider.GetDatabasePath(
                PhysiquinatorServiceCollectionExtensions.GetActiveProfileId(prefs)),
            sqliteBatteriesReady));

        builder.Services.AddPhysiquinatorServices(prefs, dbPathProvider);

        // The MAUI shell (app theme, resource colors, system bars) must follow
        // the Blazor UI theme. Override the base registration with the MAUI
        // implementation so Apply*/Sync* hooks reach the native layer.
        builder.Services.AddScoped<Physiquinator.Core.Services.ThemeService>(sp => new MauiThemeService(
            sp.GetRequiredService<IJSRuntime>(),
            sp.GetRequiredService<Physiquinator.Core.Services.UserProfileService>(),
            sp.GetRequiredService<Physiquinator.Core.Services.IAppPreferences>()));

#if ANDROID
        builder.Services.AddSingleton<Physiquinator.Core.Services.INotificationService, AndroidRestNotificationService>();
#else
        builder.Services.AddSingleton<Physiquinator.Core.Services.INotificationService, RestNotificationService>();
#endif
        builder.Services.AddSingleton<IVibrationService, MauiVibrationService>();
        builder.Services.AddSingleton<IFileTransferService, FileTransferService>();

        builder.Services.AddSingleton(_ => new HttpClient());
        builder.Services.AddSingleton<IGitHubReleaseClient, GitHubReleaseClient>();
        builder.Services.AddSingleton<IAppUpdateInstaller, MauiAppUpdateInstaller>();
        builder.Services.AddSingleton<IAppUpdateService>(sp => new AppUpdateService(
            sp.GetRequiredService<IGitHubReleaseClient>(),
            sp.GetRequiredService<IAppUpdateInstaller>(),
            AppInfo.Current.Version));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
