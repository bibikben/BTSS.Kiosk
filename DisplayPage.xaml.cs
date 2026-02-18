
namespace BTSS.IAR.Kiosk;
#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
#endif
public partial class DisplayPage : ContentPage
{
    private readonly TimeSpan _reloadEvery = TimeSpan.FromMinutes(30);
    private CancellationTokenSource? _watchdogCts;
    private DateTimeOffset _lastSuccessfulNavUtc = DateTimeOffset.MinValue;

    public DisplayPage()
    {
        InitializeComponent();

        KioskWebView.Navigated += (_, e) =>
        {
            if (e.Result == WebNavigationResult.Success)
                _lastSuccessfulNavUtc = DateTimeOffset.UtcNow;
        };
    }

    public void StartWatchdog()
    {
        _watchdogCts?.Cancel();
        _watchdogCts = new CancellationTokenSource();
        _ = RunWatchdogAsync(_watchdogCts.Token);
    }

    public void StopWatchdog()
    {
        _watchdogCts?.Cancel();
        _watchdogCts = null;
    }

#if WINDOWS
private async Task<(bool ok, CoreWebView2WebErrorStatus? err)> NavigateWebView2Async(string url, int timeoutMs = 20000)
{
    url = NormalizeUrl(url);

    await EnsureWebViewReadyAsync(TimeSpan.FromSeconds(10));

    if (KioskWebView.Handler?.PlatformView is not WebView2 wv2)
        return (false, null);

    // Ensure Core exists
    if (wv2.CoreWebView2 is null)
        await wv2.EnsureCoreWebView2Async();

    var tcs = new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    void Completed(object? s, CoreWebView2NavigationCompletedEventArgs e)
    {
        wv2.CoreWebView2.NavigationCompleted -= Completed;
        tcs.TrySetResult(e);
    }

    wv2.CoreWebView2.NavigationCompleted += Completed;

    // Helpful diagnostics
    System.Diagnostics.Debug.WriteLine($"[WV2 NAV] {url}");

    // Navigate using native API
    wv2.CoreWebView2.Navigate(url);

    var done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
    if (done != tcs.Task)
    {
        // timeout
        wv2.CoreWebView2.NavigationCompleted -= Completed;
        System.Diagnostics.Debug.WriteLine($"[WV2 TIMEOUT] {url}");
        return (false, null);
    }

    var result = await tcs.Task;

    System.Diagnostics.Debug.WriteLine($"[WV2 DONE] Success={result.IsSuccess} Error={result.WebErrorStatus}");

    if (result.IsSuccess) return (true, null);
    return (false, result.WebErrorStatus);
}
#endif

