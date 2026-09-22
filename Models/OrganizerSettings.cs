namespace SteamDeckGameOrganizer.Models;

public sealed class OrganizerSettings
{
    public string LibraryPath { get; set; } = "";
    public string SteamGridDbKey { get; set; } = "";
    public string RunMode { get; set; } = "All Archives";
    public bool PreviewOnly { get; set; }
}

// Changelog: v1.0 - Adds persistent Steam Deck user settings for library path, SteamGridDB key, run mode, and preview mode.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
