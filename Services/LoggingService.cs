namespace SteamDeckGameOrganizer.Services;

public sealed class LoggingService
{
    private readonly string _path;
    private readonly object _gate = new();

    public LoggingService()
    {
        _path = Environment.GetEnvironmentVariable("STEAM_DECK_GAME_ORGANIZER_LOG")
            ?? AppPaths.DebugLogPath;
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
    }

    public string Path => _path;

    public void Log(string message)
    {
        lock (_gate)
        {
            File.AppendAllText(_path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  {message}{Environment.NewLine}");
        }
    }
}

// Changelog: v1.2 - Stores the default diagnostic log under the application folder while preserving the explicit environment-variable override.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
