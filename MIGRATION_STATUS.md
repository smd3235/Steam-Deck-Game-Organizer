# Steam Deck Game Organizer v1.0 migration status

## Native C# / Avalonia foundation
- Single application process.
- SteamOS Linux x64 self-contained single-file publish configuration.
- Steam search, AppID/detail enrichment, genres, tags, player modes, developer/publisher, release date.
- Conservative title matching with descriptor-stripping search variants.
- Category suggestion with confidence/evidence.
- One-page Review workspace with independent list/details ScrollViewers.
- Native ComboBox dropdown scrolling; no global Tk mouse-wheel router.
- Direct Steam artwork download, validation, self-healing cache, and Avalonia decode.
- No ffmpeg/ImageMagick/Pillow artwork dependency.
- Settings persistence and New Files Only fingerprint state.
- Safe stop/shutdown handling.
- Preview organization and last-run Undo.

## Deliberate v1.0 rules
- Early Access and language information are not visual metadata.
- Original archive name/path remains separate from cleaned title and verified Steam title.
- Unsorted remains available and is placed last in the category list.
- User decisions remain review-local; Steam metadata is not silently treated as proof of a category when evidence is weak.

## Follow-up parity work
The old v6 generation had additional maintenance/transaction paths that should be restored only after this native foundation is validated on the real Steam Deck: complete multi-version Archive handling, password-removal workflows, duplicate-content auditing, final inventory generation, organizer shortcut lifecycle, and any legacy cleanup operations still required by the user's real library.

<!-- Changelog: v1.0 - Records the clean C# migration baseline, preserved behavior, deliberate exclusions, and remaining parity work. -->


Changelog: v1.2 - Final deployment layout is an application folder; runtime logs, cache, config, and artwork are stored beside the executable. Linux publishes must have the executable permission set before launch, and the same project supports a future win-x64 publish.

Build note for this environment: the source package was updated for the v1.2 multi-file deployment, but this build environment does not have the .NET SDK installed and cannot download it. Therefore no compiled SteamDeckGameOrganizer executable is claimed from this environment. On a machine with .NET 8 SDK, publish-steamos.sh performs the Release publish and chmod +x step.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
