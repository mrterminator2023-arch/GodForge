using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NeoModLoader.constants;
using NeoModLoader.services;

namespace NeoModLoader.utils;

/// <summary>
///     Unpacks mods dropped into a mods folder as .zip archives, so that installing a mod is
///     "put the file here" instead of "unpack it into a folder with the right name".
/// </summary>
public static class ModArchiveInstaller
{
    /// <summary>
    ///     Unpacks every *.zip found directly in <paramref name="pModsFolder" /> into a folder of the same name
    ///     and removes the archive afterwards. Archives that fail to unpack are left alone and reported.
    /// </summary>
    public static void InstallArchives(string pModsFolder)
    {
        if (string.IsNullOrEmpty(pModsFolder) || !Directory.Exists(pModsFolder)) return;

        string[] archives;
        try
        {
            archives = Directory.GetFiles(pModsFolder, "*.zip", SearchOption.TopDirectoryOnly);
        }
        catch (Exception e)
        {
            LogService.LogWarning($"Cannot list archives in {pModsFolder}: {e.Message}");
            return;
        }

        foreach (string archive in archives)
        {
            try
            {
                Install(archive, pModsFolder);
            }
            catch (Exception e)
            {
                LogService.LogWarning($"Cannot install mod archive {Path.GetFileName(archive)}: {e.Message}");
            }
        }
    }

    private static void Install(string pArchive, string pModsFolder)
    {
        string name = Path.GetFileNameWithoutExtension(pArchive);
        string target = Path.Combine(pModsFolder, name);
        if (Directory.Exists(target))
        {
            LogService.LogWarning($"Mod folder {name} already exists, leaving {Path.GetFileName(pArchive)} alone");
            return;
        }

        string staging = target + ".installing";
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        ZipFile.ExtractToDirectory(pArchive, staging);

        // Archives usually carry a single top-level folder ("MyMod/mod.json"); use it directly so we
        // do not end up with MyMod/MyMod/mod.json.
        string root = staging;
        if (!File.Exists(Path.Combine(root, Paths.ModDeclarationFileName)))
        {
            string[] dirs = Directory.GetDirectories(root);
            if (dirs.Length == 1 && !Directory.GetFiles(root).Any() &&
                File.Exists(Path.Combine(dirs[0], Paths.ModDeclarationFileName)))
                root = dirs[0];
        }

        if (!File.Exists(Path.Combine(root, Paths.ModDeclarationFileName)))
        {
            Directory.Delete(staging, true);
            LogService.LogWarning($"{Path.GetFileName(pArchive)} has no {Paths.ModDeclarationFileName}, not a mod archive");
            return;
        }

        Directory.Move(root, target);
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        File.Delete(pArchive);
        LogService.LogInfo($"Installed mod {name} from {Path.GetFileName(pArchive)}");
    }
}
