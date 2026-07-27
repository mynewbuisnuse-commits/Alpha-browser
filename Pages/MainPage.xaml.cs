using System.Collections.ObjectModel;
using AlphaBrowser.Models;
using AlphaBrowser.Services;

namespace AlphaBrowser.Pages;

public partial class MainPage : ContentPage
{
    private readonly StorageService _storage;
    private readonly ObservableCollection<BrowserTab> _tabs = new();
    private BrowserTab? _currentTab;

    public MainPage()
    {
        InitializeComponent();
        _storage = new StorageService();
        AddNewTab("https://www.google.com");
    }

    // ── Tab Management ─────────────────────────────────────────────

    private BrowserTab AddNewTab(string? url = null)
    {
        var tab = new BrowserTab
        {
            Title = "New Tab",
            Url = url ?? _storage.Settings.HomeUrl,
            WebView = CreateWebView()
        };

        _tabs.Add(tab);
        ContentArea.Children.Add(tab.WebView);

        if (url != null)
            tab.WebView.Source = new UrlWebViewSource { Url = url };

        SelectTab(tab);
        RenderTabStrip();
        return tab;
    }

    private WebView CreateWebView()
    {
        var wv = new WebView
        {
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            IsVisible = false
        };

        wv.Navigating += OnWebViewNavigating;
        wv.Navigated += OnWebViewNavigated;

#if ANDROID
        wv.HandlerChanged += (s, e) =>
        {
            if (wv.Handler?.PlatformView is Android.Webkit.WebView nativeWv)
            {
                nativeWv.Settings.JavaScriptEnabled = true;
                nativeWv.Settings.DomStorageEnabled = true;
                nativeWv.Settings.MixedContentMode = Android.Webkit.MixedContentHandling.AlwaysAllow;
                nativeWv.Settings.BuiltInZoomControls = true;
                nativeWv.Settings.DisplayZoomControls = false;
                nativeWv.Settings.LoadWithOverviewMode = true;
                nativeWv.Settings.UseWideViewPort = true;
                nativeWv.Settings.SetSupportMultipleWindows(true);
                nativeWv.SetWebChromeClient(new BrowserChromeClient());
            }
        };
#endif

        return wv;
    }

    private void SelectTab(BrowserTab tab)
    {
        if (_currentTab != null)
        {
            _currentTab.IsSelected = false;
            _currentTab.WebView.IsVisible = false;
        }
        _currentTab = tab;
        _currentTab.IsSelected = true;
        _currentTab.WebView.IsVisible = true;
        AddressBar.Text = _currentTab.Url;
        UpdateNavButtons();
    }

    private void CloseTab(BrowserTab tab)
    {
        if (_tabs.Count <= 1) return;
        var idx = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        ContentArea.Children.Remove(tab.WebView);
        if (_currentTab == tab)
            SelectTab(_tabs[Math.Min(idx, _tabs.Count - 1)]);
        RenderTabStrip();
    }

    private void RenderTabStrip()
    {
        TabStrip.Children.Clear();
        foreach (var tab in _tabs)
        {
            var btn = new Button
            {
                Text = tab.Title.Length > 12 ? tab.Title[..12] + "…" : tab.Title,
                FontSize = 12,
                BackgroundColor = tab == _currentTab
                    ? Color.FromArgb("#26262C")
                    : Color.FromArgb("#1B1B1F"),
                TextColor = Color.FromArgb("#EDEDF2"),
                WidthRequest = 100,
                HeightRequest = 32,
                CornerRadius = 6,
                Padding = new Thickness(8, 0)
            };

            var closeBtn = new Button
            {
                Text = "✕",
                FontSize = 10,
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#9A9AA6"),
                WidthRequest = 20,
                HeightRequest = 20,
                Padding = new Thickness(0)
            };

            var stack = new HorizontalStackLayout
            {
                Spacing = 2,
                Children = { btn, closeBtn }
            };

            var frame = new Border
            {
                Content = stack,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                Stroke = Color.FromArgb("#34343C"),
                StrokeThickness = tab == _currentTab ? 0 : 1,
                BackgroundColor = Colors.Transparent,
                Padding = new Thickness(4, 2),
                Margin = new Thickness(1, 0)
            };

            var captured = tab;
            btn.Clicked += (s, e) => SelectTab(captured);
            closeBtn.Clicked += (s, e) => CloseTab(captured);

            TabStrip.Children.Add(frame);
        }

        // Add tab button
        var addBtn = new Button
        {
            Text = "+",
            FontSize = 16,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#EDEDF2"),
            WidthRequest = 32,
            HeightRequest = 32
        };
        addBtn.Clicked += (s, e) => AddNewTab();
        TabStrip.Children.Add(addBtn);
    }

    // ── Navigation ─────────────────────────────────────────────────

