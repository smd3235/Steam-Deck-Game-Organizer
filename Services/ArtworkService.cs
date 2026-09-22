using Avalonia.Media.Imaging;
using SteamDeckGameOrganizer.Models;

namespace SteamDeckGameOrganizer.Services;

public sealed class ArtworkService
{
    private readonly HttpClient _http;
    private readonly LoggingService _log;
    private readonly string _cache;

    public ArtworkService(LoggingService log)
    {
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamDeckGameOrganizer/1.1");
        _cache = AppPaths.ArtworkRoot;
    }

    public async Task<Bitmap?> LoadAsync(GameRecord game, CancellationToken ct)
    {
        var appid = game.AppId.Trim();
        if (!appid.All(char.IsDigit))
        {
            _log.Log($"ARTWORK_SKIP game=[{game.CleanName}] reason=[invalid-appid]");
            return null;
        }

        _log.Log($"ARTWORK_REQUEST_START game=[{game.SteamName}] appid=[{appid}]");
        var path = Path.Combine(_cache, appid + ".jpg");
        if (!ValidImageFile(path))
        {
            if (File.Exists(path)) _log.Log($"ARTWORK_CACHE_INVALID appid=[{appid}] path=[{path}]");
            await DownloadAsync(appid, game.SteamName, path, ct);
        }
        else
        {
            _log.Log($"ARTWORK_CACHE_HIT game=[{game.SteamName}] appid=[{appid}] path=[{path}]");
        }

        if (!ValidImageFile(path))
        {
            game.ArtworkFailed = true;
            _log.Log($"ARTWORK_UNAVAILABLE game=[{game.SteamName}] appid=[{appid}] reason=[no-valid-cache]");
            return null;
        }

        try
        {
            _log.Log($"ARTWORK_DECODE_START game=[{game.SteamName}] appid=[{appid}] decoder=[Avalonia]");
            await using var stream = File.OpenRead(path);
            var bitmap = new Bitmap(stream);
            game.ArtworkPath = path;
            _log.Log($"ARTWORK_DECODE_COMPLETE game=[{game.SteamName}] appid=[{appid}] size={bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
            return bitmap;
        }
        catch (Exception ex)
        {
            game.ArtworkFailed = true;
            _log.Log($"ARTWORK_RENDER_ERROR appid={appid} error={ex.GetType().Name}:{ex.Message}");
            return null;
        }
    }

    private async Task DownloadAsync(string appid, string gameName, string path, CancellationToken ct)
    {
        var urls = new[]
        {
            $"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{appid}/header.jpg",
            $"https://cdn.akamai.steamstatic.com/steam/apps/{appid}/header.jpg",
            $"https://steamcdn-a.akamaihd.net/steam/apps/{appid}/header.jpg"
        };

        foreach (var url in urls)
        {
            _log.Log($"ARTWORK_SOURCE_SELECTED game=[{gameName}] appid=[{appid}] url=[{url}]");
            try
            {
                _log.Log($"ARTWORK_DOWNLOAD_START game=[{gameName}] appid=[{appid}]");
                using var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _log.Log($"ARTWORK_DOWNLOAD_FAIL appid=[{appid}] status={(int)response.StatusCode}");
                    continue;
                }
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                if (!LooksLikeImage(bytes))
                {
                    _log.Log($"ARTWORK_DOWNLOAD_FAIL appid=[{appid}] reason=[invalid-image-bytes] bytes={bytes.Length}");
                    continue;
                }
                var tmp = path + $".tmp.{Environment.ProcessId}.{Guid.NewGuid():N}.jpg";
                await File.WriteAllBytesAsync(tmp, bytes, ct);
                File.Move(tmp, path, true);
                _log.Log($"ARTWORK_DOWNLOAD_COMPLETE game=[{gameName}] appid=[{appid}] bytes={bytes.Length}");
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log.Log($"ARTWORK_DOWNLOAD_FAIL appid=[{appid}] error={ex.GetType().Name}:{ex.Message}"); }
        }
    }

    private static bool ValidImageFile(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            if (new FileInfo(path).Length < 1024) return false;
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[8];
            var read = stream.Read(header);
            return read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        }
        catch { return false; }
    }

    private static bool LooksLikeImage(byte[] b) =>
        (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) ||
        (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47);
}

// Changelog: v1.2 - Stores Organizer-downloaded artwork in the application-local cache/artwork directory; direct Avalonia decoding remains unchanged.

// Previous changelog: v1.0 - Native artwork pipeline validates Steam image bytes, self-heals bad cache entries, downloads directly, and decodes JPEG without ffmpeg/ImageMagick/Pillow.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
