using Avalonia;

namespace SteamDeckGameOrganizer;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

// Changelog: v1.2 - Keeps the cross-platform Avalonia entry point while the publish layout is now a self-contained multi-file application folder.

// Previous changelog: v1.0 - New Steam Deck C# application entry point; release target was a self-contained single Linux x64 executable.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