    private void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AddressBar.Text = e.Url;
            LoadingLabel.IsVisible = true;
            if (_currentTab != null)
            {
                _currentTab.IsLoading = true;
                _currentTab.Url = e.Url;
            }
        });
    }

    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            LoadingLabel.IsVisible = false;
            if (_currentTab != null)
            {
                _currentTab.IsLoading = false;
                _currentTab.Url = e.Url;

                // Extract title via JS
                try
                {
                    var title = await _currentTab.WebView.EvaluateJavaScriptAsync("document.title");
                    if (!string.IsNullOrEmpty(title))
                        _currentTab.Title = title;
                }
                catch { }

                UpdateNavButtons();
                RenderTabStrip();

                // Record history
                if (!string.IsNullOrEmpty(e.Url) && !e.Url.StartsWith("file://"))
                {
                    _storage.Settings.History.Insert(0, new HistoryEntry
                    {
                        Title = _currentTab.Title,
                        Url = e.Url,
                        VisitedAt = DateTime.Now
                    });
                    if (_storage.Settings.History.Count > 500)
                        _storage.Settings.History.RemoveRange(500,
                            _storage.Settings.History.Count - 500);
                    _storage.Save();
                }

                // Inject adblock CSS if enabled
                if (_storage.Settings.AdBlockEnabled)
                {
                    try
                    {
                        await _currentTab.WebView.EvaluateJavaScriptAsync(AdBlockService.InjectCssScript());
                    }
                    catch { }
                }
            }
        });
    }

    private void OnAddressSubmitted(object? sender, EventArgs e)
    {
        var text = AddressBar.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        string url;
        if (text.Contains(".") && !text.Contains(" "))
            url = text.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? text : "https://" + text;
        else
            url = $"https://www.google.com/search?q={Uri.EscapeDataString(text)}";

        if (_currentTab != null)
        {
            _currentTab.Url = url;
            _currentTab.WebView.Source = new UrlWebViewSource { Url = url };
        }
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        if (_currentTab?.WebView?.CanGoBack == true)
            _currentTab.WebView.GoBack();
    }

    private void OnForwardClicked(object? sender, EventArgs e)
    {
        if (_currentTab?.WebView?.CanGoForward == true)
            _currentTab.WebView.GoForward();
    }

    private void OnReloadClicked(object? sender, EventArgs e)
    {
        _currentTab?.WebView?.Reload();
    }

    private void OnHomeClicked(object? sender, EventArgs e)
    {
        NavigateTo(_storage.Settings.HomeUrl);
    }

    private void UpdateNavButtons()
    {
        BackButton.Opacity = _currentTab?.WebView?.CanGoBack == true ? 1.0 : 0.4;
        ForwardButton.Opacity = _currentTab?.WebView?.CanGoForward == true ? 1.0 : 0.4;
    }

    public void NavigateTo(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        if (_currentTab != null)
        {
            _currentTab.Url = url;
            _currentTab.WebView.Source = new UrlWebViewSource { Url = url };
            AddressBar.Text = url;
        }
    }

    // ── Bottom Bar Actions ─────────────────────────────────────────

    private async void OnBookmarksClicked(object? sender, EventArgs e)
    {
        var bookmarks = _storage.Settings.Bookmarks;
        if (bookmarks.Count == 0)
        {
            await DisplayAlert("Bookmarks", "No bookmarks yet.\n\nNavigate to a page, then long-press the address bar to bookmark it.", "OK");
            return;
        }

        var items = bookmarks.Select(b => b.Title).ToArray();
        var selected = await DisplayActionSheet("Bookmarks", "Cancel", null, items);
        if (selected != null && selected != "Cancel")
        {
            var bookmark = bookmarks.FirstOrDefault(b => b.Title == selected);
            if (bookmark != null)
                NavigateTo(bookmark.Url);
        }
    }

    private async void OnHistoryClicked(object? sender, EventArgs e)
    {
        var history = _storage.Settings.History;
        if (history.Count == 0)
        {
            await DisplayAlert("History", "No history yet.", "OK");
            return;
        }

        var items = history.Select(h => h.Title.Length > 40 ? h.Title[..40] + "…" : h.Title).ToArray();
        var selected = await DisplayActionSheet("History (Clear All = clear)", "Cancel", "Clear All", items);
        if (selected == "Clear All")
        {
            _storage.Settings.History.Clear();
            _storage.Save();
            await DisplayAlert("History", "History cleared.", "OK");
        }
        else if (selected != null && selected != "Cancel")
        {
            var entry = history.FirstOrDefault(h => h.Title == selected || (h.Title.Length > 40 ? h.Title[..40] + "…" : h.Title) == selected);
            if (entry != null)
                NavigateTo(entry.Url);
        }
    }

    private async void OnDownloadsClicked(object? sender, EventArgs e)
    {
        var downloads = _storage.Settings.Downloads;
        if (downloads.Count == 0)
        {
            await DisplayAlert("Downloads", "No downloads yet.", "OK");
            return;
        }

        var items = downloads.Select(d => $"{d.FileName} ({d.ProgressText})").ToArray();
        await DisplayActionSheet("Downloads", "Close", null, items);
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheet("Settings", "Close", null,
            "Home Page", "Search Engine", "Ad Block: " + (_storage.Settings.AdBlockEnabled ? "On" : "Off"),
            "Clear Cache", "Clear History", "Reset Browser");

        switch (action)
        {
            case "Home Page":
                var homeUrl = await DisplayPromptAsync("Home Page", "Enter URL:", initialValue: _storage.Settings.HomeUrl);
                if (!string.IsNullOrEmpty(homeUrl))
                {
                    _storage.Settings.HomeUrl = homeUrl;
                    _storage.Save();
                }
                break;

            case "Search Engine":
                var engine = await DisplayActionSheet("Search Engine", "Cancel", null, "Google", "Bing", "DuckDuckGo");
                if (engine != null && engine != "Cancel")
                {
                    _storage.Settings.SearchEngine = engine;
                    _storage.Save();
                }
                break;

            case "Ad Block: On":
            case "Ad Block: Off":
                _storage.Settings.AdBlockEnabled = !_storage.Settings.AdBlockEnabled;
                _storage.Save();
                await DisplayAlert("Ad Block", $"Ad blocking is now {(_storage.Settings.AdBlockEnabled ? "enabled" : "disabled")}.\nReload pages for changes to take effect.", "OK");
                break;

            case "Clear Cache":
                try
                {
                    if (_currentTab?.WebView?.Handler?.PlatformView is Android.Webkit.WebView nativeWv)
                        nativeWv.ClearCache(true);
                }
                catch { }
                await DisplayAlert("Cache", "Cache cleared.", "OK");
                break;

            case "Clear History":
                _storage.Settings.History.Clear();
                _storage.Save();
                await DisplayAlert("History", "History cleared.", "OK");
                break;

            case "Reset Browser":
                var confirm = await DisplayAlert("Reset", "Reset all browser data?", "Yes", "No");
                if (confirm)
                {
                    File.Delete(Path.Combine(FileSystem.AppDataDirectory, "AlphaBrowser", "settings.json"));
                    _storage.Reset();
                    await DisplayAlert("Reset", "Browser has been reset. Restart the app.", "OK");
                }
                break;
        }
    }

    private void OnTabsClicked(object? sender, EventArgs e)
    {
        var tabNames = _tabs.Select(t => t.Title.Length > 25 ? t.Title[..25] + "…" : t.Title).ToArray();
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var selected = await DisplayActionSheet($"Tabs ({_tabs.Count})", "New Tab", "Close Current Tab", tabNames);
            if (selected == "New Tab")
                AddNewTab();
            else if (selected == "Close Current Tab")
            {
                if (_currentTab != null)
                    CloseTab(_currentTab);
            }
            else if (selected != null && selected != "Cancel")
            {
                var tab = _tabs.FirstOrDefault(t =>
                    (t.Title.Length > 25 ? t.Title[..25] + "…" : t.Title) == selected);
                if (tab != null)
                    SelectTab(tab);
            }
        });
    }

    private async void OnMenuClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheet("Alpha Browser", "Close", null,
            "New Tab", "Bookmark This Page", "Share Page", "Incognito Mode",
            "Add to Home Screen", "About");

        switch (action)
        {
            case "New Tab":
                AddNewTab();
                break;

            case "Bookmark This Page":
                if (_currentTab != null && !string.IsNullOrEmpty(_currentTab.Url))
                {
                    _storage.Settings.Bookmarks.Insert(0, new BookmarkEntry
                    {
                        Title = _currentTab.Title,
                        Url = _currentTab.Url,
                        AddedAt = DateTime.Now
                    });
                    _storage.Save();
                    await DisplayAlert("Bookmark", "Page bookmarked!", "OK");
                }
                break;

            case "Share Page":
                if (_currentTab != null && !string.IsNullOrEmpty(_currentTab.Url))
                {
                    await Share.RequestAsync(new ShareTextRequest
                    {
                        Uri = _currentTab.Url,
                        Title = _currentTab.Title
                    });
                }
                break;

            case "Incognito Mode":
                var incogUrl = _storage.Settings.HomeUrl;
                var incogTab = new BrowserTab
                {
                    Title = "Incognito",
                    Url = incogUrl,
                    WebView = CreateWebView()
                };
                // Differentiate incognito visually
                _tabs.Add(incogTab);
                ContentArea.Children.Add(incogTab.WebView);
                incogTab.WebView.Source = new UrlWebViewSource { Url = incogUrl };
                SelectTab(incogTab);
                RenderTabStrip();
                break;

            case "About":
                await DisplayAlert("Alpha Browser",
                    "Alpha Browser for Android\nVersion 1.0\n\nPort from WPF to MAUI",
                    "OK");
                break;
        }
    }

    // ── Android Chrome Client ──────────────────────────────────────
#if ANDROID
    private class BrowserChromeClient : Android.Webkit.WebChromeClient
    {
        public override void OnProgressChanged(Android.Webkit.WebView? view, int newProgress)
        {
            base.OnProgressChanged(view, newProgress);
        }
    }
#endif
}
