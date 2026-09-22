using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using SteamDeckGameOrganizer.Models;

namespace SteamDeckGameOrganizer.Services;

public sealed class SteamService
{
    private readonly HttpClient _http;
    private readonly LoggingService _log;

    public SteamService(LoggingService log)
    {
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamDeckGameOrganizer/1.0");
    }

    public async Task<bool> VerifySteamGridDbKeyAsync(string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.steamgriddb.com/api/v2/search/autocomplete/test");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key.Trim());
        try
        {
            using var response = await _http.SendAsync(request, ct);
            _log.Log($"SGDB_VERIFY status={(int)response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { _log.Log($"SGDB_VERIFY_ERROR {ex.GetType().Name}:{ex.Message}"); return false; }
    }

    public async Task<GameRecord> ResolveAsync(GameRecord game, CancellationToken ct)
    {
        var variants = BuildSearchVariants(game.CleanName).ToList();
        _log.Log($"STEAM_LOOKUP start query=[{game.CleanName}] variants=[{string.Join(" | ", variants)}]");

        foreach (var query in variants)
        {
            try
            {
                var url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(query)}&cc=us&l=english";
                using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
                if (!doc.RootElement.TryGetProperty("items", out var items)) continue;
                var candidates = items.EnumerateArray()
                    .Select(x => new
                    {
                        Id = x.TryGetProperty("id", out var id) ? id.GetInt32().ToString() : "",
                        Name = x.TryGetProperty("name", out var n) ? n.GetString() ?? "" : ""
                    })
                    .Where(x => x.Id.Length > 0 && x.Name.Length > 0)
                    .OrderByDescending(x => TitleScore(query, x.Name))
                    .Take(15)
                    .ToList();

                foreach (var candidate in candidates)
                {
                    if (!Plausible(query, candidate.Name)) continue;
                    var enriched = await GetDetailsAsync(candidate.Id, ct);
                    if (enriched is null) continue;
                    enriched.CleanName = game.CleanName;
                    enriched.ArchivePath = game.ArchivePath;
                    enriched.ArchiveName = game.ArchiveName;
                    enriched.Category = game.Category;
                    enriched.NeedsReview = true;
                    _log.Log($"STEAM_LOOKUP result query=[{query}] appid=[{enriched.AppId}] steam=[{enriched.SteamName}]");
                    return enriched;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log.Log($"STEAM_LOOKUP_ERROR query=[{query}] error={ex.GetType().Name}:{ex.Message}"); }
        }

        _log.Log($"STEAM_LOOKUP result query=[{game.CleanName}] found=0");
        return game;
    }

    private async Task<GameRecord?> GetDetailsAsync(string appId, CancellationToken ct)
    {
        var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=us&l=english";
        using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
        if (!doc.RootElement.TryGetProperty(appId, out var root) || !root.GetProperty("success").GetBoolean()) return null;
        var d = root.GetProperty("data");
        var game = new GameRecord { AppId = appId, SteamName = d.GetProperty("name").GetString() ?? "" };
        if (d.TryGetProperty("release_date", out var rd)) game.ReleaseDate = NormalizeDate(rd.TryGetProperty("date", out var date) ? date.GetString() : "");
        if (d.TryGetProperty("genres", out var genres)) game.Genres = genres.EnumerateArray().Select(x => x.GetProperty("description").GetString() ?? "").Where(x => x.Length > 0).ToList();
        if (d.TryGetProperty("categories", out var cats))
        {
            var all = cats.EnumerateArray().Select(x => x.TryGetProperty("description", out var v) ? v.GetString() ?? "" : "").ToList();
            game.PlayerModes = all.Where(IsPlayerMode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        if (d.TryGetProperty("developers", out var dev)) game.Developer = string.Join(", ", dev.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)));
        if (d.TryGetProperty("publishers", out var pub)) game.Publisher = string.Join(", ", pub.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)));
        if (d.TryGetProperty("short_description", out var desc)) game.Description = Regex.Replace(WebUtility.HtmlDecode(desc.GetString() ?? ""), "<.*?>", " ");
        game.Tags = await GetTopTagsAsync(appId, ct);
        ApplySuggestion(game);
        return game;
    }

    private async Task<List<string>> GetTopTagsAsync(string appId, CancellationToken ct)
    {
        try
        {
            var html = await _http.GetStringAsync($"https://store.steampowered.com/app/{appId}/?l=english", ct);
            return Regex.Matches(html, @"store_tag_\d+[^>]*>\s*<a[^>]*>([^<]+)", RegexOptions.IgnoreCase)
                .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList();
        }
        catch { return []; }
    }

