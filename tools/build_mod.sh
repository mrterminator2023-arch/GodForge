#!/bin/bash
# Build a WBML mod on the Mac into a precompiled <GUID>.dll placed next to its mod.json.
# Usage: tools/build_mod.sh <mod_dir> [Il2CppAssemblies dir]
#   Il2CppAssemblies dir defaults to $IL2CPP_DIR, then /tmp/il2cppasm_fixed, then
#   ~/IDK/wbandroid/backup/*/com.mkarpenko.worldbox/MelonLoader/Il2CppAssemblies, else repo copies.
set -euo pipefail
MOD_DIR=$(cd "${1:?usage: build_mod.sh <mod_dir> [il2cpp_dir]}" && pwd)
ROOT=$(cd "$(dirname "$0")/.." && pwd)
DOTNET=${DOTNET:-$HOME/.dotnet/dotnet}
[ -x "$DOTNET" ] || DOTNET=dotnet
IL2CPP_DIR=${2:-${IL2CPP_DIR:-}}
if [ -z "$IL2CPP_DIR" ]; then
  for d in "$HOME"/IDK/wbandroid/refs/Il2CppAssemblies_0.51.4 /tmp/il2cppasm_fixed "$HOME"/IDK/wbandroid/backup/*/com.mkarpenko.worldbox/MelonLoader/Il2CppAssemblies; do
    [ -f "$d/Assembly-CSharp.dll" ] && { IL2CPP_DIR=$d; break; }
  done
fi
[ -f "$MOD_DIR/mod.json" ] || { echo "no mod.json in $MOD_DIR" >&2; exit 1; }
GUID=$(python3 -c 'import json,sys;print(json.load(open(sys.argv[1]))["GUID"])' "$MOD_DIR/mod.json")
[ -f "$ROOT/bin/Release/net8.0/WBML.dll" ] || {
  echo "WBML.dll not built, building..." >&2
  "$DOTNET" build "$ROOT/WBML.csproj" -c Release -p:SkipNmlPostBuild=true -v q
}
echo "Building $GUID from $MOD_DIR (Il2CppDir=${IL2CPP_DIR:-<repo copies>})"
"$DOTNET" build "$ROOT/tools/mod_template/Mod.csproj" -c Release -v q -nologo \
  -p:ModDir="$MOD_DIR" -p:ModGuid="$GUID" -p:WbmlRoot="$ROOT" -p:Il2CppDir="$IL2CPP_DIR" \
  -p:BaseIntermediateOutputPath="$MOD_DIR/obj/" -p:MSBuildProjectExtensionsPath="$MOD_DIR/obj/"
rm -rf "$MOD_DIR/obj"
ls -la "$MOD_DIR/$GUID.dll"
