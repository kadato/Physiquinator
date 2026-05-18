using Microsoft.AspNetCore.Components.WebView;
using Physiquinator.Services;

namespace Physiquinator;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		try
		{
			InitializeComponent();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"MainPage init error: {ex}");
			throw;
		}

		blazorWebView.BlazorWebViewInitializing += (s, e) =>
			System.Diagnostics.Debug.WriteLine("BlazorWebView Initializing");
		blazorWebView.BlazorWebViewInitialized += OnBlazorWebViewInitialized;
		blazorWebView.UrlLoading += (s, e) =>
			System.Diagnostics.Debug.WriteLine($"URL Loading: {e.Url}");
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		SystemBarsHelper.ApplyFromCurrentResources();
	}

	private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs args)
	{
		System.Diagnostics.Debug.WriteLine("BlazorWebView Initialized");
		try
		{
			WebViewLogger.AttachConsoleLogging(args.WebView);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"BlazorWebView init error: {ex}");
		}
	}
}
