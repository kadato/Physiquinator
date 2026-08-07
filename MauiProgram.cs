using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
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
        SQLitePCL.Batteries_V2.Init();

        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseLocalNotification(config =>
            {
                config.AddAndroid(android =>
                {
                    android.AddChannel(new AndroidNotificationChannelRequest
                    {
                        Id = RestNotificationService.AndroidChannelId,
                        Name = "Rest timer",
                        Description = "Alerts when rest periods end",
                        Importance = AndroidImportance.High
                    });
                });
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
            config.SnackbarConfiguration.VisibleStateDuration = 3000;
            config.SnackbarConfiguration.HideTransitionDuration = 100;
            config.SnackbarConfiguration.ShowTransitionDuration = 100;
            config.SnackbarConfiguration.PreventDuplicates = true;
            config.SnackbarConfiguration.ShowCloseIcon = true;
        });

        builder.Services.AddPhysiquinatorServices(
            new AppPreferences(),
            new DatabasePathProvider());

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

    public static MauiApp? CreateMauiAppSafe()
    {
        try
        {
            return CreateMauiApp();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FATAL: MauiProgram.CreateMauiApp failed: {ex}");
            throw;
        }
    }
}
