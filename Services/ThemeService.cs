namespace AlphaBrowser.Services;

public static class ThemeService
{
    public static List<ThemeDefinition> Themes { get; } = new()
    {
        new("Dark", "#121214", "#1B1B1F", "#26262C", "#7C5CFF", false),
        new("Midnight", "#0A0A1A", "#12122A", "#1A1A3A", "#4A6CF7", false),
        new("Obsidian", "#0D0D0D", "#1A1A1A", "#2A2A2A", "#E06C75", false),
        new("Crimson", "#1A0A0A", "#2A1212", "#3A1A1A", "#E74C3C", false),
        new("Emerald", "#0A1A0A", "#122A12", "#1A3A1A", "#2ECC71", false),
        new("Sunset", "#1A120A", "#2A1A12", "#3A2A1A", "#F39C12", false),
        new("Aurora", "#0A0A1A", "#12122A", "#1A1A3A", "#9B59B6", false),
        new("Galaxy", "#0A001A", "#10002A", "#1A003A", "#7C5CFF", false),
        new("Light", "#F5F5F5", "#FFFFFF", "#EBEBEB", "#7C5CFF", true),
        new("Lavender", "#F0E6FF", "#F5F0FF", "#EBE0FF", "#9B59B6", true),
        new("Seashell", "#FFF5EE", "#FFF8F5", "#FFF0E8", "#E67E22", true),
        new("Arctic", "#E8F4FD", "#F0F8FF", "#E0F0FA", "#3498DB", true),
        new("Neon", "#0A0A0A", "#1A1A1A", "#2A2A2A", "#00FF88", false),
        new("Sakura", "#1A0A12", "#2A1220", "#3A1A2A", "#FF69B4", false),
        new("Abyss", "#000510", "#000A1A", "#00102A", "#00BCD4", false),
        new("Magma", "#1A0A00", "#2A1200", "#3A1A00", "#FF5722", false),
        new("Mint", "#0A1A12", "#122A1A", "#1A3A2A", "#1ABC9C", false),
        new("GoldenHour", "#1A1410", "#2A1C14", "#3A2418", "#FFD700", false),
    };

    public static ThemeDefinition Current { get; private set; } = Themes[0];
    public static event Action? ThemeChanged;

    public static void Apply(string name)
    {
        var theme = Themes.FirstOrDefault(t => t.Name == name) ?? Themes[0];
        Current = theme;
        ThemeChanged?.Invoke();
    }
}

public record ThemeDefinition(
    string Name,
    string BgDark,
    string BgPanel,
    string BgElevated,
    string Accent,
    bool IsLight
);
