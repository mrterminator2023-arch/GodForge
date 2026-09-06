#if !IL2CPP
extern alias unixsteamwork;
using unixsteamwork::Steamworks;
#endif
using System.IO.Compression;
using System.Net;
using System.Reflection;
using NeoModLoader.api;
using NeoModLoader.constants;
using NeoModLoader.General;
using NeoModLoader.services;
using NeoModLoader.ui;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WBML;
using UnityEngine;
namespace NeoModLoader.utils;

internal static class ModInfoUtils
{
    private static Queue<ModDeclare> link_request_mods = new();
    private static bool to_install_bepinex;

    private static Dictionary<string, ModCompilationCache> mod_compilation_caches;

    private static readonly Dictionary<string, long> mod_last_update_timestamps = new();

    public static void InitializeModCompileCache()
    {
        if (!File.Exists(Paths.ModCompileRecordPath)) File.WriteAllText(Paths.ModCompileRecordPath, "{}");
        var json = File.ReadAllText(Paths.ModCompileRecordPath);
        var json_settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented
        };
        try
        {
            mod_compilation_caches =
                JsonConvert.DeserializeObject<Dictionary<string, ModCompilationCache>>(json, json_settings) ??
                new Dictionary<string, ModCompilationCache>();
        }
        catch (Exception)
        {
            mod_compilation_caches = new Dictionary<string, ModCompilationCache>();
        }
        finally
        {
            mod_compilation_caches ??= new Dictionary<string, ModCompilationCache>();
        }

