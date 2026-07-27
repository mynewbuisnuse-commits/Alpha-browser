using System.Text.Json.Serialization;

namespace AlphaBrowser.Models;

public class PinnedSite
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string IconBase64 { get; set; } = "";

    [JsonIgnore]
    public string Initial =>
        string.IsNullOrEmpty(Title) ? "?" : Title.Substring(0, 1).ToUpperInvariant();

    [JsonIgnore]
    public ImageSource? IconSource =>
        !string.IsNullOrEmpty(IconBase64)
            ? ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(IconBase64)))
            : null;
}

public class SavedPassword
{
    public string Site { get; set; } = "";
    public string Username { get; set; } = "";
    public string EncryptedPassword { get; set; } = "";
}

public class DownloadEntry
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    public long TotalBytes { get; set; }
    public long ReceivedBytes { get; set; }
    public string State { get; set; } = "InProgress";

    [JsonIgnore]
    public string ProgressText =>
        TotalBytes > 0 ? $"{ReceivedBytes / 1024}KB / {TotalBytes / 1024}KB" : $"{ReceivedBytes / 1024}KB";
}

public class WallpaperItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsBuiltIn { get; set; }
}

public class HistoryEntry
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime VisitedAt { get; set; } = DateTime.Now;
}

public class BookmarkEntry
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime AddedAt { get; set; } = DateTime.Now;
}

public class InstalledApp
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

public class SavedTab
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsPinned { get; set; }
    public bool IsActive { get; set; }
}

public class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Default";
    public string AvatarEmoji { get; set; } = "😊";
    public string AccentHex { get; set; } = "#7C5CFF";
    public string Theme { get; set; } = "Emerald";
    public string CurrentWallpaper { get; set; } = "";
    public string HomeUrl { get; set; } = "https://www.google.com";
    public string SearchEngine { get; set; } = "Google";
    public string AssistantProvider { get; set; } = "Gemini";
    public string AssistantUrl { get; set; } = "https://gemini.google.com/";
    public bool AdBlockEnabled { get; set; } = true;
    public bool RestoreTabsOnStartup { get; set; } = false;
    public List<BookmarkEntry> Bookmarks { get; set; } = new();
    public List<HistoryEntry> History { get; set; } = new();
    public List<SavedPassword> SavedPasswords { get; set; } = new();
    public List<SavedTab> SavedTabs { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class AppSettings
{
    public List<PinnedSite> PinnedSites { get; set; } = new();
    public List<SavedPassword> SavedPasswords { get; set; } = new();
    public List<DownloadEntry> Downloads { get; set; } = new();
    public List<HistoryEntry> History { get; set; } = new();
    public List<BookmarkEntry> Bookmarks { get; set; } = new();
    public List<InstalledApp> InstalledApps { get; set; } = new();
    public List<SavedTab> SavedTabs { get; set; } = new();
    public string CurrentWallpaper { get; set; } = "";
    public string HomeUrl { get; set; } = "https://www.google.com";
    public bool RememberPasswordsEnabled { get; set; } = true;
    public string DownloadFolder { get; set; } = "";
    public string Theme { get; set; } = "Emerald";
    public bool AdBlockEnabled { get; set; } = true;
    public string SearchEngine { get; set; } = "Google";
    public string AssistantUrl { get; set; } = "https://gemini.google.com/";
    public string AssistantProvider { get; set; } = "Gemini";
    public bool BlockWebsiteNavigation { get; set; } = false;
    public bool RestoreTabsOnStartup { get; set; } = false;
    public string GoogleAccountEmail { get; set; } = "";
    public bool GoogleSyncEnabled { get; set; } = true;
    public List<UserProfile> Profiles { get; set; } = new();
    public string ActiveProfileId { get; set; } = "";
}
