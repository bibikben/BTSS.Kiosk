
using Microsoft.UI.Xaml;

namespace BTSS.IAR.Kiosk;
#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

using Microsoft.UI.Xaml.Input;
using Windows.System;

#endif
public partial class DisplayPage : ContentPage
{
    private readonly TimeSpan _reloadEvery = TimeSpan.FromMinutes(30);
    private CancellationTokenSource? _watchdogCts;
    private DateTime _lastSuccessfulNavUtc = DateTime.MinValue;

#if WINDOWS
    private KeyboardAccelerator? _exitHotkey;
    private UIElement? _hotkeyTarget;
#endif

    
    // Domain lock + relogin state
    private string? _initialUrl;
    private string? _agency;
    private string? _username;
    private string? _password;

    private bool _sawNonInitialUrl;
    private bool _reloginInProgress;
    private DateTime _lastReloginAttemptUtc = DateTime.MinValue;
    private DateTime _lastDomainEnforceUtc = DateTime.MinValue;

    private const string AllowedUrlFragment = "dashboard.iamresponding.com";
public DisplayPage()
    {
        InitializeComponent();

        KioskWebView.Navigated += (_, e) =>
        {
            // fire-and-forget (event handler)
            _ = OnNavigatedAsync(e);
        };
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();


    }

    protected override void OnDisappearing()
    {
#if WINDOWS
        DetachExitHotkeyWindows();
#endif

        base.OnDisappearing();
    }

#if WINDOWS
    private void AttachExitHotkeyWindows()
    {
        // Already attached
        if (_exitHotkey != null)
            return;

        try
        {
            // For multi-window MAUI, this.Window is the DisplayWindow hosting this page.
            var win = this.Window?.Handler?.PlatformView as Microsoft.Maui.MauiWinUIWindow;
            var root = win?.Content as UIElement;
            if (root == null)
                return;

            _hotkeyTarget = root;
            _exitHotkey = new KeyboardAccelerator
            {
                Key = VirtualKey.E,
                Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift
            };
            _exitHotkey.Invoked += ExitHotkey_Invoked;
            root.KeyboardAccelerators.Add(_exitHotkey);
        }
        catch
        {
            // no-op: hotkey is best-effort
        }
    }

    private void DetachExitHotkeyWindows()
    {
        try
        {
            if (_exitHotkey != null)
                _exitHotkey.Invoked -= ExitHotkey_Invoked;

            if (_hotkeyTarget != null && _exitHotkey != null)
                _hotkeyTarget.KeyboardAccelerators.Remove(_exitHotkey);
        }
        catch
        {
            // ignore
        }
        finally
        {
            _exitHotkey = null;
            _hotkeyTarget = null;
        }
    }

    private void ExitHotkey_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        // Exit display window back to admin
        if (Microsoft.Maui.Controls.Application.Current is BTSS.IAR.Kiosk.App app)
        {
            try
            {
                app.StopDisplayWindow();
                app.ShowAdminWindow();
            }
            catch
            {
                // ignore
            }
        }
    }
