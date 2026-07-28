using System.Collections.ObjectModel;
using AlphaBrowser.Models;
using AlphaBrowser.Services;

namespace AlphaBrowser.Pages;

public partial class MainPage : ContentPage
{
    private readonly StorageService _store;
    private readonly List<BrowserTab> _tabs = new();
    private BrowserTab? _currentTab;
    private bool _isIncognito;
    private IDispatcherTimer? _clockTimer;
    private IDispatcherTimer? _weatherTimer;

    public MainPage()
    {
        InitializeComponent();
        _store = new StorageService();
        ThemeService.ThemeChanged += ApplyTheme;
        InitStartPage();
        AddNewTab();
    }

    // ── Theme ────────────────────────────────────────────────
    private void ApplyTheme()
    {
        var t = ThemeService.Current;
        var bg = Color.FromArgb(t.BgDark);
        var panel = Color.FromArgb(t.BgPanel);
        var elev = Color.FromArgb(t.BgElevated);
        var accent = Color.FromArgb(t.Accent);
        var text = t.IsLight ? Colors.Black : Colors.White;
        var muted = t.IsLight ? "#888888" : "#9A9AA6";

        Root.BackgroundColor = bg;
        Toolbar.BackgroundColor = panel;
        AddressBar.BackgroundColor = elev;
        AddressBar.TextColor = text;
        AddressBar.PlaceholderColor = Color.FromArgb(muted);
        BackBtn.TextColor = text;
        FwdBtn.TextColor = text;
        ReloadBtn.TextColor = text;
        MenuBtn.TextColor = text;
        ClockLabel.TextColor = text;
        DateLabel.TextColor = text;
        WeatherIcon.TextColor = text;
        WeatherTemp.TextColor = text;
        WeatherCity.TextColor = text;
        SidePanel.BackgroundColor = panel;
    }

    // ── Start Page ───────────────────────────────────────────
    private void InitStartPage()
    {
        ApplyTheme();
        UpdateClock();
        _clockTimer = Dispatcher.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();

        UpdateWeather();
        _weatherTimer = Dispatcher.CreateTimer();
        _weatherTimer.Interval = TimeSpan.FromMinutes(30);
        _weatherTimer.Tick += async (s, e) => await FetchWeatherAsync();
        _weatherTimer.Start();

        LoadPinnedSites();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockLabel.Text = now.ToString("h:mm tt");
        DateLabel.Text = now.ToString("dddd, MMMM d");
    }

