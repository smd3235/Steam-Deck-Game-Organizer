namespace SteamDeckGameOrganizer.Models;

public sealed class ScanState
{
    public Dictionary<string, string> Fingerprints { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

// Changelog: v1.0 - Adds New Files Only fingerprint persistence without storing game-review decisions.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
