// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Idel Nigmatullin and GodForge contributors
// This file is part of GodForge (GFML). See LICENSE for details.

namespace NeoModLoader.constants;

/// <summary>
///     Branding constants of this mod loader. All user-visible names go through here.
/// </summary>
public static class Branding
{
    /// <summary>Full name of the loader (file name is Name + ".dll")</summary>
    public const string Name = "GodForge";

    /// <summary>Short name used in log prefixes</summary>
    public const string ShortName = "GFML";

    /// <summary>Human readable version</summary>
    public const string Version = "0.1.0-beta";

    /// <summary>Project author (shown in About and the startup log)</summary>
    public const string Author = "Idel Nigmatullin";

    /// <summary>License of the loader</summary>
    public const string License = "GPL-3.0-or-later";

    /// <summary>Official channel / homepage</summary>
    public const string Homepage = "https://t.me/GodForgeHub";

    /// <summary>Official source repository</summary>
    public const string Repository = "https://github.com/mrterminator2023-arch/GodForge";

    /// <summary>One-line identification string for logs</summary>
    public const string Signature = Name + " (" + ShortName + ") v" + Version + " by " + Author + " - " + License + " - " + Homepage;

    /// <summary>Root of embedded manifest resources (RootNamespace + ".resources")</summary>
    public const string ResourceRoot = "GodForge.resources";

    /// <summary>Embedded logo file name under resources/</summary>
    public const string LogoResource = "logo.png";

    /// <summary>Sprite path the logo is registered under</summary>
    public const string LogoSpritePath = "ui/icons/godforge";

    /// <summary>Name of the cache folder inside the native mods folder</summary>
    public const string CacheFolder = "GodForge";

    /// <summary>Text shown in the About window</summary>
    public const string AboutText =
        Name + " (" + ShortName + ") v" + Version + " - WorldBox Mod Loader for Android\n" +
        "BETA: expect bugs. Mods may break or crash the game.\n\n" +
        "Author: " + Author + "\nLicense: " + License + " (free software, source available)\n" +
        "Official channel: " + Homepage + "\nSource: " + Repository + "\n" +
        "Only builds from the official channel are genuine. The GodForge name and logo may not be used by forks.\n\n" +
        Name + " is built upon NeoModLoader / AndroidModLoader by WorldBoxOpenMods (MIT). " +
        "We learned a great deal from their code and are grateful to its authors.\n\n" +
        "See CREDITS.md and LICENSE-NeoModLoader shipped with " + Name + ".";

    /// <summary>Show the "PCInput" / "MouseMode" buttons of the PC input overlay in the top-left corner. Input handling works regardless.</summary>
    public const bool ShowPCInputOverlay = false;

    /// <summary>Decoration: small translucent logos drifting on the mods window background. Set false to disable.</summary>
    public const bool FloatingLogos = true;

    /// <summary>Number of drifting logos</summary>
    public const int FloatingLogoCount = 8;

    /// <summary>Logo size in UI units</summary>
    public const float FloatingLogoSize = 14f;

    /// <summary>Logo opacity (0..1)</summary>
    public const float FloatingLogoAlpha = 0.28f;

    /// <summary>Drift speed range, UI units per second</summary>
    public const float FloatingLogoSpeedMin = 8f;

    /// <summary>Drift speed range, UI units per second</summary>
    public const float FloatingLogoSpeedMax = 20f;
}
