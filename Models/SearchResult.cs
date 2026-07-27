using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AlphaBrowser.Models;

public class SearchResult : INotifyPropertyChanged
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Display { get; set; } = "";
    public string Snippet { get; set; } = "";

    private string? _faviconSource;
    public string? FaviconSource
    {
        get => _faviconSource;
        set { _faviconSource = value; OnPropertyChanged(); }
    }

    public string Domain
    {
        get
        {
            try { return new Uri(Url).Host.TrimStart('w', '.'); }
            catch { return Url; }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
