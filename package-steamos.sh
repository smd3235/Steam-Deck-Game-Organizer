#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
PUBLISH="$ROOT/publish/linux-x64"
PACKAGE_ROOT="$ROOT/Steam Deck Game Organizer"
ZIP="$ROOT/steam_deck_game_organizer_v1.2.zip"

"$ROOT/publish-steamos.sh"
rm -rf "$PACKAGE_ROOT"
mkdir -p "$PACKAGE_ROOT"
cp -a "$PUBLISH"/. "$PACKAGE_ROOT"/
mkdir -p "$PACKAGE_ROOT/logs" "$PACKAGE_ROOT/cache/artwork" "$PACKAGE_ROOT/config"
chmod +x "$PACKAGE_ROOT/SteamDeckGameOrganizer"
rm -f "$ZIP"
(cd "$ROOT" && zip -qr "$ZIP" "Steam Deck Game Organizer")
printf 'Package: %s\n' "$ZIP"
printf 'Executable: %s\n' "$PACKAGE_ROOT/SteamDeckGameOrganizer"
# Changelog: v1.2 - Adds deterministic Steam Deck packaging with the fixed extracted folder name and executable permission.
