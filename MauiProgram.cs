using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using Physiquinator.Services;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models.AndroidOption;

namespace Physiquinator;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		SQLitePCL.Batteries_V2.Init();

		var builder = MauiApp.CreateBuilder();
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
			config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
			config.SnackbarConfiguration.VisibleStateDuration = 3000;
			config.SnackbarConfiguration.HideTransitionDuration = 100;
			config.SnackbarConfiguration.ShowTransitionDuration = 100;
			config.SnackbarConfiguration.PreventDuplicates = true;
		});

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "physiquinator.db3");
		builder.Services.AddSingleton(TimeProvider.System);
		builder.Services.AddSingleton(new Data.AppDatabase(dbPath));
		builder.Services.AddSingleton<Data.WorkoutPlanRepository>();
		builder.Services.AddSingleton<Data.WorkoutHistoryRepository>();
		builder.Services.AddSingleton<Services.WorkoutHistoryService>();
		builder.Services.AddSingleton<Services.WorkoutPlanService>();
		builder.Services.AddSingleton<Services.WorkoutSessionService>();
		builder.Services.AddSingleton<Services.RestAlertSettingsService>();
		builder.Services.AddSingleton<Services.RestNotificationService>();
		builder.Services.AddSingleton<Services.IDemoSeedPreferences, Services.MauiDemoSeedPreferences>();
		builder.Services.AddSingleton<Services.DemoDataSeeder>();
		builder.Services.AddScoped<Services.AppDataResetService>();
		builder.Services.AddScoped<Services.ThemeService>();

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