    private static bool IsPlayerMode(string s) =>
        s.Contains("Single-player", StringComparison.OrdinalIgnoreCase) ||
        s.Contains("Co-op", StringComparison.OrdinalIgnoreCase) ||
        s.Contains("PvP", StringComparison.OrdinalIgnoreCase) ||
        s.Contains("Online", StringComparison.OrdinalIgnoreCase) ||
        s.Contains("Shared/Split", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDate(string? raw) => DateTime.TryParse(raw, out var dt) ? dt.ToString("yyyy-MM-dd") : raw ?? "";

    private static IEnumerable<string> BuildSearchVariants(string value)
    {
        yield return value;
        var stripped = Regex.Replace(value, @"\b(incremental|simulator|demo|prologue|edition|deluxe|remastered)\b", " ", RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, "\\s+", " ").Trim();
        if (!string.Equals(stripped, value, StringComparison.OrdinalIgnoreCase) && stripped.Length >= 4) yield return stripped;
    }

    private static int TitleScore(string q, string n)
    {
        var a = Tokens(q); var b = Tokens(n);
        if (a.Count == 0 || b.Count == 0) return 0;
        var overlap = a.Count(t => b.Contains(t));
        var exact = string.Equals(q.Trim(), n.Trim(), StringComparison.OrdinalIgnoreCase) ? 100 : 0;
        return exact + overlap * 10 - Math.Abs(a.Count - b.Count);
    }

    private static bool Plausible(string q, string n) => TitleScore(q, n) >= 18 || string.Equals(q.Trim(), n.Trim(), StringComparison.OrdinalIgnoreCase);
    private static HashSet<string> Tokens(string s) => Regex.Matches(s.ToLowerInvariant(), "[a-z0-9]+").Select(m => m.Value).ToHashSet();

    private static void ApplySuggestion(GameRecord g)
    {
        var all = g.Genres.Concat(g.Tags).Select(x => x.ToLowerInvariant()).ToList();
        var rules = new (string Category, string[] Keys)[]
        {
            ("Roguelite", ["roguelite"]), ("Roguelike", ["roguelike"]), ("Soulslike", ["souls-like", "soulslike"]),
            ("Metroidvania", ["metroidvania"]), ("Platformer", ["platformer"]), ("First-Person Shooter", ["first-person shooter", "fps"]),
            ("Third-Person Shooter", ["third-person shooter"]), ("Fighting", ["fighting"]), ("Horror", ["horror"]),
            ("Puzzle", ["puzzle"]), ("Tower Defense", ["tower defense"]), ("Deckbuilding", ["deckbuilding"]),
            ("Card Game", ["card game"]), ("Turn-Based", ["turn-based"]), ("Tactical", ["tactical"]), ("Stealth", ["stealth"]),
            ("Sandbox", ["sandbox"]), ("Open World", ["open world"]), ("Walking Sim", ["walking simulator"]),
            ("Life Sim", ["life sim"]), ("Tycoon", ["tycoon"]), ("Shop Management", ["shop keeper", "shop management"]),
            ("Colony Sim", ["colony sim"]), ("City Builder", ["city builder"]), ("Automation", ["automation"]),
            ("Factory", ["factory"]), ("Idle", ["idle"]), ("Incremental", ["incremental"]), ("Fishing", ["fishing"]),
            ("Cooking", ["cooking"]), ("Farming", ["farming"]), ("Crafting", ["crafting"]), ("Party Game", ["party game"]),
            ("Local Co-op", ["local co-op"]), ("Rhythm", ["rhythm"]), ("Visual Novel", ["visual novel"]), ("Cozy", ["cozy"]),
            ("Survivors", ["survivors"])
        };

        foreach (var rule in rules)
        {
            var key = rule.Keys.FirstOrDefault(k => all.Any(x => x.Contains(k, StringComparison.OrdinalIgnoreCase)));
            if (key is null) continue;
            var genreHit = g.Genres.FirstOrDefault(x => rule.Keys.Any(k => x.Contains(k, StringComparison.OrdinalIgnoreCase)));
            g.SuggestedCategory = rule.Category;
            g.SuggestionConfidence = genreHit is not null ? "High" : "Medium";
            g.SuggestionEvidence = genreHit is not null ? $"Steam genre: {genreHit}" : $"Steam tag signal: {g.Tags.FirstOrDefault(x => rule.Keys.Any(k => x.Contains(k, StringComparison.OrdinalIgnoreCase))) ?? rule.Category}";
            g.Category = g.SuggestedCategory;
            return;
        }
        g.SuggestedCategory = "Unsorted";
        g.SuggestionConfidence = "Low";
        g.SuggestionEvidence = "No strong Steam genre/tag signal";
        g.Category = "Unsorted";
    }
}

// Changelog: v1.0 - Native Steam search/detail enrichment, exact/compact title scoring, Steam genres/tags/player modes, and category evidence. Language and Early Access are intentionally excluded from the visual UI.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
