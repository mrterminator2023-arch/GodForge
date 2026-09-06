using System.Reflection;
using NeoModLoader.constants;

namespace NeoModLoader.services;

/// <summary>
/// Optional compiler pack (Roslyn + deps + Assembly-CSharp-Publicized) living in <see cref="Paths.CompilerPackPath"/>.
/// It is only required for mods shipped as source (Code/*.cs); precompiled mods never trigger it.
/// </summary>
public static class CompilerPack
{
    private static bool _resolver_hooked;

    /// <summary>Whether the pack assemblies have been loaded into the process</summary>
    public static bool IsLoaded { get; private set; }

    /// <summary>Whether the pack folder contains Roslyn</summary>
    public static bool IsAvailable =>
        Directory.Exists(Paths.CompilerPackPath) &&
        File.Exists(Path.Combine(Paths.CompilerPackPath, "Microsoft.CodeAnalysis.CSharp.dll"));

    /// <summary>
    /// Load the pack if present. Returns false (and logs) when the pack is missing.
    /// </summary>
    public static bool EnsureLoaded()
    {
        if (IsLoaded) return true;
        if (!IsAvailable)
        {
            LogService.LogWarning($"Compiler pack not found at {Paths.CompilerPackPath}");
            return false;
        }

        if (!_resolver_hooked)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromPack;
            _resolver_hooked = true;
        }

        foreach (var dll in Directory.GetFiles(Paths.CompilerPackPath, "*.dll"))
        {
            if (Path.GetFileName(dll).StartsWith("Assembly-CSharp-Publicized")) continue;
            try
            {
                var asm = Assembly.LoadFrom(dll);
                LogService.LogInfo($"Compiler pack: loaded {asm.GetName().Name}");
            }
            catch (Exception e)
            {
                LogService.LogWarning($"Compiler pack: failed to load {dll}: {e.Message}");
            }
        }

        IsLoaded = true;
        return true;
    }

    private static Assembly ResolveFromPack(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        var path = Path.Combine(Paths.CompilerPackPath, name + ".dll");
        if (!File.Exists(path)) return null;
        try
        {
            return Assembly.LoadFrom(path);
        }
        catch
        {
            return null;
        }
    }
}
