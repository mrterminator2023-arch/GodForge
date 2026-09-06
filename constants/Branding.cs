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
}
