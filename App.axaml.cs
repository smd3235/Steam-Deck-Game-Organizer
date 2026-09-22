using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace SteamDeckGameOrganizer;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

// Changelog: v1.0 - Initializes the native Avalonia application and single main window.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
