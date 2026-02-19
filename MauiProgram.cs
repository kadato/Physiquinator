using Microsoft.Extensions.Logging;

namespace Physiquinator;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// Force Blazor WebView to use 127.0.0.1 instead of 0.0.0.1 (browsers block 0.0.0.x → ERR_ADDRESS_UNREACHABLE).
		Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "https://127.0.0.1:0");

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddSingleton<Services.WorkoutPlanService>();
		builder.Services.AddSingleton<Services.WorkoutSessionService>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
