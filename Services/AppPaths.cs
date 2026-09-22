namespace SteamDeckGameOrganizer.Services;

public static class AppPaths
{
    public static string AppRoot => AppContext.BaseDirectory;
    public static string LogsRoot => Ensure("logs");
    public static string CacheRoot => Ensure("cache");
    public static string ConfigRoot => Ensure("config");
    public static string ArtworkRoot => Ensure(Path.Combine("cache", "artwork"));

    public static string DebugLogPath => Path.Combine(LogsRoot, "debug.log");
    public static string SettingsPath => Path.Combine(ConfigRoot, "settings.json");
    public static string ScanStatePath => Path.Combine(ConfigRoot, "scan-state.json");

    private static string Ensure(string relativePath)
    {
        var path = Path.Combine(AppRoot, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }
}

// Changelog: v1.2 - Centralizes application-relative logs, cache, configuration, and artwork paths so runtime data stays beside the published application on SteamOS and Windows.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