#endif

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
        // Persist the starting URL + credentials for relogin behavior
        _initialUrl = NormalizeUrl(url);
        _agency = agency;
        _username = username;
        _password = password;

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

    private async Task OnNavigatedAsync(WebNavigatedEventArgs e)
    {
        if (e.Result == WebNavigationResult.Success)
            _lastSuccessfulNavUtc = DateTime.UtcNow;

        var url = (e.Url ?? "").Trim();
        if (string.IsNullOrWhiteSpace(url) || _initialUrl is null)
            return;

        // Track whether we've ever left the starting URL; prevents loops on first load
        var isInitial = IsSameOrStartsWith(url, _initialUrl);
        if (!isInitial)
            _sawNonInitialUrl = true;
#if WINDOWS
        AttachExitHotkeyWindows();
#endif
        // Domain lock: if we leave the allowed domain, bounce back to the starting URL.
        // Throttle so we don't spam navigation events.
        if (!url.Contains(AllowedUrlFragment, StringComparison.OrdinalIgnoreCase) &&
            !isInitial &&
            DateTime.UtcNow - _lastDomainEnforceUtc > TimeSpan.FromSeconds(2))
        {
            _lastDomainEnforceUtc = DateTime.UtcNow;
            System.Diagnostics.Debug.WriteLine($"[DOMAIN LOCK] {url} -> {_initialUrl}");
#if WINDOWS
            await NavigateWebView2Async(_initialUrl, timeoutMs: 25000);
#else
            KioskWebView.Source = _initialUrl;
#endif
            return;
        }

        // If we have previously left the initial URL and we end up back at it,
        // treat this as an auth/session bounce and attempt login again.
        if (_sawNonInitialUrl &&
            isInitial &&
            !_reloginInProgress &&
            DateTime.UtcNow - _lastReloginAttemptUtc > TimeSpan.FromSeconds(15) &&
            !string.IsNullOrWhiteSpace(_agency) &&
            !string.IsNullOrWhiteSpace(_username) &&
            !string.IsNullOrWhiteSpace(_password))
        {
            _lastReloginAttemptUtc = DateTime.UtcNow;
            _reloginInProgress = true;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[RELOGIN] Returned to initial URL: {url} -> wiping session and logging in again");

                await ClearWebSessionAsync();
                await NavigateAndLoginIfNeededAsync(_initialUrl, _agency!, _username!, _password!);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RELOGIN ERROR] {ex}");
            }
            finally
            {
                _reloginInProgress = false;
            }
        }
    }

    private static bool IsSameOrStartsWith(string actualUrl, string expectedRoot)
    {
        if (string.IsNullOrWhiteSpace(actualUrl) || string.IsNullOrWhiteSpace(expectedRoot))
            return false;

        actualUrl = actualUrl.TrimEnd('/');
        expectedRoot = expectedRoot.TrimEnd('/');

        return actualUrl.Equals(expectedRoot, StringComparison.OrdinalIgnoreCase)
               || actualUrl.StartsWith(expectedRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ClearWebSessionAsync()
    {
#if WINDOWS
        await EnsureWebViewReadyAsync(TimeSpan.FromSeconds(10));
        if (KioskWebView.Handler?.PlatformView is WebView2 wv2 && wv2.CoreWebView2 != null)
        {
            try
            {
                // Cookies (fast + explicit)
                wv2.CoreWebView2.CookieManager.DeleteAllCookies();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SESSION WIPE] CookieManager failed: {ex.Message}");
            }

            try
            {
                // Full profile browsing data wipe (cookies/cache/storage/etc.)
                // This is supported by WebView2 Profile API.
                await wv2.CoreWebView2.Profile.ClearBrowsingDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SESSION WIPE] ClearBrowsingDataAsync failed: {ex.Message}");
            }

            try
            {
                // Best-effort in-page storage cleanup (may be blocked cross-origin; that's OK)
                await wv2.ExecuteScriptAsync(@"
                    (async function(){
                        try { localStorage && localStorage.clear && localStorage.clear(); } catch(e){}
                        try { sessionStorage && sessionStorage.clear && sessionStorage.clear(); } catch(e){}
                        try {
                            if (navigator.serviceWorker && navigator.serviceWorker.getRegistrations) {
                                const regs = await navigator.serviceWorker.getRegistrations();
                                for (const r of regs) { try { await r.unregister(); } catch(e){} }
                            }
                        } catch(e){}
                        return true;
                    })();");
            }
            catch { /* ignored */ }
        }
#else
        // Non-Windows: no-op for now
        await Task.CompletedTask;
#endif
    }



    private async Task RunWatchdogAsync(CancellationToken ct)
    {
        var periodic = new PeriodicTimer(TimeSpan.FromSeconds(10));
        var lastPeriodicReloadUtc = DateTime.UtcNow;

        try
        {
            while (await periodic.WaitForNextTickAsync(ct))
            {
                // periodic refresh
                if (DateTime.UtcNow - lastPeriodicReloadUtc > _reloadEvery)
                {
                    await MainThread.InvokeOnMainThreadAsync(ReloadAsync);
                    lastPeriodicReloadUtc = DateTime.UtcNow;
                }

                // nav stalled/no success in 5 minutes => reload
                if (_lastSuccessfulNavUtc != DateTime.MinValue &&
                    DateTime.UtcNow - _lastSuccessfulNavUtc > TimeSpan.FromMinutes(5))
                {
                    await MainThread.InvokeOnMainThreadAsync(ReloadAsync);
                    _lastSuccessfulNavUtc = DateTime.UtcNow;
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
