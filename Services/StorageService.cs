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
        _settingsFile = Path.Combine(_root, "settings.json");
        Settings = Load();
        if (string.IsNullOrWhiteSpace(Settings.DownloadFolder))
            Settings.DownloadFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
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
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Settings, options);
            File.WriteAllText(_settingsFile, json);
        }
        catch { }
    }

    public static string EncryptPassword(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        var bytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(bytes);
    }

    public static string DecryptPassword(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64)) return "";
        try
        {
            var bytes = Convert.FromBase64String(encryptedBase64);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }
}
