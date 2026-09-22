#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
OUT="$ROOT/publish/linux-x64"
rm -rf "$OUT"
dotnet restore "$ROOT/SteamDeckGameOrganizer.csproj"
dotnet publish "$ROOT/SteamDeckGameOrganizer.csproj" -c Release -r linux-x64 --self-contained true -o "$OUT"
chmod +x "$OUT/SteamDeckGameOrganizer"
printf 'Published SteamOS build: %s\n' "$OUT/SteamDeckGameOrganizer"

# Changelog: v1.2 - Publishes a self-contained Linux x64 application folder and explicitly marks the executable runnable.

Changelog: v1.2 - Corrected release packaging convention so the extracted application directory is named "Steam Deck Game Organizer" and the version appears only in the ZIP filename.