    public async Task NavigateAndLoginIfNeededAsync(string url, string agency, string username, string password)
    {
#if WINDOWS
    var (ok, err) = await NavigateWebView2Async(url, timeoutMs: 25000);

    if (!ok)
    {
        // Retry once (first navigation after init sometimes fails)
        await Task.Delay(1200);
        (ok, err) = await NavigateWebView2Async(url, timeoutMs: 25000);
    }

    if (!ok)
    {
        System.Diagnostics.Debug.WriteLine($"[WV2 NAV FAILED] err={err}");
        return; // watchdog will keep trying reloads
    }
#else
        // Non-Windows path if you ever add later
        KioskWebView.Source = url;
#endif

        // Now proceed with your login detection + submit JS
        var hasLogin = await EvalBoolAsync(@"
        (function(){
          const a = document.querySelector('input[name=""Input.Agency""]');
          const u = document.querySelector('input[name=""Input.Username""]');
          const p = document.querySelector('input[name=""Input.Password""]');
          const b = document.querySelector('button[name=""Input.button""][value=""login""]');
          return !!(a && u && p && b);
        })();");

        if (!hasLogin) return;

        var loginJs = $@"
        (function() {{
          const a = document.querySelector('input[name=""Input.Agency""]');
          const u = document.querySelector('input[name=""Input.Username""]');
          const p = document.querySelector('input[name=""Input.Password""]');
          const b = document.querySelector('button[name=""Input.button""][value=""login""]');
          if(a) a.value = {ToJsString(agency)};
          if(u) u.value = {ToJsString(username)};
          if(p) p.value = {ToJsString(password)};
          if(b) b.click();
        }})();";

        await KioskWebView.EvaluateJavaScriptAsync(loginJs);
    }

    private void AttachWebView2Diagnostics()
    {
#if WINDOWS
    if (KioskWebView.Handler?.PlatformView is WebView2 wv2 && wv2.CoreWebView2 != null)
    {
        wv2.CoreWebView2.NavigationStarting += (_, e) =>
            System.Diagnostics.Debug.WriteLine($"[WV2 Starting] {e.Uri}");

        wv2.CoreWebView2.NavigationCompleted += (_, e) =>
            System.Diagnostics.Debug.WriteLine($"[WV2 Completed] Success={e.IsSuccess} Code={e.WebErrorStatus}");
    }
#endif
    }

    public async Task ReloadAsync()
    {
        if (KioskWebView.Source is UrlWebViewSource u && !string.IsNullOrWhiteSpace(u.Url))
            await NavigateAsync(u.Url);
    }

    private async Task<WebNavigationResult> NavigateAsync(string url, int timeoutMs = 15000)
    {
        url = NormalizeUrl(url);

#if WINDOWS
    await EnsureWebViewReadyAsync(TimeSpan.FromSeconds(10));
#endif

        var tcs = new TaskCompletionSource<WebNavigatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? s, WebNavigatedEventArgs e)
        {
            // Some platforms can raise multiple times; take the first.
            KioskWebView.Navigated -= Handler;
            tcs.TrySetResult(e);
        }

        KioskWebView.Navigated += Handler;

        try
        {
            KioskWebView.Source = url;

            // Hard timeout so we never hang forever
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            if (completed != tcs.Task)
            {
                KioskWebView.Navigated -= Handler;
                System.Diagnostics.Debug.WriteLine($"[NAV TIMEOUT] {url}");
                return WebNavigationResult.Timeout;
            }

            var nav = await tcs.Task;

            System.Diagnostics.Debug.WriteLine($"[NAV DONE] {url} => {nav.Result}");
            return nav.Result;
        }
        catch (Exception ex)
        {
            KioskWebView.Navigated -= Handler;
            System.Diagnostics.Debug.WriteLine($"[NAV EX] {url} => {ex}");
            return WebNavigationResult.Failure;
        }
    }
    private static string NormalizeUrl(string url)
    {
        url = (url ?? "").Trim();
        if (string.IsNullOrWhiteSpace(url)) return "about:blank";
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        return url;
    }

#if WINDOWS
private async Task EnsureWebViewReadyAsync(TimeSpan timeout)
{
    var start = DateTime.UtcNow;

    while (KioskWebView.Handler?.PlatformView is not WebView2 &&
           DateTime.UtcNow - start < timeout)
        await Task.Delay(50);

    if (KioskWebView.Handler?.PlatformView is not WebView2 wv2)
        return;

    try
    {
        if (wv2.CoreWebView2 is null)
            await wv2.EnsureCoreWebView2Async();
    }
    catch { /* handled by timeouts */ }
}
#endif

    private async Task RunWatchdogAsync(CancellationToken ct)
    {
        var periodic = new PeriodicTimer(TimeSpan.FromSeconds(10));
        var lastPeriodicReloadUtc = DateTimeOffset.UtcNow;

        try
        {
            while (await periodic.WaitForNextTickAsync(ct))
            {
                // periodic refresh
                if (DateTimeOffset.UtcNow - lastPeriodicReloadUtc > _reloadEvery)
                {
                    await MainThread.InvokeOnMainThreadAsync(ReloadAsync);
                    lastPeriodicReloadUtc = DateTimeOffset.UtcNow;
                }

                // nav stalled/no success in 5 minutes => reload
                if (_lastSuccessfulNavUtc != DateTimeOffset.MinValue &&
                    DateTimeOffset.UtcNow - _lastSuccessfulNavUtc > TimeSpan.FromMinutes(5))
                {
                    await MainThread.InvokeOnMainThreadAsync(ReloadAsync);
                    _lastSuccessfulNavUtc = DateTimeOffset.UtcNow;
                }

                // "page alive" check
                var alive = await EvalBoolAsync(@"
                    (function(){
                      try { return document && document.readyState && document.readyState !== 'loading'; }
                      catch(e){ return false; }
                    })();");

                if (!alive)
                    await MainThread.InvokeOnMainThreadAsync(ReloadAsync);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            periodic.Dispose();
        }
    }

    private async Task<bool> EvalBoolAsync(string js)
    {
        var raw = await KioskWebView.EvaluateJavaScriptAsync(js);
        return string.Equals(raw?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    static string ToJsString(string value) =>
        "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

}