    private async Task FetchWeatherAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var json = await http.GetStringAsync("https://wttr.in/?format=%t|%C|%n");
            var parts = json.Split('|');
            if (parts.Length >= 2)
            {
                var isNight = parts.Length >= 3 && parts[2] == "1";
                WeatherTemp.Text = parts[0];
                WeatherIcon.Text = parts[1].ToLower() switch
                {
                    "clear" or "sunny" => isNight ? "🌙" : "☀️",
                    "partly cloudy" => "⛅",
                    "cloudy" or "overcast" => "☁️",
                    "rain" or "light rain" or "moderate rain" => "🌧️",
                    "thunderstorm" or "heavy rain" => "⛈️",
                    "snow" or "light snow" => "❄️",
                    "fog" or "mist" or "haze" => "🌫️",
                    _ => "🌤️"
                };
            }
        }
        catch { }
    }

    private async void UpdateWeather()
    {
        try { WeatherCity.Text = await new HttpClient().GetStringAsync("https://ipinfo.io/city"); } catch { }
        _ = FetchWeatherAsync();
    }

    private void LoadPinnedSites()
    {
        if (_store.Settings.PinnedSites.Count > 0)
            PinnedGrid.ItemsSource = _store.Settings.PinnedSites;
    }

    private void OnStartPageTap(object? s, TappedEventArgs e)
    {
        // Focus search
        StartSearch.Focus();
    }

    private async void OnStartSearchGo(object? s, EventArgs e)
    {
        var text = StartSearch.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        StartSearch.Text = "";
        Navigate(ResolveQuery(text));
        ShowWebView();
    }

    // ── Navigation ───────────────────────────────────────────
    private string ResolveQuery(string text)
    {
        if (text.Contains(".") && !text.Contains(" "))
            return text.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? text : "https://" + text;
        return $"https://www.google.com/search?q={Uri.EscapeDataString(text)}";
    }

    private void Navigate(string url, BrowserTab? tab = null)
    {
        tab ??= _currentTab;
        if (tab == null || string.IsNullOrEmpty(url)) return;
        tab.Url = url;
        tab.WebView.Source = new UrlWebViewSource { Url = url };
        AddressBar.Text = url;
    }

    private void ShowWebView()
    {
        StartPage.IsVisible = false;
        WebViewArea.IsVisible = true;
    }

    private void ShowStartPage()
    {
        StartPage.IsVisible = true;
        WebViewArea.IsVisible = false;
        AddressBar.Text = "";
    }

    private void OnAddressGo(object? s, EventArgs e)
    {
        var text = AddressBar.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            ShowWebView();
            Navigate(ResolveQuery(text));
        }
    }

    private void OnBack(object? s, EventArgs e)
    {
        if (_currentTab?.WebView?.CanGoBack == true) _currentTab.WebView.GoBack();
    }

    private void OnForward(object? s, EventArgs e)
    {
        if (_currentTab?.WebView?.CanGoForward == true) _currentTab.WebView.GoForward();
    }

    private void OnReload(object? s, EventArgs e)
    {
        if (_currentTab?.WebView != null)
        {
            if (_currentTab.Url == "about:blank" || string.IsNullOrEmpty(_currentTab.Url))
                ShowStartPage();
            else _currentTab.WebView.Reload();
        }
    }

    // ── Tab Management ───────────────────────────────────────
    private BrowserTab AddNewTab(string? url = null)
    {
        var wv = new WebView { IsVisible = false };
        wv.Navigated += OnTabNavigated;
        wv.Navigating += OnTabNavigating;

#if ANDROID
        wv.HandlerChanged += (s, e) =>
        {
            if (wv.Handler?.PlatformView is Android.Webkit.WebView nwv)
            {
                nwv.Settings.JavaScriptEnabled = true;
                nwv.Settings.DomStorageEnabled = true;
                nwv.Settings.MixedContentMode = Android.Webkit.MixedContentHandling.AlwaysAllow;
                nwv.Settings.BuiltInZoomControls = true;
                nwv.Settings.DisplayZoomControls = false;
                nwv.Settings.LoadWithOverviewMode = true;
                nwv.Settings.UseWideViewPort = true;
            }
        };
#endif

        var tab = new BrowserTab { Title = "New Tab", Url = url ?? "about:blank", WebView = wv };
        _tabs.Add(tab);
        WebViewArea.Children.Add(wv);

        if (!string.IsNullOrEmpty(url) && url != "about:blank")
        {
            wv.Source = new UrlWebViewSource { Url = url };
            ShowWebView();
        }

        SelectTab(tab);
        return tab;
    }

    private void SelectTab(BrowserTab tab)
    {
        if (_currentTab != null) { _currentTab.IsSelected = false; _currentTab.WebView.IsVisible = false; }
        _currentTab = tab;
        _currentTab.IsSelected = true;
        _currentTab.WebView.IsVisible = true;
        AddressBar.Text = _currentTab.Url;

        if (_currentTab.Url == "about:blank" || string.IsNullOrEmpty(_currentTab.Url))
            ShowStartPage();

        UpdateTabStrip();
        UpdateNavButtons();
    }

    private void CloseTab(BrowserTab tab)
    {
        if (_tabs.Count <= 1) return;
        var idx = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        WebViewArea.Children.Remove(tab.WebView);
        if (_currentTab == tab)
            SelectTab(_tabs[Math.Min(idx, _tabs.Count - 1)]);
        UpdateTabStrip();
    }

    private void UpdateTabStrip()
    {
        TabStrip.Children.Clear();
        foreach (var tab in _tabs)
        {
            var text = tab.IsPinned ? "📌" : "";
            text += tab.Title.Length > 10 ? tab.Title[..10] + "…" : tab.Title;

            var btn = new Button
            {
                Text = text, FontSize = 11, HeightRequest = 28, CornerRadius = 6,
                Padding = new Thickness(8, 0),
                BackgroundColor = tab == _currentTab
                    ? Color.FromArgb(ThemeService.Current.IsLight ? "#E0E0E0" : "#26262C")
                    : Colors.Transparent,
                TextColor = Color.FromArgb(ThemeService.Current.IsLight ? "#000000" : "#EDEDF2")
            };

            var captured = tab;
            btn.Clicked += (s, e) => SelectTab(captured);

            var frame = new Border
            {
                Content = btn,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                Stroke = Color.FromArgb("#34343C"),
                StrokeThickness = tab == _currentTab ? 0 : 1,
                Margin = new Thickness(1, 0)
            };
            TabStrip.Children.Add(frame);
        }

        // + button
        var addBtn = new Button { Text = "+", FontSize = 16, WidthRequest = 28, HeightRequest = 28,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb(ThemeService.Current.IsLight ? "#000000" : "#EDEDF2") };
        addBtn.Clicked += (s, e) => AddNewTab();
        TabStrip.Children.Add(addBtn);
    }

    private void UpdateNavButtons()
    {
        BackBtn.Opacity = _currentTab?.WebView?.CanGoBack == true ? 1.0 : 0.3;
        FwdBtn.Opacity = _currentTab?.WebView?.CanGoForward == true ? 1.0 : 0.3;
        BackBtn.IsEnabled = _currentTab?.WebView?.CanGoBack == true;
        FwdBtn.IsEnabled = _currentTab?.WebView?.CanGoForward == true;
    }

    private void OnTabNavigating(object? s, WebNavigatingEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AddressBar.Text = e.Url;
            if (_currentTab != null) _currentTab.Url = e.Url;
        });
    }

    private void OnTabNavigated(object? s, WebNavigatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_currentTab == null || s is not WebView wv) return;

            _currentTab.Url = e.Url;
            _currentTab.IsLoading = false;

            try
            {
                var title = await wv.EvaluateJavaScriptAsync("document.title");
                if (!string.IsNullOrEmpty(title)) _currentTab.Title = title;
            }
            catch { }

            AddressBar.Text = e.Url;
            UpdateTabStrip();
            UpdateNavButtons();

            if (!string.IsNullOrEmpty(e.Url) && !e.Url.StartsWith("about:") && !e.Url.StartsWith("file://"))
            {
                _store.AddHistory(_currentTab.Title, e.Url);

                if (_store.Settings.AdBlockEnabled)
                {
                    try { await wv.EvaluateJavaScriptAsync(AdBlockService.InjectCssScript()); }
                    catch { }
                }
            }
        });
    }

    // ── Menu ─────────────────────────────────────────────────
    private async void OnMenu(object? s, EventArgs e)
    {
        var actions = new List<string>
        {
            "New Tab", "Bookmark This Page", "Share Page",
            _isIncognito ? "Disable Incognito" : "Incognito Mode",
            "Bookmarks", "History", "Downloads", "Passwords",
            "Pinned Sites", "Profiles", "Settings", "About"
        };

        var choice = await DisplayActionSheet("Alpha Browser", "Close", null, actions.ToArray());
        if (choice == null || choice == "Close") return;

        switch (choice)
        {
            case "New Tab": AddNewTab(); break;
            case "Bookmark This Page":
                if (_currentTab != null && !string.IsNullOrEmpty(_currentTab.Url) && _currentTab.Url != "about:blank")
                {
                    _store.AddBookmark(_currentTab.Title, _currentTab.Url);
                    await DisplayAlert("Bookmark", "Page bookmarked!", "OK");
                }
                else await DisplayAlert("Bookmark", "No page to bookmark.", "OK");
                break;

            case "Share Page":
                if (_currentTab != null && !string.IsNullOrEmpty(_currentTab.Url))
                    await Share.Default.RequestAsync(new ShareTextRequest
                    { Uri = _currentTab.Url, Title = _currentTab.Title });
                break;

            case "Incognito Mode":
                _isIncognito = true;
                AddNewTab("about:blank");
                break;
            case "Disable Incognito":
                _isIncognito = false;
                break;

            case "Bookmarks": await ShowBookmarks(); break;
            case "History": await ShowHistory(); break;
            case "Downloads": await ShowDownloads(); break;
            case "Passwords": await ShowPasswords(); break;
            case "Pinned Sites": await ShowPinnedManager(); break;
            case "Profiles": await ShowProfiles(); break;
            case "Settings": await ShowSettings(); break;
            case "About":
                await DisplayAlert("Alpha Browser",
                    $"Alpha Browser v1.0\n\nBuilt from your 2-year WPF project\nPorted to Android via .NET MAUI\n\n© mynewbuisnuse-commits",
                    "OK");
                break;
        }
    }

    // ── Bookmarks ────────────────────────────────────────────
    private async Task ShowBookmarks()
    {
        var bm = _store.Settings.Bookmarks;
        if (bm.Count == 0) { await DisplayAlert("Bookmarks", "No bookmarks yet.", "OK"); return; }

        var items = bm.Select(b => $"{b.Title} — {ShortUrl(b.Url)}").ToArray();
        var sel = await DisplayActionSheet("Bookmarks (Delete: long-press)", "Close", null, items);
        if (sel == null || sel == "Close") return;
        var match = bm.FirstOrDefault(b => $"{b.Title} — {ShortUrl(b.Url)}" == sel);
        if (match != null) { ShowWebView(); Navigate(match.Url); }
    }

    // ── History ──────────────────────────────────────────────
    private async Task ShowHistory()
    {
        var h = _store.Settings.History;
        if (h.Count == 0) { await DisplayAlert("History", "No history.", "OK"); return; }

        var items = h.Select(e => e.Title.Length > 40 ? e.Title[..40] + "…" : e.Title).ToArray();
        var sel = await DisplayActionSheet("History (Clear = delete all)", "Close", "Clear All", items);
        if (sel == "Clear All") { _store.Settings.History.Clear(); _store.Save(); }
        else if (sel != null && sel != "Close")
        {
            var match = h.FirstOrDefault(e => (e.Title.Length > 40 ? e.Title[..40] + "…" : e.Title) == sel);
            if (match != null) { ShowWebView(); Navigate(match.Url); }
        }
    }

    // ── Downloads ────────────────────────────────────────────
    private async Task ShowDownloads()
    {
        var d = _store.Settings.Downloads;
        if (d.Count == 0) { await DisplayAlert("Downloads", "No downloads.", "OK"); return; }
        var items = d.Select(x => $"{x.FileName} ({x.ProgressText})").ToArray();
        await DisplayActionSheet("Downloads", "Close", null, items);
    }

    // ── Passwords ────────────────────────────────────────────
    private async Task ShowPasswords()
    {
        var pw = _store.Settings.SavedPasswords;
        if (pw.Count == 0) { await DisplayAlert("Passwords", "No saved passwords.", "OK"); return; }
        var items = pw.Select(p => $"{p.Site}: {p.Username}").ToArray();
        await DisplayActionSheet("Passwords", "Close", null, items);
    }

    // ── Pinned Sites ─────────────────────────────────────────
    private async Task ShowPinnedManager()
    {
        var ps = _store.Settings.PinnedSites;
        if (_currentTab != null && !string.IsNullOrEmpty(_currentTab.Url) && _currentTab.Url != "about:blank")
        {
            var add = await DisplayAlert("Pinned Sites", $"Pin \"{_currentTab.Title}\"?", "Pin It", "View List");
            if (add)
            {
                _store.Settings.PinnedSites.Add(new PinnedSite
                { Title = _currentTab.Title, Url = _currentTab.Url });
                _store.Save();
                LoadPinnedSites();
                await DisplayAlert("Pinned", "Site pinned!", "OK");
                return;
            }
        }
        if (ps.Count == 0) { await DisplayAlert("Pinned Sites", "No pinned sites.", "OK"); return; }
        var items = ps.Select(p => p.Title).Append("[Clear All]").ToArray();
        var sel = await DisplayActionSheet("Pinned Sites", "Close", null, items);
        if (sel == "[Clear All]") { _store.Settings.PinnedSites.Clear(); _store.Save(); LoadPinnedSites(); }
        else if (sel != null && sel != "Close")
        {
            var match = ps.FirstOrDefault(p => p.Title == sel);
            if (match != null) { ShowWebView(); Navigate(match.Url); }
        }
    }

    // ── Profiles ─────────────────────────────────────────────
    private async Task ShowProfiles()
    {
        var profiles = _store.Settings.Profiles;
        if (profiles.Count == 0)
            profiles.Add(new UserProfile { Name = "Default" });

        var items = profiles.Select(p => $"{p.AvatarEmoji} {p.Name}").Append("[New Profile]").ToArray();
        var sel = await DisplayActionSheet("Profiles", "Close", null, items);
        if (sel == "[New Profile]")
        {
            var name = await DisplayPromptAsync("New Profile", "Profile name:", initialValue: "New Profile");
            if (!string.IsNullOrEmpty(name))
            {
                _store.Settings.Profiles.Add(new UserProfile { Name = name });
                _store.Save();
            }
        }
        else if (sel != null && sel != "Close")
        {
            var match = profiles.FirstOrDefault(p => $"{p.AvatarEmoji} {p.Name}" == sel);
            if (match != null)
            {
                _store.Settings.ActiveProfileId = match.Id;
                _store.Settings.HomeUrl = match.HomeUrl;
                _store.Settings.SearchEngine = match.SearchEngine;
                _store.Settings.AdBlockEnabled = match.AdBlockEnabled;
                _store.Save();
                ThemeService.Apply(match.Theme);
                await DisplayAlert("Profile", $"Switched to {match.Name}", "OK");
            }
        }
    }

    // ── Settings ─────────────────────────────────────────────
    private async Task ShowSettings()
    {
        var s = _store.Settings;
        var themeNames = ThemeService.Themes.Select(t => t.Name).ToArray();
        var items = new List<string>
        {
            $"Home: {ShortUrl(s.HomeUrl)}",
            $"Search: {s.SearchEngine}",
            $"Theme: {s.Theme}",
            $"AdBlock: {(s.AdBlockEnabled ? "ON" : "OFF")}",
            $"Restore Tabs: {(s.RestoreTabsOnStartup ? "ON" : "OFF")}",
            "Clear Cache",
            "Clear History",
            "Reset Browser"
        };

        var sel = await DisplayActionSheet("Settings", "Close", null, items.ToArray());
        if (sel == null || sel == "Close") return;

        if (sel.StartsWith("Home:"))
        {
            var url = await DisplayPromptAsync("Home Page", "URL:", initialValue: s.HomeUrl);
            if (!string.IsNullOrEmpty(url)) { s.HomeUrl = url; _store.Save(); }
        }
        else if (sel.StartsWith("Search:"))
        {
            var eng = await DisplayActionSheet("Search Engine", "Cancel", null, "Google", "Bing", "DuckDuckGo");
            if (eng != null && eng != "Cancel") { s.SearchEngine = eng; _store.Save(); }
        }
        else if (sel.StartsWith("Theme:"))
        {
            var th = await DisplayActionSheet("Theme", "Cancel", null, themeNames);
            if (th != null && th != "Cancel") { s.Theme = th; ThemeService.Apply(th); _store.Save(); }
        }
        else if (sel.StartsWith("AdBlock:")) { s.AdBlockEnabled = !s.AdBlockEnabled; _store.Save(); }
        else if (sel.StartsWith("Restore Tabs:")) { s.RestoreTabsOnStartup = !s.RestoreTabsOnStartup; _store.Save(); }
        else if (sel == "Clear Cache")
        {
            try
            {
                if (_currentTab?.WebView?.Handler?.PlatformView is Android.Webkit.WebView nwv)
                    nwv.ClearCache(true);
            }
            catch { }
            await DisplayAlert("Cache", "Cleared!", "OK");
        }
        else if (sel == "Clear History") { s.History.Clear(); _store.Save(); }
        else if (sel == "Reset Browser")
        {
            if (await DisplayAlert("Reset", "Delete all data?", "Yes", "No"))
            {
                _store.Reset();
                await DisplayAlert("Reset", "Browser reset. Restart app.", "OK");
            }
        }

        await ShowSettings(); // refresh
    }

    // ── Helpers ──────────────────────────────────────────────
    private static string ShortUrl(string url)
    {
        try { var u = new Uri(url); return u.Host.TrimStart('w', '.') + u.AbsolutePath; }
        catch { return url; }
    }
}
