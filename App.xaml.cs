namespace Physiquinator;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
		{
			var ex = args.ExceptionObject as Exception;
			Services.CrashLogger.Log("UnhandledException", ex ?? new Exception(args.ExceptionObject?.ToString()));
		};

		TaskScheduler.UnobservedTaskException += (sender, args) =>
		{
			Services.CrashLogger.Log("UnobservedTaskException", args.Exception);
			args.SetObserved();
		};
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage());

		// Pause the rest timer when backgrounded so Android's battery optimizer
		// does not kill the process due to continuous background CPU work.
		window.Deactivated += (_, _) =>
		{
			var session = Handler?.MauiContext?.Services.GetService<Services.WorkoutSessionService>();
			session?.SuspendRest();
		};

		window.Activated += (_, _) =>
		{
			var session = Handler?.MauiContext?.Services.GetService<Services.WorkoutSessionService>();
			session?.ResumeRest();
		};

		return window;
	}
}
