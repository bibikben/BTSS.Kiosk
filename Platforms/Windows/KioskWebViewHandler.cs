#if WINDOWS
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;

namespace BTSS.IAR.Kiosk.Platforms.Windows;

public class KioskWebViewHandler : WebViewHandler
{
    protected override WebView2 CreatePlatformView()
    {
        var webView2 = new WebView2();
        _ = webView2.EnsureCoreWebView2Async(); // init early; uses WEBVIEW2_USER_DATA_FOLDER
        return webView2;
    }
}
#endif