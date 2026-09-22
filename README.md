# Steam Deck Game Organizer v1.2.1

## Release target
The release target is **SteamOS on Steam Deck, x86_64**, as one self-contained executable:

`SteamDeckGameOrganizer`

The project is configured for `linux-x64`, embeds the .NET runtime, and publishes as a single file. No separate .NET installation, DLL set, Python, Bash launcher, ffmpeg, ImageMagick, or Pillow is required by the finished release.

## Build locally

A .NET 8 SDK is required only on the machine performing a local build. From this project directory:

```bash
dotnet restore
dotnet publish "SteamDeckGameOrganizer.csproj" -c Release -r linux-x64 --self-contained true --publish-single-file false --publish-trimmed false
```

The recommended SteamOS build is the GitHub Actions workflow at `.github/workflows/build-steamos.yml`. It runs on a hosted Ubuntu runner, restores the project, publishes a self-contained `linux-x64` application, sets the executable permission, and creates the ZIP artifact:

`steam_deck_game_organizer_v1.2.1.zip`

The ZIP contains the application folder named exactly `Steam Deck Game Organizer` with the `SteamDeckGameOrganizer` executable and its required runtime files. The Steam Deck does **not** need .NET installed.

## Workflow

**SCAN → REVIEW → ORGANIZE → DONE**

- All Archives is the default scan mode.
- New Files Only uses persistent file fingerprints.
- Review is one page with a narrow list and wide details workspace.
- Steam genres, top tags, player mode, AppID, release date, developer, publisher, category suggestion, confidence, and evidence are shown compactly.
- Language and Early Access are intentionally not shown.
- Artwork downloads and decodes directly in C# without external image conversion tools.
- Stop Safely cancels active work and the window shutdown path cancels outstanding work.
- Organize supports preview mode and a last-run Undo action.
- Settings and diagnostic logs are stored under the Steam Deck user's config/cache directories rather than beside the executable.

## Architecture

One native process:

- Avalonia UI
- Steam metadata service
- Artwork service
- Archive/organization service
- Settings/state service
- Logging service

The application does not depend on a shell wrapper at runtime.

## Important v1.0 note
This is the clean C# generation of the application. The architecture intentionally does not carry the old Bash/Python/Tkinter lifecycle or its ffmpeg artwork conversion chain forward.

<!-- Changelog: v1.0 - Documents Steam Deck single-file publishing, native workflow, configuration locations, and architecture. -->


Changelog: v1.2 - Final deployment layout is an application folder; runtime logs, cache, config, and artwork are stored beside the executable. Linux publishes must have the executable permission set before launch, and the same project supports a future win-x64 publish.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.

Changelog: v1.2.1 - Added a GitHub Actions workflow that builds a self-contained linux-x64 deployment, sets the executable bit, and packages the exact Steam Deck application folder for download.
