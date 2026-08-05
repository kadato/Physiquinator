using Physiquinator.Core.Services;

namespace Physiquinator;

public partial class App : Application
{
	private readonly WorkoutSessionService _sessionService;

	public App(WorkoutSessionService sessionService, RestTimerCoordinator restTimer)
	{
		InitializeComponent();
		_sessionService = sessionService;
		restTimer.EnsureInitialState();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage());

		window.Activated += (_, _) => _sessionService.NotifyAppActivated();

		return window;
	}
}
