namespace Physiquinator;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage());

		// When the window loses focus, the Blazor WebView may stop firing JS timers.
		// Rest still advances in real time via wall-clock end in WorkoutSessionService.
		window.Deactivated += (_, _) =>
		{
			var session = Handler?.MauiContext?.Services.GetService<Services.WorkoutSessionService>();
			session?.SuspendRest();
		};

		window.Activated += (_, _) =>
		{
			var session = Handler?.MauiContext?.Services.GetService<Services.WorkoutSessionService>();
			session?.NotifyAppActivated();
		};

		return window;
	}
}
