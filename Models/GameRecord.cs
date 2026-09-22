namespace SteamDeckGameOrganizer.Models;

public sealed class GameRecord
{
    public string ArchivePath { get; init; } = "";
    public string ArchiveName { get; init; } = "";
    public string CleanName { get; set; } = "";
    public string SteamName { get; set; } = "";
    public string AppId { get; set; } = "";
    public string ReleaseDate { get; set; } = "";
    public List<string> Genres { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<string> PlayerModes { get; set; } = [];
    public string Developer { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string Description { get; set; } = "";
    public string SuggestedCategory { get; set; } = "Unsorted";
    public string SuggestionConfidence { get; set; } = "Low";
    public string SuggestionEvidence { get; set; } = "No strong Steam category signal";
    public string Category { get; set; } = "Unsorted";
    public bool NeedsReview { get; set; } = true;
    public bool Skipped { get; set; }
    public bool ArtworkLoading { get; set; }
    public bool ArtworkFailed { get; set; }
    public string ArtworkPath { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public bool SteamMatched => !string.IsNullOrWhiteSpace(AppId);
    public string MatchText => SteamMatched ? $"Matched: {SteamName}  •  AppID {AppId}" : "Needs Steam verification";
    public string GenresText => Genres.Count == 0 ? "Not available" : string.Join(" • ", Genres);
    public string TagsText => Tags.Count == 0 ? "Not available" : string.Join(" • ", Tags.Take(8));
    public string PlayerModeText => PlayerModes.Count == 0 ? "Not listed" : string.Join(" • ", PlayerModes);
}

// Changelog: v1.0 - Native game model keeps archive identity separate from cleaned name and verified Steam metadata.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
