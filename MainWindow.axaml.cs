using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using SteamDeckGameOrganizer.Models;
using SteamDeckGameOrganizer.Services;

namespace SteamDeckGameOrganizer;

public partial class MainWindow : Window
{
    private readonly LoggingService _log = new();
    private readonly SettingsService _settings;
    private readonly SteamService _steam;
    private readonly ArtworkService _artwork;
    private readonly ArchiveService _archive;
    private readonly ObservableCollection<GameRecord> _games = [];
    private readonly List<string> _categories =
    [
        "Roguelite", "Roguelike", "Soulslike", "Metroidvania", "Platformer", "First-Person Shooter", "Third-Person Shooter",
        "Fighting", "Horror", "Puzzle", "Tower Defense", "Deckbuilding", "Card Game", "Turn-Based", "Tactical", "Stealth",
        "Sandbox", "Open World", "Exploration", "Walking Sim", "Life Sim", "Tycoon", "Shop Management", "Colony Sim",
        "City Builder", "Automation", "Factory", "Idle", "Incremental", "Fishing", "Cooking", "Farming", "Crafting",
        "Party Game", "Local Co-op", "Rhythm", "Visual Novel", "Cozy", "Survivors", "Unsorted"
    ];

    private GameRecord? _selected;
    private Bitmap? _selectedBitmap;
    private CancellationTokenSource? _runCts;
    private readonly List<(string Source, string Destination)> _lastMoves = [];
    private OrganizerSettings _currentSettings = new();
    private bool _updatingSelection;

