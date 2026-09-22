using System.Text.Json;
using SteamDeckGameOrganizer.Models;

namespace SteamDeckGameOrganizer.Services;

public sealed class SettingsService
{
    private readonly string _root;
    private readonly string _settingsPath;
    private readonly string _statePath;
    private readonly LoggingService _log;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public SettingsService(LoggingService log)
    {
        _log = log;
        _root = AppPaths.ConfigRoot;
        _settingsPath = AppPaths.SettingsPath;
        _statePath = AppPaths.ScanStatePath;
    }

    public OrganizerSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
                return JsonSerializer.Deserialize<OrganizerSettings>(File.ReadAllText(_settingsPath), _json) ?? Defaults();
        }
        catch (Exception ex) { _log.Log($"SETTINGS_LOAD_ERROR error={ex.GetType().Name}:{ex.Message}"); }
        return Defaults();
    }

    public void SaveSettings(OrganizerSettings settings)
    {
        try { File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, _json)); }
        catch (Exception ex) { _log.Log($"SETTINGS_SAVE_ERROR error={ex.GetType().Name}:{ex.Message}"); }
    }

    public ScanState LoadScanState()
    {
        try
        {
            if (File.Exists(_statePath))
                return JsonSerializer.Deserialize<ScanState>(File.ReadAllText(_statePath), _json) ?? new ScanState();
        }
        catch (Exception ex) { _log.Log($"SCAN_STATE_LOAD_ERROR error={ex.GetType().Name}:{ex.Message}"); }
        return new ScanState();
    }

    public void SaveScanState(ScanState state)
    {
        try { File.WriteAllText(_statePath, JsonSerializer.Serialize(state, _json)); }
        catch (Exception ex) { _log.Log($"SCAN_STATE_SAVE_ERROR error={ex.GetType().Name}:{ex.Message}"); }
    }

    private static OrganizerSettings Defaults() => new()
    {
        LibraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Games"),
        RunMode = "All Archives"
    };
}

// Changelog: v1.2 - Moves settings and New Files Only fingerprint state into the application folder config directory for portable SteamOS/Windows deployments.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
