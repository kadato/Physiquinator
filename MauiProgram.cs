using Microsoft.Extensions.Logging;

namespace Physiquinator;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		SQLitePCL.Batteries_V2.Init();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "physiquinator.db3");
		builder.Services.AddSingleton(new Data.AppDatabase(dbPath));
		builder.Services.AddSingleton<Data.WorkoutPlanRepository>();
		builder.Services.AddSingleton<Services.WorkoutPlanService>();
		builder.Services.AddSingleton(new Services.WorkoutSessionService(
			(source, ex) => Services.CrashLogger.Log(source, ex)));
		builder.Services.AddSingleton<Services.DemoDataSeeder>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
