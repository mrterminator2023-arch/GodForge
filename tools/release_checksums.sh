#!/bin/bash
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Idel Nigmatullin and GodForge contributors
# Prints SHA-256 of release files for publishing alongside the release.
# Usage: tools/release_checksums.sh [file...]   (default: out/worldbox_modded.apk out/GodForge.dll)
set -e
cd "$(dirname "$0")/.."
files=("$@"); [ ${#files[@]} -eq 0 ] && files=(out/worldbox_modded.apk out/GodForge.dll)
ver=$(grep -o 'Version = "[^"]*"' constants/Branding.cs | cut -d'"' -f2)
echo "GodForge v$ver - SHA-256 ($(date -u +%Y-%m-%d))"
for f in "${files[@]}"; do
  [ -f "$f" ] || { echo "skip: $f (not found)"; continue; }
  shasum -a 256 "$f" | awk -v n="$(basename "$f")" '{print $1"  "n}'
done
echo; echo "Verify on phone: sha256sum <file>  (Termux) or in a file manager with hash support."
