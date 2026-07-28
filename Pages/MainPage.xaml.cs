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
    private IDispatcherTimer? _clock;

    public MainPage()
    {
        InitializeComponent();
        _store = new StorageService();
        ThemeService.ThemeChanged += ApplyTheme;
        ApplyTheme();
        _clock = Dispatcher.CreateTimer();
        _clock.Interval = TimeSpan.FromSeconds(1);
        _clock.Tick += (s, e) => UpdateClock();
        _clock.Start();
        UpdateClock();
        LoadStartData();
        AddNewTab();
    }

    private void ApplyTheme()
    {
        var t = ThemeService.Current;
        var bg = Color.FromArgb(t.BgDark);
        var panel = Color.FromArgb(t.BgPanel);
        var accent = Color.FromArgb(t.Accent);
        var text = Color.FromArgb(t.IsLight ? "#1A1A1A" : "#EDEDF2");
        var muted = Color.FromArgb(t.IsLight ? "#666666" : "#9A9AA6");

        RootGrid.BackgroundColor = bg;
        TitleBar.BackgroundColor = bg;
        Toolbar.BackgroundColor = bg;
        BackBtn.TextColor = text;
        FwdBtn.TextColor = text;
        ReloadBtn.TextColor = text;
        HomeBtn.TextColor = text;
        MenuBtn.TextColor = text;
        AddressBar.TextColor = text;
        AddressBar.PlaceholderColor = muted;
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockLabel.Text = now.ToString("h:mm tt");
        DateLabel.Text = now.ToString("dddd, MMMM d");
    }

    private async void LoadStartData()
    {
        try
        {
            using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var w = await h.GetStringAsync("https://wttr.in/?format=%t|%C");
            var p = w.Split('|');
            if (p.Length >= 2) { WeatherTemp.Text = p[0]; WeatherIcon.Text = p[1].Contains("clear") ? "☀️" : "🌤️"; }
        }
        catch { }
        if (_store.Settings.PinnedSites.Count > 0)
            PinnedGrid.ItemsSource = _store.Settings.PinnedSites;
    }

    private string Resolve(string q) => q.Contains(".") && !q.Contains(" ") ? (q.StartsWith("http") ? q : "https://" + q) : $"https://www.google.com/search?q={Uri.EscapeDataString(q)}";

    private void Navigate(string url, BrowserTab? tab = null)
    {
        tab ??= _currentTab;
        if (tab == null) return;
        tab.Url = url; tab.WebView.Source = new UrlWebViewSource { Url = url };
        AddressBar.Text = url;
    }

    private void ShowWebView() { StartPage.IsVisible = false; WebViewArea.IsVisible = true; }
    private void ShowStart() { StartPage.IsVisible = true; WebViewArea.IsVisible = false; AddressBar.Text = ""; }

    private BrowserTab AddNewTab(string? url = null)
    {
        var wv = new WebView { IsVisible = false };
        wv.Navigated += OnNav; wv.Navigating += OnNavStart;
#if ANDROID
        wv.HandlerChanged += (s, e) => {
            if (wv.Handler?.PlatformView is Android.Webkit.WebView n) {
                n.Settings.JavaScriptEnabled = true; n.Settings.DomStorageEnabled = true;
                n.Settings.MixedContentMode = Android.Webkit.MixedContentHandling.AlwaysAllow;
                n.Settings.BuiltInZoomControls = true; n.Settings.DisplayZoomControls = false;
                n.Settings.LoadWithOverviewMode = true; n.Settings.UseWideViewPort = true;
            }
        };
#endif
        var tab = new BrowserTab { Title = "New Tab", Url = url ?? "about:blank", WebView = wv };
        _tabs.Add(tab); WebViewArea.Children.Add(wv);
        if (!string.IsNullOrEmpty(url) && url != "about:blank") { wv.Source = new UrlWebViewSource { Url = url }; ShowWebView(); }
        SelectTab(tab);
        return tab;
    }

    private void SelectTab(BrowserTab tab)
    {
        if (_currentTab != null) { _currentTab.IsSelected = false; _currentTab.WebView.IsVisible = false; }
        _currentTab = tab; _currentTab.IsSelected = true; _currentTab.WebView.IsVisible = true;
        AddressBar.Text = _currentTab.Url;
        if (_currentTab.Url == "about:blank" || string.IsNullOrEmpty(_currentTab.Url)) ShowStart(); else ShowWebView();
        RenderTabs(); UpdateNav();
    }

    private void CloseTab(BrowserTab tab)
    {
        if (_tabs.Count <= 1) return;
        var i = _tabs.IndexOf(tab); _tabs.Remove(tab); WebViewArea.Children.Remove(tab.WebView);
        if (_currentTab == tab) SelectTab(_tabs[Math.Min(i, _tabs.Count - 1)]);
        RenderTabs();
    }

    private void RenderTabs()
    {
        TabStrip.Children.Clear();
        foreach (var t in _tabs)
        {
            var isSel = t == _currentTab;
            var lbl = new Label
            {
                Text = (t.IsPinned ? "📌" : "") + (t.Title.Length > 10 ? t.Title[..10] + "…" : t.Title),
                FontSize = 11, VerticalOptions = LayoutOptions.Center,
                TextColor = Color.FromArgb("#EDEDF2")
            };
            var selBar = new BoxView { Color = Color.FromArgb("#7C5CFF"), HeightRequest = 2, IsVisible = isSel, VerticalOptions = LayoutOptions.End };
            var c = isSel ? Color.FromArgb("#26262C") : Colors.Transparent;
            var b = new Border { Content = new Grid { Children = { lbl, selBar } }, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6, }, Stroke = Color.FromArgb("#34343C"), StrokeThickness = isSel ? 0 : 1, BackgroundColor = c, Padding = new Thickness(10, 2), Margin = new Thickness(1, 0) };
            var captured = t;
            var tap = new TapGestureRecognizer(); tap.Tapped += (s, e) => SelectTab(captured); b.GestureRecognizers.Add(tap);
            TabStrip.Children.Add(b);
        }
        var add = new Label { Text = "+", FontSize = 16, TextColor = Color.FromArgb("#EDEDF2"), VerticalOptions = LayoutOptions.Center, Margin = new Thickness(4, 0) };
        var addTap = new TapGestureRecognizer(); addTap.Tapped += (s, e) => AddNewTab(); add.GestureRecognizers.Add(addTap);
        TabStrip.Children.Add(add);
    }

    private void UpdateNav() { BackBtn.Opacity = _currentTab?.WebView?.CanGoBack == true ? 1 : 0.35; FwdBtn.Opacity = _currentTab?.WebView?.CanGoForward == true ? 1 : 0.35; }

    private void OnNavStart(object? s, WebNavigatingEventArgs e) => MainThread.BeginInvokeOnMainThread(() => { if (_currentTab != null) _currentTab.Url = e.Url; AddressBar.Text = e.Url; });
    private void OnNav(object? s, WebNavigatedEventArgs e) => MainThread.BeginInvokeOnMainThread(async () =>
    {
        if (_currentTab == null || s is not WebView w) return;
        _currentTab.Url = e.Url;
        try { var t = await w.EvaluateJavaScriptAsync("document.title"); if (!string.IsNullOrEmpty(t)) _currentTab.Title = t; } catch { }
        AddressBar.Text = e.Url; RenderTabs(); UpdateNav();
        if (!string.IsNullOrEmpty(e.Url) && !e.Url.StartsWith("about:") && !_isIncognito) _store.AddHistory(_currentTab.Title, e.Url);
        if (_store.Settings.AdBlockEnabled) try { await w.EvaluateJavaScriptAsync(AdBlockService.InjectCssScript()); } catch { }
    });

    private void OnAddressGo(object? s, EventArgs e) { var t = AddressBar.Text?.Trim(); if (!string.IsNullOrEmpty(t)) { ShowWebView(); Navigate(Resolve(t)); } }
    private void OnStartSearchGo(object? s, EventArgs e) { var t = StartSearch.Text?.Trim(); if (!string.IsNullOrEmpty(t)) { StartSearch.Text = ""; ShowWebView(); Navigate(Resolve(t)); } }
    private void OnBack(object? s, EventArgs e) { if (_currentTab?.WebView?.CanGoBack == true) _currentTab.WebView.GoBack(); }
    private void OnForward(object? s, EventArgs e) { if (_currentTab?.WebView?.CanGoForward == true) _currentTab.WebView.GoForward(); }
    private void OnReload(object? s, EventArgs e) { if (_currentTab?.WebView != null && _currentTab.Url != "about:blank") _currentTab.WebView.Reload(); else ShowStart(); }
    private void OnHome(object? s, EventArgs e) { ShowStart(); }

    private async void OnMenu(object? s, EventArgs e)
    {
        var a = await DisplayActionSheet("Alpha Browser", "Close", null,
            "New Tab", "Bookmark This Page", "Share",
            _isIncognito ? "Incognito: ON" : "Incognito: OFF",
            "Bookmarks", "History", "Downloads", "Passwords",
            "Pinned Sites", "Profiles", "Themes", "Settings", "About");
        if (a == null || a == "Close") return;
        switch (a)
        {
            case "New Tab": AddNewTab(); break;
            case "Bookmark This Page":
                if (_currentTab != null && !string.IsNullOrEmpty(_currentTab.Url) && _currentTab.Url != "about:blank") { _store.AddBookmark(_currentTab.Title, _currentTab.Url); await DisplayAlert("", "Bookmarked!", "OK"); }
                else await DisplayAlert("", "No page to bookmark.", "OK"); break;
            case "Share":
                if (_currentTab != null && !string.IsNullOrEmpty(_currentTab.Url)) await Share.Default.RequestAsync(new ShareTextRequest { Uri = _currentTab.Url, Title = _currentTab.Title }); break;
            case "Incognito: OFF": _isIncognito = true; AddNewTab(); break;
            case "Incognito: ON": _isIncognito = false; await DisplayAlert("", "Incognito off", "OK"); break;
            case "Bookmarks": await ShowBookmarks(); break;
            case "History": await ShowHistory(); break;
            case "Downloads": await ShowDownloads(); break;
            case "Passwords": await ShowPasswords(); break;
            case "Pinned Sites": await ShowPinned(); break;
            case "Profiles": await ShowProfiles(); break;
            case "Themes": await ShowThemes(); break;
            case "Settings": await ShowSettings(); break;
            case "About": await DisplayAlert("Alpha Browser", "Alpha Browser v1.0\nPorted from WPF", "OK"); break;
        }
    }

    private async Task ShowBookmarks()
    {
        var b = _store.Settings.Bookmarks; if (b.Count == 0) { await DisplayAlert("", "No bookmarks.", "OK"); return; }
        var s = await DisplayActionSheet("Bookmarks", "Close", null, b.Select(x => x.Title.Length > 40 ? x.Title[..40] + "…" : x.Title).ToArray());
        if (s != null && s != "Close") { var m = b.FirstOrDefault(x => (x.Title.Length > 40 ? x.Title[..40] + "…" : x.Title) == s); if (m != null) { ShowWebView(); Navigate(m.Url); } }
    }

    private async Task ShowHistory()
    {
        var h = _store.Settings.History; if (h.Count == 0) { await DisplayAlert("", "No history.", "OK"); return; }
        var s = await DisplayActionSheet("History", "Close", "Clear All", h.Select(x => x.Title.Length > 40 ? x.Title[..40] + "…" : x.Title).ToArray());
        if (s == "Clear All") { _store.Settings.History.Clear(); _store.Save(); }
        else if (s != null && s != "Close") { var m = h.FirstOrDefault(x => (x.Title.Length > 40 ? x.Title[..40] + "…" : x.Title) == s); if (m != null) { ShowWebView(); Navigate(m.Url); } }
    }

    private Task ShowDownloads()
    {
        var d = _store.Settings.Downloads;
        return d.Count == 0 ? DisplayAlert("", "No downloads.", "OK") : DisplayActionSheet("Downloads", "Close", null, d.Select(x => $"{x.FileName}").ToArray());
    }

    private Task ShowPasswords()
    {
        var p = _store.Settings.SavedPasswords;
        return p.Count == 0 ? DisplayAlert("", "No passwords.", "OK") : DisplayActionSheet("Passwords", "Close", null, p.Select(x => $"{x.Site}: {x.Username}").ToArray());
    }

    private async Task ShowPinned()
    {
        var p = _store.Settings.PinnedSites;
        if (_currentTab != null && !string.IsNullOrEmpty(_currentTab.Url) && _currentTab.Url != "about:blank" && await DisplayAlert("Pin", $"Pin \"{_currentTab.Title}\"?", "Yes", "View List"))
        { _store.Settings.PinnedSites.Add(new PinnedSite { Title = _currentTab.Title, Url = _currentTab.Url }); _store.Save(); PinnedGrid.ItemsSource = null; PinnedGrid.ItemsSource = _store.Settings.PinnedSites; return; }
        if (p.Count == 0) { await DisplayAlert("", "No pinned sites.", "OK"); return; }
        var s = await DisplayActionSheet("Pinned Sites", "Close", "Clear All", p.Select(x => x.Title).ToArray());
        if (s == "Clear All") { _store.Settings.PinnedSites.Clear(); _store.Save(); PinnedGrid.ItemsSource = null; PinnedGrid.ItemsSource = _store.Settings.PinnedSites; }
        else if (s != null && s != "Close") { var m = p.FirstOrDefault(x => x.Title == s); if (m != null) { ShowWebView(); Navigate(m.Url); } }
    }

    private async Task ShowProfiles()
    {
        var p = _store.Settings.Profiles; if (p.Count == 0) p.Add(new UserProfile { Name = "Default" });
        var s = await DisplayActionSheet("Profiles", "Close", null, p.Select(x => $"{x.AvatarEmoji} {x.Name}").Append("[New]").ToArray());
        if (s == "[New]") { var n = await DisplayPromptAsync("", "Name:"); if (!string.IsNullOrEmpty(n)) { _store.Settings.Profiles.Add(new UserProfile { Name = n }); _store.Save(); } }
        else if (s != null && s != "Close") { var m = p.FirstOrDefault(x => $"{x.AvatarEmoji} {x.Name}" == s); if (m != null) { _store.Settings.ActiveProfileId = m.Id; _store.Settings.HomeUrl = m.HomeUrl; _store.Settings.SearchEngine = m.SearchEngine; _store.Settings.AdBlockEnabled = m.AdBlockEnabled; _store.Save(); ThemeService.Apply(m.Theme); } }
    }

    private async Task ShowThemes()
    {
        var names = ThemeService.Themes.Select(t => t.Name).ToArray();
        var s = await DisplayActionSheet("Theme", "Cancel", null, names);
        if (s != null && s != "Cancel") { _store.Settings.Theme = s; ThemeService.Apply(s); _store.Save(); }
    }

    private async Task ShowSettings()
    {
        var s = _store.Settings;
        var items = new[] {
            $"Home: {ShortUrl(s.HomeUrl)}", $"Search: {s.SearchEngine}", $"AdBlock: {(s.AdBlockEnabled ? "ON" : "OFF")}",
            $"Restore Tabs: {(s.RestoreTabsOnStartup ? "ON" : "OFF")}", "Clear Cache", "Clear History", "Reset"
        };
        var c = await DisplayActionSheet("Settings", "Close", null, items);
        if (c == null || c == "Close") return;
        if (c.StartsWith("Home:")) { var u = await DisplayPromptAsync("", "Home URL:", initialValue: s.HomeUrl); if (!string.IsNullOrEmpty(u)) { s.HomeUrl = u; _store.Save(); } }
        else if (c.StartsWith("Search:")) { var e = await DisplayActionSheet("Engine", "Cancel", null, "Google", "Bing", "DuckDuckGo"); if (e != null && e != "Cancel") { s.SearchEngine = e; _store.Save(); } }
        else if (c.StartsWith("AdBlock:")) { s.AdBlockEnabled = !s.AdBlockEnabled; _store.Save(); }
        else if (c.StartsWith("Restore Tabs:")) { s.RestoreTabsOnStartup = !s.RestoreTabsOnStartup; _store.Save(); }
        else if (c == "Clear Cache") { try { if (_currentTab?.WebView?.Handler?.PlatformView is Android.Webkit.WebView n) n.ClearCache(true); } catch { } await DisplayAlert("", "Cache cleared.", "OK"); }
        else if (c == "Clear History") { s.History.Clear(); _store.Save(); }
        else if (c == "Reset") { if (await DisplayAlert("", "Reset all data?", "Yes", "No")) { _store.Reset(); } }
    }

    private static string ShortUrl(string u) { try { var x = new Uri(u); return x.Host.TrimStart('w', '.'); } catch { return u; } }
}
