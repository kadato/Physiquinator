namespace Physiquinator;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

		blazorWebView.BlazorWebViewInitializing += (sender, args) =>
		{
			System.Diagnostics.Debug.WriteLine("BlazorWebView Initializing");
		};

		blazorWebView.BlazorWebViewInitialized += (sender, args) =>
		{
			System.Diagnostics.Debug.WriteLine("BlazorWebView Initialized");
		};

		blazorWebView.UrlLoading += (sender, args) =>
		{
			System.Diagnostics.Debug.WriteLine($"URL Loading: {args.Url}");
		};
	}
}
