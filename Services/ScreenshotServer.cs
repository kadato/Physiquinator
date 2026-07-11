#if WINDOWS
using Microsoft.UI.Xaml.Controls;

namespace Physiquinator.Services;

public sealed class ScreenshotServer
{
    private WebView2? _webView;

    public void SetWebView(WebView2 webView)
    {
        _webView = webView;
    }

    public void Start()
    {
        if (_webView is null) return;

        System.Diagnostics.Debug.WriteLine("ScreenshotServer ready, WebView2 attached.");
    }
}
#endif
