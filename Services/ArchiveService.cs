using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SteamDeckGameOrganizer.Models;

namespace SteamDeckGameOrganizer.Services;

public sealed class ArchiveService
{
    private static readonly string[] Extensions = [".zip", ".rar", ".7z"];
    private readonly LoggingService _log;

    public ArchiveService(LoggingService log) => _log = log;

    public List<GameRecord> Scan(string root, string mode, IReadOnlyDictionary<string, string> known)
    {
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(p => !IsProtectedPath(p))
            .Where(p => Extensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (mode.Equals("New Files Only", StringComparison.OrdinalIgnoreCase))
            files = files.Where(p => !known.TryGetValue(p, out var oldFingerprint) || !string.Equals(oldFingerprint, Fingerprint(p), StringComparison.OrdinalIgnoreCase)).ToList();

        _log.Log($"ARCHIVE_DISCOVERY total={files.Count} run_mode=[{mode}]");
        return files.Select(p => new GameRecord
        {
            ArchivePath = p,
            ArchiveName = Path.GetFileName(p),
            CleanName = CleanGameName(Path.GetFileNameWithoutExtension(p)),
            NeedsReview = true,
            Fingerprint = Fingerprint(p)
        }).ToList();
    }

    public static bool IsProtectedPath(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(x => x.Equals("Archive", StringComparison.OrdinalIgnoreCase));

    public static string CleanGameName(string name)
    {
        var n = Regex.Replace(name, @"[\s_.-]+(SteamRip|GOG|DRM\s*Free|CODEX|TENOKE|RUNE|Goldberg|Portable|Repack|MULTI|PROPER|DODI|Fit\s*Girl|FitGirl|ISO)[\s_.-]*$", "", RegexOptions.IgnoreCase);
        n = Regex.Replace(n, @"[\s_.-]+v?\d+(?:[._-]\d+){1,5}[\s_.-]*$", "", RegexOptions.IgnoreCase);
        n = Regex.Replace(n, @"[\s_.-]+\d{4}[-_.]\d{2}(?:[-_.]\d{2})?[\s_.-]*$", "");
        n = Regex.Replace(n, @"([a-z0-9])([A-Z])", "$1 $2");
        n = n.Replace('_', ' ').Replace('.', ' ').Replace('-', ' ');
        return Regex.Replace(n, "\\s+", " ").Trim();
    }


    public string BuildDestination(GameRecord game, string root)
    {
        var folderName = string.IsNullOrWhiteSpace(game.ReleaseDate) ? game.SteamName : $"{game.SteamName} - {game.ReleaseDate}";
        folderName = Sanitize(string.IsNullOrWhiteSpace(folderName) ? game.CleanName : folderName);
        var category = Sanitize(game.Category);
        var destDir = Path.Combine(root, category, folderName);
        return CollisionSafe(Path.Combine(destDir, game.ArchiveName));
    }

    public async Task<int> OrganizeAsync(IEnumerable<GameRecord> games, string root, bool preview, CancellationToken ct)
    {
        var count = 0;
        foreach (var game in games.Where(x => !x.Skipped && !string.IsNullOrWhiteSpace(x.Category) && x.Category != "Unsorted"))
        {
            ct.ThrowIfCancellationRequested();
            var dest = BuildDestination(game, root);
            var destDir = Path.GetDirectoryName(dest)!;
            _log.Log($"ORGANIZE_PLAN source=[{game.ArchivePath}] destination=[{dest}] preview={preview}");
            if (!preview)
            {
                Directory.CreateDirectory(destDir);
                File.Move(game.ArchivePath, dest);
                if (!File.Exists(dest)) throw new IOException("Move verification failed");
                _log.Log($"ORGANIZE_MOVE_OK source=[{game.ArchivePath}] destination=[{dest}]");
            }
            count++;
            await Task.Yield();
        }
        return count;
    }

    private static string Fingerprint(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(sha.ComputeHash(stream));
        var info = new FileInfo(path);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}:{hash}";
    }

    private static string CollisionSafe(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 2; i < 10000; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        throw new IOException("Could not find a collision-safe destination");
    }

    private static string Sanitize(string s) => Regex.Replace(s.Replace('/', '-').Replace('\\', '-'), "[<>:\"|?*]", "-").Trim().TrimEnd('.');
}

// Changelog: v1.0 - Adds native archive discovery, name normalization, New Files Only fingerprints, protected Archive exclusion, preview mode, and collision-safe organization.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
