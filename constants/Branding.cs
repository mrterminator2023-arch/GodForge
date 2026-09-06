namespace NeoModLoader.constants;

/// <summary>
///     Branding constants of this mod loader. All user-visible names go through here.
/// </summary>
public static class Branding
{
    /// <summary>Full name of the loader (file name is Name + ".dll")</summary>
    public const string Name = "WBML";

    /// <summary>Short name used in log prefixes</summary>
    public const string ShortName = "WBML";

    /// <summary>Human readable version</summary>
    public const string Version = "0.1.0";

    /// <summary>Root of embedded manifest resources (RootNamespace + ".resources")</summary>
    public const string ResourceRoot = "WBML.resources";

    /// <summary>Embedded logo file name under resources/</summary>
    public const string LogoResource = "logo.png";

    /// <summary>Sprite path the logo is registered under</summary>
    public const string LogoSpritePath = "ui/icons/wbml";

    /// <summary>Name of the cache folder inside the native mods folder</summary>
    public const string CacheFolder = "WBML";

    /// <summary>Text shown in the About window</summary>
    public const string AboutText =
        Name + " v" + Version + " - WorldBox Mod Loader for Android\n\n" +
        "WBML is built upon NeoModLoader / AndroidModLoader by WorldBoxOpenMods (MIT). " +
        "We learned a great deal from their code and are grateful to its authors.\n\n" +
        "See CREDITS.md and LICENSE-NeoModLoader shipped with WBML.";

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