    public MainWindow()
    {
        InitializeComponent();
        _settings = new SettingsService(_log);
        _steam = new SteamService(_log);
        _artwork = new ArtworkService(_log);
        _archive = new ArchiveService(_log);
        CategoryBox.ItemsSource = _categories;
        _currentSettings = _settings.LoadSettings();
        LibraryPathBox.Text = _currentSettings.LibraryPath;
        KeyBox.Text = _currentSettings.SteamGridDbKey;
        RunModeBox.SelectedIndex = string.Equals(_currentSettings.RunMode, "New Files Only", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        PreviewBox.IsChecked = _currentSettings.PreviewOnly;
        _log.Log("===== Steam Deck Game Organizer v1.0 starting =====");
        _ = VerifySavedKeyAsync();
    }

    private async Task VerifySavedKeyAsync()
    {
        var key = KeyBox.Text?.Trim() ?? "";
        if (key.Length == 0)
        {
            ConnectionText.Text = "SteamGridDB: not configured";
            return;
        }
        ConnectionText.Text = "SteamGridDB: verifying…";
        var ok = await _steam.VerifySteamGridDbKeyAsync(key, CancellationToken.None);
        ConnectionText.Text = ok ? "SteamGridDB: connected / verified" : "SteamGridDB: verification failed";
    }

    private async void Verify_Click(object? sender, RoutedEventArgs e)
    {
        var key = KeyBox.Text?.Trim() ?? "";
        SaveSettings();
        if (key.Length == 0)
        {
            ConnectionText.Text = "SteamGridDB: not configured";
            return;
        }
        VerifyButton.IsEnabled = false;
        ConnectionText.Text = "SteamGridDB: verifying…";
        var ok = await _steam.VerifySteamGridDbKeyAsync(key, CancellationToken.None);
        ConnectionText.Text = ok ? "SteamGridDB: connected / verified" : "SteamGridDB: verification failed";
        VerifyButton.IsEnabled = true;
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e)
    {
        var root = LibraryPathBox.Text?.Trim() ?? "";
        if (!Directory.Exists(root))
        {
            StatusText.Text = "Library folder does not exist.";
            return;
        }

        SaveSettings();
        StopCurrentRun();
        _runCts = new CancellationTokenSource();
        StopButton.IsEnabled = true;
        var ct = _runCts.Token;
        _games.Clear();
        _selected = null;
        ClearDetails();
        _lastMoves.Clear();

        var mode = RunModeBox.SelectedIndex == 1 ? "New Files Only" : "All Archives";
        StatusText.Text = "Scanning archives…";
        try
        {
            var state = _settings.LoadScanState();
            var scanned = await Task.Run(() => _archive.Scan(root, mode, state.Fingerprints), ct);
            foreach (var game in scanned) _games.Add(game);
            RenderList();
            StatusText.Text = $"Found {_games.Count} archive(s). Resolving Steam metadata…";

            foreach (var game in _games.ToList())
            {
                ct.ThrowIfCancellationRequested();
                var resolved = await _steam.ResolveAsync(game, ct);
                CopyMetadata(resolved, game);
                state.Fingerprints[game.ArchivePath] = game.Fingerprint;
                RenderList();
                if (_selected is null) SelectGame(game);
            }

            _settings.SaveScanState(state);
            StatusText.Text = $"Review ready — {_games.Count} game(s).";
        }
        catch (OperationCanceledException) { StatusText.Text = "Stopped safely."; }
        catch (Exception ex)
        {
            _log.Log($"RUN_ERROR {ex.GetType().Name}:{ex.Message}");
            StatusText.Text = "Run stopped with an error. See debug log.";
        }
        finally
        {
            StopButton.IsEnabled = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    private void Stop_Click(object? sender, RoutedEventArgs e)
    {
        StopCurrentRun();
        StatusText.Text = "Stopping safely…";
    }

    private void StopCurrentRun()
    {
        try { _runCts?.Cancel(); } catch { }
    }

    private void RenderList()
    {
        var search = (SearchBox.Text ?? "").Trim();
        var filter = (FilterBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
        GameList.Children.Clear();
        foreach (var game in _games.Where(x => Matches(x, search, filter)).OrderBy(x => string.IsNullOrWhiteSpace(x.SteamName) ? x.CleanName : x.SteamName, StringComparer.OrdinalIgnoreCase))
        {
            var button = new Button
            {
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Padding = new Avalonia.Thickness(9, 7),
                Content = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(game.SteamName) ? game.CleanName : game.SteamName,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                }
            };
            button.Click += (_, _) => SelectGame(game);
            GameList.Children.Add(button);
        }
    }

    private static bool Matches(GameRecord game, string search, string filter)
    {
        if (search.Length > 0 && !($"{game.CleanName} {game.SteamName} {game.TagsText} {game.SuggestedCategory}".Contains(search, StringComparison.OrdinalIgnoreCase))) return false;
        return filter switch
        {
            "Needs Review" => game.NeedsReview,
            "High Confidence" => game.SuggestionConfidence == "High",
            "Medium Confidence" => game.SuggestionConfidence == "Medium",
            "Low Confidence" => game.SuggestionConfidence == "Low",
            _ => true
        };
    }

    private async void SelectGame(GameRecord game)
    {
        _selected = game;
        DetailTitle.Text = string.IsNullOrWhiteSpace(game.SteamName) ? game.CleanName : game.SteamName;
        DetailMatch.Text = game.MatchText;
        MetaAppId.Text = $"AppID: {game.AppId}";
        MetaRelease.Text = $"Release: {game.ReleaseDate}";
        MetaMode.Text = $"Mode: {game.PlayerModeText}";
        MetaDeveloper.Text = $"Developer: {game.Developer}";
        MetaPublisher.Text = $"Publisher: {game.Publisher}";
        MetaGenres.Text = $"Steam Genres: {game.GenresText}";
        MetaTags.Text = $"Steam Tags: {game.TagsText}";
        ConfidenceText.Text = $"Confidence: {game.SuggestionConfidence}";
        EvidenceText.Text = $"Suggested: {game.SuggestedCategory}  •  {game.SuggestionEvidence}";
        _updatingSelection = true;
        CategoryBox.SelectedItem = game.Category;
        _updatingSelection = false;
        _selectedBitmap?.Dispose();
        _selectedBitmap = null;
        ArtworkImage.Source = null;

        if (!game.SteamMatched)
        {
            _log.Log($"ARTWORK_SKIP game=[{game.CleanName}] reason=[no-steam-match]");
            return;
        }

        game.ArtworkLoading = true;
        var bitmap = await _artwork.LoadAsync(game, CancellationToken.None);
        if (ReferenceEquals(_selected, game))
        {
            _selectedBitmap = bitmap;
            ArtworkImage.Source = bitmap;
            _log.Log($"ARTWORK_ASSIGNED game=[{game.SteamName}] appid=[{game.AppId}] success={(bitmap is not null)}");
        }
        game.ArtworkLoading = false;
    }

    private void CopyMetadata(GameRecord source, GameRecord destination)
    {
        destination.CleanName = source.CleanName;
        destination.SteamName = source.SteamName;
        destination.AppId = source.AppId;
        destination.ReleaseDate = source.ReleaseDate;
        destination.Genres = source.Genres;
        destination.Tags = source.Tags;
        destination.PlayerModes = source.PlayerModes;
        destination.Developer = source.Developer;
        destination.Publisher = source.Publisher;
        destination.Description = source.Description;
        destination.SuggestedCategory = source.SuggestedCategory;
        destination.SuggestionConfidence = source.SuggestionConfidence;
        destination.SuggestionEvidence = source.SuggestionEvidence;
        destination.Category = source.Category;
    }

    private void ClearDetails()
    {
        DetailTitle.Text = "Select a game";
        DetailMatch.Text = "";
        MetaAppId.Text = MetaRelease.Text = MetaMode.Text = MetaDeveloper.Text = MetaPublisher.Text = "";
        MetaGenres.Text = MetaTags.Text = EvidenceText.Text = "";
        ConfidenceText.Text = "";
        ArtworkImage.Source = null;
        _selectedBitmap?.Dispose();
        _selectedBitmap = null;
    }

    private void Category_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection || _selected is null || CategoryBox.SelectedItem is not string category) return;
        _selected.Category = category;
        _selected.NeedsReview = false;
        RenderList();
    }

    private void Skip_Click(object? sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        _selected.Skipped = true;
        _selected.NeedsReview = false;
        RenderList();
    }

    private void Search_Changed(object? sender, TextChangedEventArgs e) => RenderList();
    private void Filter_Changed(object? sender, SelectionChangedEventArgs e) => RenderList();

    private void OpenSteam_Click(object? sender, RoutedEventArgs e)
    {
        if (_selected?.AppId is not { Length: > 0 } id) return;
        try { Process.Start(new ProcessStartInfo($"https://store.steampowered.com/app/{id}/") { UseShellExecute = true }); }
        catch (Exception ex) { _log.Log($"OPEN_STEAM_ERROR {ex.GetType().Name}:{ex.Message}"); }
    }

    private async void Organize_Click(object? sender, RoutedEventArgs e)
    {
        if (_games.Count == 0) return;
        SaveSettings();
        var root = LibraryPathBox.Text?.Trim() ?? "";
        var preview = PreviewBox.IsChecked == true;
        using var cts = new CancellationTokenSource();
        StatusText.Text = preview ? "Previewing organization…" : "Organizing reviewed games…";
        _lastMoves.Clear();
        try
        {
            var targets = _games.Where(x => !x.NeedsReview && !x.Skipped && x.Category != "Unsorted").ToList();
            if (!preview)
            {
                foreach (var game in targets)
                {
                    _lastMoves.Add((game.ArchivePath, _archive.BuildDestination(game, root)));
                }
            }
            var count = await _archive.OrganizeAsync(targets, root, preview, cts.Token);
            StatusText.Text = preview ? $"Preview complete — {count} move(s) planned." : $"Organization complete — {count} game(s) moved.";
        }
        catch (Exception ex)
        {
            _log.Log($"ORGANIZE_ERROR {ex.GetType().Name}:{ex.Message}");
            StatusText.Text = "Organization stopped with an error. See debug log.";
        }
    }

    private void Undo_Click(object? sender, RoutedEventArgs e)
    {
        if (_lastMoves.Count == 0)
        {
            StatusText.Text = "Nothing to undo from the last run.";
            return;
        }
        var restored = 0;
        foreach (var move in _lastMoves.AsEnumerable().Reverse())
        {
            try
            {
                if (!File.Exists(move.Destination)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(move.Source)!);
                if (File.Exists(move.Source)) continue;
                File.Move(move.Destination, move.Source);
                restored++;
            }
            catch (Exception ex) { _log.Log($"UNDO_ERROR error={ex.GetType().Name}:{ex.Message}"); }
        }
        _lastMoves.Clear();
        StatusText.Text = $"Undo complete — {restored} archive(s) restored.";
    }

    private void SaveSettings()
    {
        _currentSettings.LibraryPath = LibraryPathBox.Text?.Trim() ?? "";
        _currentSettings.SteamGridDbKey = KeyBox.Text?.Trim() ?? "";
        _currentSettings.RunMode = RunModeBox.SelectedIndex == 1 ? "New Files Only" : "All Archives";
        _currentSettings.PreviewOnly = PreviewBox.IsChecked == true;
        _settings.SaveSettings(_currentSettings);
    }

    protected override void OnClosed(EventArgs e)
    {
        StopCurrentRun();
        _selectedBitmap?.Dispose();
        SaveSettings();
        _log.Log("APP_SHUTDOWN");
        base.OnClosed(e);
    }
}

// Changelog: v1.0 - Rebuilds the Steam Deck workflow as one Avalonia window with safe cancellation, startup key verification, Review selection, metadata/artwork loading, New Files Only state, organization, and last-run undo.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