        if (File.Exists(Paths.ModsDisabledRecordPath))
        {
            var old_disabled = new List<string>(File.ReadAllLines(Paths.ModsDisabledRecordPath));
            foreach (var disabled in old_disabled)
                if (!mod_compilation_caches.ContainsKey(disabled))
                {
                    mod_compilation_caches[disabled] = new ModCompilationCache(disabled);
                    mod_compilation_caches[disabled].disabled = true;
                }
                else
                {
                    mod_compilation_caches[disabled].disabled = true;
                }

            File.Delete(Paths.ModsDisabledRecordPath);
        }
    }

    public static string TryToUnzipModZip(string pZipFile)
    {
        var extract_path = Path.Combine(Application.temporaryCachePath,
            Path.GetFileNameWithoutExtension(pZipFile));
        if (Directory.Exists(extract_path)) Directory.Delete(extract_path, true);

        try
        {
            ZipFile.ExtractToDirectory(pZipFile, extract_path);
        }
        catch (Exception e)
        {
            if (Directory.Exists(extract_path)) Directory.Delete(extract_path, true);

            LogService.LogError($"Error occurs when extracting {pZipFile}");
            LogService.LogError(e.Message);
            LogService.LogError(e.StackTrace);
            return "";
        }

        var mod_json_files = SystemUtils.SearchFileRecursive(extract_path,
            filename => filename == Paths.ModDeclarationFileName,
            dirname => true);
        if (mod_json_files.Count == 0)
        {
            Directory.Delete(extract_path, true);
            return "";
        }

        if (mod_json_files.Count > 1)
            LogService.LogWarning($"More than one mod.json file in {pZipFile}, only load the first one");
        var target_folder_name = Path.GetFileNameWithoutExtension(pZipFile);
        try
        {
            ModDeclare mod_declare = new(mod_json_files[0]);
            target_folder_name = mod_declare.UID;
        }
        catch (Exception e)
        {
            return "";
        }

        try
        {
            SystemUtils.CopyDirectory(Path.GetDirectoryName(mod_json_files[0]),
                Path.Combine(Paths.ModsPath, target_folder_name));
            return Path.Combine(Paths.ModsPath, target_folder_name);
        }
        catch (UnauthorizedAccessException)
        {
            ZipFile.ExtractToDirectory(pZipFile,
                Path.Combine(Paths.ModsPath, Path.GetFileNameWithoutExtension(pZipFile)));
        }
        finally
        {
            try
            {
                File.Delete(pZipFile);
                if (Directory.Exists(extract_path))
                    Directory.Delete(extract_path, true);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        return "";
    }

    public static void CheckModsFolder(string pFolderPath, HashSet<string> pFindModsIDs, List<ModDeclare> pModsToFill,
        bool pLogModJsonNotFound = true)
    {
        if (!Directory.Exists(pFolderPath)) return;
        var zipped_mods = new HashSet<string>(Directory.GetFiles(pFolderPath, "*.zip"))
            .Union(Directory.GetFiles(pFolderPath, "*.7z"))
            .Union(Directory.GetFiles(pFolderPath, "*.rar"))
            .Union(Directory.GetFiles(pFolderPath, "*.tar"))
            .Union(Directory.GetFiles(pFolderPath, "*.tar.gz"))
            .Union(Directory.GetFiles(pFolderPath, "*.mod"));
        foreach (var zipped_mod in zipped_mods) TryToUnzipModZip(zipped_mod);

        var mod_folders = Directory.GetDirectories(pFolderPath);
        foreach (var mod_folder in mod_folders)
        {
            ModDeclare mod = recogMod(mod_folder, pLogModJsonNotFound);
            if (mod != null)
            {
                if (pFindModsIDs.Contains(mod.UID))
                {
                    LogService.LogWarning($"Repeat Mod with {mod.UID}, Only load one of them");
                    continue;
                }

                pModsToFill.Add(mod);
                pFindModsIDs.Add(mod.UID);
            }
        }
    }

    public static List<ModDeclare> findAndPrepareMods()
    {
        HashSet<string> findModsIDs = new();
        var mods = new List<ModDeclare>();
        if (!NCMSHere())
        {
            CheckModsFolder(Paths.ModsPath, findModsIDs, mods);
        }

        CheckModsFolder(Paths.NativeModsPath, findModsIDs, mods, false);

        bool NCMSHere()
        {
            return false;
            return Directory.GetFiles(Paths.NativeModsPath, "NCMS*.dll").Length > 0;
        }

        List<ModDeclare> recognized_mods = new();
        HashSet<string> registered_mods = new();
        foreach (var mod in mods)
        {
            ModDeclare recognized_mod = EnsureRecognizedMod(mod);
            if (!registered_mods.Add(recognized_mod.UID))
            {
                continue;
            }

            if (WorldBoxMod.AllRecognizedMods[recognized_mod] != ModState.LOADED)
            {
                WorldBoxMod.AllRecognizedMods[recognized_mod] =
                    isModDisabled(recognized_mod.UID) ? ModState.DISABLED : ModState.FAILED;
            }

            recognized_mods.Add(recognized_mod);
        }

        return recognized_mods;
    }


    public static ModDeclare recogMod(string pModFolderPath, bool pLogModJsonNotFound = true)
    {
        var mod_config_path = Path.Combine(pModFolderPath, Paths.ModDeclarationFileName);
        if (!File.Exists(mod_config_path))
        {
            var possible_mod_config_path = SystemUtils.SearchFileRecursive(pModFolderPath,
                file_name =>
                    file_name ==
                    Paths.ModDeclarationFileName,
                _ => true);
            if (possible_mod_config_path.Count == 0)
            {
                if (pLogModJsonNotFound)
                    LogService.LogWarning($"No mod.json file for folder {pModFolderPath} in Mods");
                return null;
            }

            if (possible_mod_config_path.Count > 1)
                LogService.LogWarning(
                    $"More than one mod.json file in mod folder, only load the first one at '{possible_mod_config_path[0]}'");
            mod_config_path = possible_mod_config_path[0];
        }

        try
        {
            var mod = new ModDeclare(mod_config_path);
            return mod;
        }
        catch (Exception e)
        {
            LogService.LogError($"Error occurs when loading mod config file {mod_config_path}");
            LogService.LogError(e.Message);
            LogService.LogError(e.StackTrace);
            return null;
        }
    }


    public static bool TryGetRecognizedMod(string pModUID, out ModDeclare pModDeclare)
    {
        foreach (var recognized_mod in WorldBoxMod.AllRecognizedMods.Keys)
        {
            if (recognized_mod.UID != pModUID) continue;

            pModDeclare = recognized_mod;
            return true;
        }

        pModDeclare = null;
        return false;
    }

    public static ModDeclare EnsureRecognizedMod(ModDeclare pModDeclare)
    {
        if (TryGetRecognizedMod(pModDeclare.UID, out ModDeclare recognized_mod))
        {
            return recognized_mod;
        }

        WorldBoxMod.AllRecognizedMods[pModDeclare] = isModDisabled(pModDeclare.UID) ? ModState.DISABLED : ModState.FAILED;
        return pModDeclare;
    }

    public static bool TryFindMod(string pModUID, out ModDeclare pModDeclare)
    {
        if (TryGetRecognizedMod(pModUID, out pModDeclare))
        {
            return true;
        }

        if (TryFindModInFolder(Paths.ModsPath, pModUID, true, false, out pModDeclare))
        {
            return true;
        }

        if (TryFindModInFolder(Paths.NativeModsPath, pModUID, false, false, out pModDeclare))
        {
            return true;
        }

        pModDeclare = null;
        return false;
    }

    private static bool TryFindModInFolder(string pFolderPath, string pModUID, bool pLogModJsonNotFound,
        bool pSetWorkshopRepoUrl, out ModDeclare pModDeclare)
    {
        pModDeclare = null;
        if (!Directory.Exists(pFolderPath))
        {
            return false;
        }

        foreach (var mod_folder in Directory.GetDirectories(pFolderPath))
        {
            var mod = recogMod(mod_folder, pLogModJsonNotFound);
            if (mod == null || mod.UID != pModUID)
            {
                continue;
            }

            if (pSetWorkshopRepoUrl && string.IsNullOrEmpty(mod.RepoUrl))
            {
                mod.SetRepoUrlToWorkshopPage(Path.GetFileName(mod_folder));
            }

            pModDeclare = EnsureRecognizedMod(mod);
            return true;
        }

        return false;
    }

    public static bool isModDisabled(string pModUID)
    {
        return mod_compilation_caches.TryGetValue(pModUID, out ModCompilationCache cache) && cache.disabled;
    }

    public static void setModDisabled(string pModUID, bool pDisabled, bool pSave = true)
    {
        if (!mod_compilation_caches.TryGetValue(pModUID, out ModCompilationCache cache))
        {
            cache = new ModCompilationCache(pModUID);
            mod_compilation_caches[pModUID] = cache;
        }

        cache.disabled = pDisabled;
        if (pSave)
            SaveModRecords();
    }

    public static void SaveModRecords()
    {
        var json_settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver(),
            Formatting = Formatting.Indented
        };
        var json = JsonConvert.SerializeObject(mod_compilation_caches, json_settings);
        File.WriteAllText(Paths.ModCompileRecordPath, json);
    }

    public static void RecordMod(ModDeclare pModDeclare, List<string> pDependencies, List<string> pOptionalDependencies,
        bool pDisabled = false, bool pSave = true)
    {
        if (!mod_compilation_caches.TryGetValue(pModDeclare.UID, out ModCompilationCache cache))
        {
            cache = new ModCompilationCache(pModDeclare, pDependencies, pOptionalDependencies);
        }
        else
        {
            cache.dependencies = new List<string>(pDependencies);
            cache.optional_dependencies = new List<string>(pOptionalDependencies);
        }

        cache.disabled = pDisabled;
        cache.timestamp = getModNewestUpdateTimestamp(pModDeclare.FolderPath);

        mod_compilation_caches[pModDeclare.UID] = cache;
        if (pSave)
            SaveModRecords();
    }

    // ReSharper disable once InconsistentNaming
    public static bool doesModNeedRecompile(ModDeclare pModDeclare, List<string> pDependencies,
        List<string> pOptionalDependencies)
    {
        if (!mod_compilation_caches.TryGetValue(pModDeclare.UID, out ModCompilationCache cache)) return true;
        if (!File.Exists(Path.Combine(Paths.CompiledModsPath, pModDeclare.UID))) return true;
        var curr = new HashSet<string>(pDependencies);
        var last = new HashSet<string>(cache.dependencies);

        if (!curr.SetEquals(last)) return true;
        curr = new HashSet<string>(pOptionalDependencies);
        last = new HashSet<string>(cache.optional_dependencies);
        if (!curr.SetEquals(last)) return true;

        var last_compile_time = cache.timestamp;
        bool need_recompile = last_compile_time <
                              Others.confirmed_compile_time + getModNewestUpdateTimestamp(pModDeclare.FolderPath);
        if (need_recompile) return true;

        foreach (var depen in pDependencies)
        {
            need_recompile |= last_compile_time < Others.confirmed_compile_time + getModLastCompileTimestamp(depen);
            if (need_recompile) return true;
        }

        foreach (var depen in pOptionalDependencies)
        {
            need_recompile |= last_compile_time < Others.confirmed_compile_time + getModLastCompileTimestamp(depen);
            if (need_recompile) return true;
        }

        return false;
    }

    public static void clearModCompileTimestamp(string pModUUID, bool pSave = true)
    {
        if (!mod_compilation_caches.TryGetValue(pModUUID, out ModCompilationCache cache))
        {
            cache = new ModCompilationCache(pModUUID);
            cache.disabled = false;
            cache.timestamp = 0;
            mod_compilation_caches[pModUUID] = cache;
            return;
        }

        cache.timestamp = 0;
        if (pSave)
            SaveModRecords();
    }

    // ReSharper disable once InconsistentNaming
    private static long getModLastCompileTimestamp(string pModUID)
    {
        return mod_compilation_caches.TryGetValue(pModUID, out ModCompilationCache cache) ? cache.timestamp : 0;
    }

    private static long getModNewestUpdateTimestamp(string pModFolderPath)
    {
        var dir = new DirectoryInfo(pModFolderPath);
        if (mod_last_update_timestamps.ContainsKey(dir.FullName)) return mod_last_update_timestamps[dir.FullName];
        var files = SystemUtils.SearchFileRecursive(dir.FullName, (filename) => !filename.StartsWith("."),
            dirname => !dirname.StartsWith(".") &&
                       !Paths.CompileIgnoreSearchDirectories.Contains(dirname));
        var result = files.Select(filepath => new FileInfo(filepath))
            .Select(file_info =>
                Math.Max(file_info.CreationTimeUtc.Ticks, file_info.LastWriteTimeUtc.Ticks))
            .Prepend(Math.Max(dir.CreationTimeUtc.Ticks, dir.LastWriteTimeUtc.Ticks))
            .Prepend(InternalResourcesGetter.GetLastWriteTime())
            .Max();
        mod_last_update_timestamps[dir.FullName] = result;
        return result;
    }
}
