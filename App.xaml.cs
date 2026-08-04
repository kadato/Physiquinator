using Physiquinator.Services;

namespace Physiquinator;

public partial class App : Application
{
	private readonly WorkoutSessionService _sessionService;

	public App(WorkoutSessionService sessionService)
	{
		InitializeComponent();
		_sessionService = sessionService;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage());

		window.Activated += (_, _) => _sessionService.NotifyAppActivated();

		return window;
	}
}
