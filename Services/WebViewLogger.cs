namespace Physiquinator;

internal static class WebViewLogger
{
    public static void AttachConsoleLogging(object? webView)
    {
#if ANDROID
        if (webView is Android.Webkit.WebView androidWebView)
        {
            try
            {
                androidWebView.SetWebChromeClient(new LoggingWebChromeClient());
            }
            catch (Exception ex) when (ex is not System.Threading.ThreadAbortException)
            {
                System.Diagnostics.Debug.WriteLine($"WebViewLogger: Failed to attach chrome client: {ex}");
            }
        }
#endif
    }

#if ANDROID
    private sealed class LoggingWebChromeClient : Android.Webkit.WebChromeClient
    {
        public override bool OnConsoleMessage(Android.Webkit.ConsoleMessage? consoleMessage)
        {
            if (consoleMessage != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[JS] {consoleMessage.Message()} ({consoleMessage.SourceId()}:{consoleMessage.LineNumber()})");
            }
            return base.OnConsoleMessage(consoleMessage);
        }
    }
#endif
}