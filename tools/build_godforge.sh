#!/bin/bash
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Idel Nigmatullin and GodForge contributors
# Build GodForge.dll, assemble the optional compiler pack and the HelloMod example into out/.
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
DOTNET=${DOTNET:-$HOME/.dotnet/dotnet}; [ -x "$DOTNET" ] || DOTNET=dotnet
OUT=$ROOT/out
"$DOTNET" build "$ROOT/GodForge.csproj" -c Release -p:SkipNmlPostBuild=true -v q -nologo
rm -rf "$OUT"; mkdir -p "$OUT/Compiler" "$OUT/NMLMods/HelloMod"
cp "$ROOT/bin/Release/net8.0/GodForge.dll" "$OUT/"
# Compiler pack: only needed for mods shipped as source (Code/*.cs without a dll). Goes to <game>/mods/GodForge/Compiler/
for f in Microsoft.CodeAnalysis.dll Microsoft.CodeAnalysis.CSharp.dll Assembly-CSharp-Publicized.dll \
         System.Buffers.dll System.Collections.Immutable.dll System.Diagnostics.StackTrace.dll \
         System.Globalization.Extensions.dll System.Memory.dll System.Numerics.dll System.Numerics.Vectors.dll \
         System.Reflection.Metadata.dll System.Runtime.dll System.Runtime.CompilerServices.Unsafe.dll \
         System.Text.Encoding.CodePages.dll System.Threading.Tasks.Extensions.dll; do
  cp "$ROOT/resources/assemblies/$f" "$OUT/Compiler/"
done
"$ROOT/tools/build_mod.sh" "$ROOT/examples/HelloMod" >/dev/null
cp "$ROOT/examples/HelloMod/mod.json" "$ROOT/examples/HelloMod/icon.png" "$ROOT/examples/HelloMod/godforge.hellomod.dll" "$OUT/NMLMods/HelloMod/"
cp "$ROOT/LICENSE" "$ROOT/LICENSE-NeoModLoader" "$ROOT/CREDITS.md" "$OUT/"
du -sh "$OUT"/* "$OUT/Compiler" "$OUT/NMLMods/HelloMod"
