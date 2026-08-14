using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Physiquinator.Core.Services;

namespace Physiquinator.Platforms.Windows;

/// <summary>
/// Exposes the Chrome DevTools protocol endpoint while the app runs in
/// screenshot mode so Playwright can drive the WebView for docs images.
/// The WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS environment variable is no
/// longer honored by recent WebView2 runtimes, so the port is passed
/// through CoreWebView2EnvironmentOptions instead.
/// </summary>
public sealed class ScreenshotWebViewHandler : Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebViewHandler
{
    protected override WebView2 CreatePlatformView()
    {
        var platformView = base.CreatePlatformView();

        if (AppEnvironment.IsScreenshotMode)
        {
            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = $"--remote-debugging-port={AppEnvironment.ScreenshotCdpPort}"
            };
            var environment = CoreWebView2Environment.CreateWithOptionsAsync(null, null, options).GetAwaiter().GetResult();
            _ = platformView.EnsureCoreWebView2Async(environment);
        }

        return platformView;
    }
}
