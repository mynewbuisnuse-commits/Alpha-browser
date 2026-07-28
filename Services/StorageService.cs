using System.Text;
using System.Text.Json;
using AlphaBrowser.Models;

namespace AlphaBrowser.Services;

public class StorageService
{
    private readonly string _root;
    private readonly string _settingsFile;

    public AppSettings Settings { get; private set; }

    public StorageService()
    {
        _root = Path.Combine(FileSystem.AppDataDirectory, "AlphaBrowser");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "Wallpapers"));
        _settingsFile = Path.Combine(_root, "settings.json");
        Settings = Load();
    }

    public string RootFolder => _root;

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var json = File.ReadAllText(_settingsFile);
                var data = JsonSerializer.Deserialize<AppSettings>(json);
                if (data != null) return data;
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFile, json);
        }
        catch { }
    }

    public void Reset()
    {
        Settings = new AppSettings();
        Save();
    }

    public void AddBookmark(string title, string url)
    {
        Settings.Bookmarks.Insert(0, new BookmarkEntry { Title = title, Url = url, AddedAt = DateTime.Now });
        Save();
    }

    public void AddHistory(string title, string url)
    {
        Settings.History.Insert(0, new HistoryEntry { Title = title, Url = url, VisitedAt = DateTime.Now });
        if (Settings.History.Count > 500)
            Settings.History.RemoveRange(500, Settings.History.Count - 500);
        Save();
    }

    public void AddDownload(string fileName, string filePath, string url)
    {
        Settings.Downloads.Insert(0, new DownloadEntry
        {
            FileName = fileName, FilePath = filePath, Url = url, Date = DateTime.Now
        });
        Save();
    }

    public static string EncryptPassword(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
    }

    public static string DecryptPassword(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64)) return "";
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedBase64)); }
        catch { return ""; }
    }
}
