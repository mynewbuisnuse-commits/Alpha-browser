using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AlphaBrowser.Models;

public class BrowserTab : INotifyPropertyChanged
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    private static int _nextId;

    public Microsoft.Maui.Controls.WebView WebView { get; set; } = null!;

    private string _title = "New Tab";
    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    private string _url = "";
    public string Url
    {
        get => _url;
        set { _url = value; OnPropertyChanged(); OnPropertyChanged(nameof(Hostname)); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    private string? _faviconSource;
    public string? FaviconSource
    {
        get => _faviconSource;
        set { _faviconSource = value; OnPropertyChanged(); }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    private bool _isPinned;
    public bool IsPinned
    {
        get => _isPinned;
        set { _isPinned = value; OnPropertyChanged(); }
    }

    public string Hostname
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_url)) return "";
            try { return new Uri(_url).Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase); }
            catch { return ""; }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
