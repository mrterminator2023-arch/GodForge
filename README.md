<h1 align="center">GodForge (GFML)</h1>
<p align="center">WorldBox Mod Loader for Android (WorldBox 0.51.x, MelonLoader/LemonLoader bootstrap). Short name: GFML.</p>

GodForge is a trimmed, rebranded fork of [AndroidModLoader](https://github.com/WorldBoxOpenMods/AndroidModLoader)
(the mobile branch of NeoModLoader by WorldBoxOpenMods, MIT). See `CREDITS.md` and `LICENSE-NeoModLoader`.

## Official sources
The only genuine GodForge builds are published on the official channel (see `Branding.Homepage`).
Every release comes with a SHA-256 checksum (`tools/release_checksums.sh`); verify the APK before installing.
Builds from anywhere else are unofficial and may be modified.

## License
- GodForge is **free software under GPL-3.0-or-later** (`LICENSE`). You may use, study, modify and redistribute it,
  but any derivative must be released under the same license with its source code available.
- Code inherited from NeoModLoader / AndroidModLoader stays under MIT (`LICENSE-NeoModLoader`).
- The **name "GodForge" / "GFML" and the logo are not covered by the license** (`NOTICE`). Forks must pick their own
  name and artwork and must not present themselves as official GodForge builds.

## What changed compared to NeoModLoader mobile
- Removed: auto-update, Steam Workshop, GitHub/Discord authentication, BepInEx bridge, gamebanana installer, mod hot-reload.
- Crash fixes for WorldBox 0.51.4 (SpriteAtlas fallback whitelist, Il2CPPBehaviour null guards, isolated init steps, locale resource prefix).
- Precompiled mods (`<GUID>.dll` inside the mod folder) are the primary path. Source mods (`Code/*.cs`) need the optional
  compiler pack in `Mods/GodForge/Compiler/` (Roslyn + Assembly-CSharp-Publicized), which is no longer embedded into `GodForge.dll`.

## Build
```
~/.dotnet/dotnet build GodForge.csproj -c Release -p:SkipNmlPostBuild=true
# -> bin/Release/net8.0/GodForge.dll
```
The entry type is `GodForge.WorldBoxMod`; MelonLoader's WorldBox compatibility layer resolves `<file name>.WorldBoxMod`, so the file must stay `GodForge.dll`.

## Install (Android)
- Copy `GodForge.dll` to `MelonLoader/com.mkarpenko.worldbox/Mods/GodForge.dll` (remove `NeoModLoader.dll` and `mods/NeoModLoader.AutoUpdate_mobile_memload.dll` if present).
- Put mods into `NMLMods/<ModName>/` with `mod.json` and `<GUID>.dll`. Building a mod: `tools/build_mod.sh <mod_dir>`; example: `examples/HelloMod`.

## Target game version

**WorldBox 0.51.2 (Unity 2022.3.60f1) — the only supported version.** Do not build against 0.51.4.
Game sources: `~/IDK/wbandroid/orig_0512/`. Build the APK with `ORIG_DIR=orig_0512 /usr/bin/python3 patch.py`.
Export map: `mapping/real2obf_0.51.2.json`.
