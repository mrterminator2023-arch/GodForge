<h1 align="center">WBML</h1>
<p align="center">WorldBox Mod Loader for Android (WorldBox 0.51.x, MelonLoader/LemonLoader bootstrap)</p>

WBML is a trimmed, rebranded fork of [AndroidModLoader](https://github.com/WorldBoxOpenMods/AndroidModLoader)
(the mobile branch of NeoModLoader by WorldBoxOpenMods, MIT). See `CREDITS.md` and `LICENSE-NeoModLoader`.

## What changed compared to NeoModLoader mobile
- Removed: auto-update, Steam Workshop, GitHub/Discord authentication, BepInEx bridge, gamebanana installer, mod hot-reload.
- Crash fixes for WorldBox 0.51.4 (SpriteAtlas fallback whitelist, Il2CPPBehaviour null guards, isolated init steps, locale resource prefix).
- Precompiled mods (`<GUID>.dll` inside the mod folder) are the primary path. Source mods (`Code/*.cs`) need the optional
  compiler pack in `Mods/WBML/Compiler/` (Roslyn + Assembly-CSharp-Publicized), which is no longer embedded into `WBML.dll`.

## Build
```
~/.dotnet/dotnet build WBML.csproj -c Release -p:SkipNmlPostBuild=true
# -> bin/Release/net8.0/WBML.dll
```
The entry type is `WBML.WorldBoxMod`; MelonLoader's WorldBox compatibility layer resolves `<file name>.WorldBoxMod`, so the file must stay `WBML.dll`.

## Install (Android)
- Copy `WBML.dll` to `MelonLoader/com.mkarpenko.worldbox/Mods/WBML.dll` (remove `NeoModLoader.dll` and `mods/NeoModLoader.AutoUpdate_mobile_memload.dll` if present).
- Put mods into `NMLMods/<ModName>/` with `mod.json` and `<GUID>.dll`. Building a mod: `tools/build_mod.sh <mod_dir>`; example: `examples/HelloMod`.
