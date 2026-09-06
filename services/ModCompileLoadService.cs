using System.IO.Compression;
using System.Reflection;
using System.Text;
using HarmonyLib;
using ModDeclaration;
using NCMS;

using NeoModLoader.api;
using NeoModLoader.constants;
using NeoModLoader.General;
using NeoModLoader.ncms_compatible_layer;
using NeoModLoader.utils;
using WorldBoxMod = WBML.WorldBoxMod;
using UnityEngine;
using UnityEngine.Networking;

namespace NeoModLoader.services;
using NeoModLoader.AndroidCompatibilityModule;
/// <summary>
/// Service of mod compiling and loading. 
/// </summary>
public static class ModCompileLoadService
{

    internal static void LoadLocales(object pModComponent, ModDeclare pModDeclare, bool pUpdateTexts = true,
        bool pLogLoadedFiles = false)
    {
        if (pModComponent is not ILocalizable localizable_mod)
            return;

        string locale_path = localizable_mod.GetLocaleFilesDirectory(pModDeclare);
        if (!Directory.Exists(locale_path)) return;

        char csv_separator = ',';
        if (pModComponent is ICsvSepCustomized sep_customized)
            csv_separator = sep_customized.GetCsvSeparator();

        var files = Directory.GetFiles(locale_path, "*", SearchOption.AllDirectories);
        foreach (var locale_file in files)
        {
            if (pLogLoadedFiles)
            {
                LogService.LogInfo(
                    $"Reload {locale_file} as {Path.GetFileNameWithoutExtension(locale_file)}");
            }

            try
            {
                if (locale_file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    LM.LoadLocale(Path.GetFileNameWithoutExtension(locale_file), locale_file);
                }
                else if (locale_file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    LM.LoadLocales(locale_file, csv_separator);
                }
            }
            catch (FormatException e)
            {
                LogService.LogWarning(e.Message);
            }
        }

        LM.ApplyLocale(pUpdateTexts);
    }
    /// <summary>
    /// Prepare references for mod nodes. Roslyn (the optional compiler pack) is only loaded when at least one
    /// mod in the list has no precompiled dll in its folder.
    /// </summary>
    /// <param name="pModNodes"></param>
    public static void prepareCompile(List<ModDependencyNode> pModNodes)
    {
        var source_mods = pModNodes.Where(n => !IsPrecompiled(n.mod_decl)).ToList();
        if (source_mods.Count == 0)
        {
            LogService.LogInfo("All mods are precompiled, compiler pack is not needed");
            return;
        }

        if (!CompilerPack.EnsureLoaded())
        {
            foreach (var node in source_mods)
                LogService.LogError(
                    $"Source mod {node.mod_decl.UID} requires the {Branding.Name} Compiler pack in {Paths.CompilerPackPath}");
            return;
        }

        ModCompiler.PrepareReferences(pModNodes);
    }

    /// <summary>
    /// Whether the mod folder contains a precompiled dll
    /// </summary>
    public static bool IsPrecompiled(ModDeclare pModDeclare)
    {
        return Directory.GetFiles(pModDeclare.FolderPath, "*.dll").Length > 0;
    }

    /// <summary>
    /// Prepare references for a single mod node
    /// </summary>
    /// <param name="pModNode"></param>
    public static void prepareCompileRuntime(ModDependencyNode pModNode)
    {
        if (IsPrecompiled(pModNode.mod_decl) || !CompilerPack.EnsureLoaded()) return;
        ModCompiler.PrepareRuntime(pModNode);
    }

    /// <summary>
    /// Public mod compiling method
    /// </summary>
    /// <param name="pModNode">The mod to compile</param>
    /// <param name="pForce">Wheather recompile when the mod does not need to recompile</param>
    /// <returns></returns>
    public static bool compileMod(ModDependencyNode pModNode, bool pForce = false)
    {
        string[] precompiled_dll_files = Directory.GetFiles(pModNode.mod_decl.FolderPath, "*.dll");
        if (precompiled_dll_files.Length > 0)
        {
            LogService.LogInfo(
                $"{pModNode.mod_decl.UID} detected as precompiled, compilation phase will be skipped on it!");
            pModNode.mod_decl.SetModType(ModTypeEnum.COMPILED_NEOMOD);

            string main_dll = precompiled_dll_files.FirstOrDefault(file =>
                                  Path.GetFileNameWithoutExtension(file) == pModNode.mod_decl.UID) ??
                              precompiled_dll_files[0];
            if (CompilerPack.IsLoaded) ModCompiler.RegisterPrecompiled(pModNode.mod_decl.UID, main_dll);
            return true;
        }

        if (!CompilerPack.EnsureLoaded())
        {
            pModNode.mod_decl.FailReason.AppendLine(
                $"Source mod requires the {Branding.Name} Compiler pack\nPut Roslyn dlls into {Paths.CompilerPackPath}\nor ship a precompiled {pModNode.mod_decl.UID}.dll");
            LogService.LogError(
                $"Source mod {pModNode.mod_decl.UID} requires the {Branding.Name} Compiler pack in {Paths.CompilerPackPath}");
            return false;
        }

        return ModCompiler.CompileNode(pModNode, pForce);
    }


    /// <summary>
    /// Load a list of mods
    /// </summary>
    /// <param name="mods_to_load"></param>
    public static void loadMods(List<ModDeclare> mods_to_load)
    {
        // It can be sure that all mods are compiled successfully.
        foreach (var mod in mods_to_load)
        {
            try
            {
                LoadMod(mod);
            }
            catch (ReflectionTypeLoadException e)
            {
                LogService.LogError(
                    $"Compiled mod {mod.UID} out of date, if it happens again after restarting game, please update, delete or unsubscribe it");
                LogService.LogException(e);

                string dll_path = Path.Combine(Paths.CompiledModsPath, $"{mod.UID}.dll");
                string pdb_path = Path.Combine(Paths.CompiledModsPath, $"{mod.UID}.pdb");
                try
                {
                    if (File.Exists(dll_path)) File.Delete(dll_path);

                    if (File.Exists(pdb_path)) File.Delete(pdb_path);
                }
                catch (Exception)
                {
                    // ignored
                }

                ModInfoUtils.clearModCompileTimestamp(mod.UID);
            }
        }
    }

    /// <summary>
    /// Load a single mod
    /// </summary>
    /// <param name="pMod"></param>
    public static void LoadMod(ModDeclare pMod)
    {
        Assembly[] mod_assemblies;
        switch (pMod.ModType)
        {
            case ModTypeEnum.NEOMOD:
                mod_assemblies = new[]
                {
                    Assembly.Load(
                        File.ReadAllBytes(Path.Combine(Paths.CompiledModsPath,
                            $"{pMod.UID}.dll")),
                        File.ReadAllBytes(Path.Combine(Paths.CompiledModsPath, $"{pMod.UID}.pdb"))
                    )
                };
                break;
            case ModTypeEnum.COMPILED_NEOMOD:
                var dll_files = Directory.GetFiles(pMod.FolderPath, "*.dll");
                List<string> pdb_files = Directory.GetFiles(pMod.FolderPath, "*.pdb").ToList();
                mod_assemblies = new Assembly[dll_files.Length];
                for (int i = 0; i < dll_files.Length; i++)
                {
                    string dll_file_name = Path.GetFileName(dll_files[i]).Replace(".dll", "");
                    int index = pdb_files.IndexOf(Path.Combine(pMod.FolderPath, $"{dll_file_name}.pdb"));
                    if (index != -1)
                    {
                        mod_assemblies[i] = Assembly.Load(
                            File.ReadAllBytes(dll_files[i]),
                            File.ReadAllBytes(pdb_files[index])
                        );
                        pdb_files.RemoveAt(index);
                    }
                    else
                    {
                        mod_assemblies[i] = Assembly.Load(File.ReadAllBytes(dll_files[i]));
                    }
                }

                break;
            case ModTypeEnum.BEPINEX:
            case ModTypeEnum.RESOURCE_PACK:
            default:
                throw new ArgumentException("Cannot load mod of type " + pMod.ModType + " with NML!");
        }
        bool all_success = true;
        foreach (var mod_assembly in mod_assemblies)
        {
            MelonLoader.RegisterTypeInIl2Cpp.RegisterAssembly(mod_assembly);
            GameObject mod_instance;
            bool any_loaded = false;
            foreach (var type in mod_assembly.GetTypes())
            {
                var mod_entry = Attribute.GetCustomAttribute(type, typeof(ModEntry));
                if (!type.IsSubclassOf(typeof(WrappedBehaviour)) ||
                    (type.GetInterface(nameof(IMod)) == null && mod_entry == null) || type.IsAbstract) continue;
                mod_instance = new GameObject(pMod.Name)
                {
                    transform =
                    {
                        parent = GameObject.Find("Services/ModLoader").transform
                    }
                };
                mod_instance.SetActive(false);

                if (mod_entry != null)
                {
                    pMod.IsNCMSMod = true;
                    Type ncmsGlobalObjectType = mod_assembly.GetType("Mod");
                    ncmsGlobalObjectType.GetField("Info")
                        ?.SetValue(null, new Info(NCMSCompatibleLayer.GenerateNCMSMod(pMod)));
                    ncmsGlobalObjectType.GetField("GameObject")?.SetValue(null, mod_instance);
                }

                IMod mod_interface = null;
                try
                {
                    object main_component;
                    if (type.GetInterface(nameof(IMod)) == null)
                    {
                        mod_interface = mod_instance.AddComponent<AttachedModComponent>();
                        main_component = mod_instance.AddComponent(type);
                    }
                    else
                    {
                        mod_interface = (IMod)mod_instance.AddComponent(type);
                        main_component = (WrappedBehaviour)mod_interface;
                    }
                    LoadLocales(main_component, pMod, false);

                    mod_interface.OnLoad(pMod, mod_instance);
                    mod_instance.SetActive(true);
                    WorldBoxMod.LoadedMods.Add(mod_instance.GetWrappedComponent<IMod>());
                    any_loaded = true;
                    break;
                }
                catch (Exception e)
                {
                    LogService.LogError(e.Message);
                    if (e.StackTrace != null) LogService.LogError(e.StackTrace);

                    mod_instance.SetActive(false);
                    LogService.LogError(
                        $"{pMod.Name} has been disabled due to an error. Please check the log for details.");

                    continue;
                }
            }
            if (!any_loaded)
            {
                all_success = false;
                LogService.LogError(
                    $"No valid mod component found in assembly {mod_assembly.FullName} for mod {pMod.UID}");
            }
        }
        if (all_success)
        {
                WorldBoxMod.AllRecognizedMods[pMod] = ModState.LOADED;
                ModDepenSolveService.MarkModLoaded(pMod);
        }
        else
        {
            pMod.FailReason.AppendLine("All mod assemblies failed to load.");
            ModInfoUtils.clearModCompileTimestamp(pMod.UID);
        }
    }

    /// <summary>
    /// Initializes a single mod if the mod implements IStagedLoad
    /// </summary>
    /// <param name="mod">The mod to init</param>
    public static bool TryInitMod(IMod mod)
    {
        if (mod is IStagedLoad staged_load_mod)
        {
            try
            {
                staged_load_mod.Init();
            }
            catch (Exception e)
            {
                LogService.LogError(e.Message);
                if (e.StackTrace != null) LogService.LogError(e.StackTrace);
                mod.GetGameObject().SetActive(false);
                LogService.LogError(
                    $"{mod.GetDeclaration().Name} has been disabled due to an init error. Please check the log for details.");
                return false;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Post initializes a single mod if the mod implements IStagedLoad
    /// </summary>
    /// <param name="mod">The mod to post-init</param>
    public static void PostInitMod(IMod mod)
    {
        if (mod is IStagedLoad staged_load_mod)
        {
            try
            {
                staged_load_mod.PostInit();
            }
            catch (Exception e)
            {
                LogService.LogError(e.Message);
                if (e.StackTrace != null) LogService.LogError(e.StackTrace);
                mod.GetGameObject().SetActive(false);
                LogService.LogError(
                    $"{mod.GetDeclaration().Name} has been disabled due to a post init error. Please check the log for details.");
            }
        }
    }

    /// <summary>
    /// Check whether a mod loaded with mod's UID
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    public static bool IsModLoaded(string uid)
    {
        foreach (var mod in WorldBoxMod.LoadedMods)
        {
            if (mod.GetDeclaration().UID == uid)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildRuntimeLoadErrorMessage(ModDeclare pModDeclare, string pReason)
    {
        return $"Failed to load mod {pModDeclare.Name}:\n{pReason.Trim()}";
    }

    private static void ShowRuntimeLoadError(ModDeclare pModDeclare, string pFallbackReason)
    {
        string reason = pModDeclare.FailReason.ToString().Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = pFallbackReason;
        }

        ErrorWindow.errorMessage = BuildRuntimeLoadErrorMessage(pModDeclare, reason);
        ScrollWindow.get("error_with_reason").clickShow();
    }

    private static bool TryCompileRuntimeNode(ModDependencyNode pModNode, bool pForce = false)
    {
        prepareCompileRuntime(pModNode);
        bool success = compileMod(pModNode, pForce);
        if (success)
        {
            return true;
        }

        WorldBoxMod.AllRecognizedMods[pModNode.mod_decl] = ModState.FAILED;
        return false;
    }

    private static bool TryLoadCompiledModAtRuntime(ModDeclare pModDeclare)
    {
        AssetLinker linker = new AssetLinker();
        ResourcesPatch.LoadResourceFromFolder(Path.Combine(pModDeclare.FolderPath, Paths.ModResourceFolderName),
            linker);
        ResourcesPatch.LoadResourceFromFolder(Path.Combine(pModDeclare.FolderPath,
            Paths.NCMSAdditionModResourceFolderName), linker);
        ResourcesPatch.LoadAssetBundlesFromFolder(Path.Combine(pModDeclare.FolderPath, Paths.ModAssetBundleFolderName));

        LoadMod(pModDeclare);
        linker.AddAssets();

        if (IsModLoaded(pModDeclare.UID))
        {
            return true;
        }

        WorldBoxMod.AllRecognizedMods[pModDeclare] = ModState.FAILED;
        return false;
    }

    /// <summary>
    /// Compile mod at runtime.
    /// </summary>
    /// <param name="pModDeclare">Info of to be compiled mod</param>
    /// <param name="pForce">Wheather recompile mod if the mod has been compiled</param>
    /// <returns></returns>
    public static bool TryCompileModAtRuntime(ModDeclare pModDeclare, bool pForce = false)
    {
        pModDeclare = ModInfoUtils.EnsureRecognizedMod(pModDeclare);

        ModDependencyNode node = ModDepenSolveService.EnsureNode(pModDeclare);
        bool success = TryCompileRuntimeNode(node, pForce);
        if (!success)
        {
            ShowRuntimeLoadError(pModDeclare,
                "Failed to compile mod. Check incompatible mods and dependencies, then try again.");
            return false;
        }

        ModInfoUtils.SaveModRecords();
        return true;
    }

    /// <summary>
    /// Compile and load mod at runtime
    /// </summary>
    /// <param name="mod_declare">Info of to be compiled mo</param>
    /// <returns></returns>
    public static bool TryCompileAndLoadModAtRuntime(ModDeclare mod_declare)
    {
        mod_declare = ModInfoUtils.EnsureRecognizedMod(mod_declare);
        bool actually_loaded = IsModLoaded(mod_declare.UID);

        if (actually_loaded) return false;

        
        ModEnablePlan plan = ModDepenSolveService.BuildRuntimeEnablePlan(mod_declare);
        if (plan.HasFailure)
        {
            ShowRuntimeLoadError(mod_declare, plan.FailureReason);
            return false;
        }

        foreach (ModDependencyNode node in plan.LoadOrder)
        {
            if (node.Loaded || IsModLoaded(node.mod_decl.UID))
            {
                ModDepenSolveService.MarkModLoaded(node.mod_decl);
                continue;
            }

            if (!TryCompileRuntimeNode(node))
            {
                ModDepenSolveService.RollbackEnablePlan(plan);
                mod_declare.FailReason.Clear();
                mod_declare.FailReason.Append(node.mod_decl.FailReason);
                WorldBoxMod.AllRecognizedMods[mod_declare] = ModState.FAILED;
                ShowRuntimeLoadError(node.mod_decl,
                    "Failed to compile mod. Check incompatible mods and dependencies, then try again.");
                return false;
            }
        }

        foreach (ModDependencyNode node in plan.LoadOrder)
        {
            if (node.Loaded || IsModLoaded(node.mod_decl.UID))
            {
                ModDepenSolveService.MarkModLoaded(node.mod_decl);
                continue;
            }

            if (!TryLoadCompiledModAtRuntime(node.mod_decl))
            {
                ModDepenSolveService.RollbackEnablePlan(plan);
                mod_declare.FailReason.Clear();
                mod_declare.FailReason.Append(node.mod_decl.FailReason);
                WorldBoxMod.AllRecognizedMods[mod_declare] = ModState.FAILED;
                ShowRuntimeLoadError(node.mod_decl, "Failed to load mod. Check the log for details.");
                return false;
            }
        }

        if (!plan.RequestedRoots.All(IsModLoaded))
        {
            ModDepenSolveService.RollbackEnablePlan(plan);
            ShowRuntimeLoadError(mod_declare, "Failed to load mod. Check the log for details.");
            return false;
        }

        ModDepenSolveService.CommitEnablePlan(plan);
        ModInfoUtils.SaveModRecords();
        return true;
    }

    public static bool TryEnableMod(ModDeclare pModDeclare)
    {
        pModDeclare = ModInfoUtils.EnsureRecognizedMod(pModDeclare);
        if (IsModLoaded(pModDeclare.UID))
        {
            ModEnablePlan plan = ModDepenSolveService.BuildRuntimeEnablePlan(pModDeclare);
            if (plan.HasFailure)
            {
                ShowRuntimeLoadError(pModDeclare, plan.FailureReason);
                return false;
            }

            ModDepenSolveService.CommitEnablePlan(plan);
            ModInfoUtils.SaveModRecords();
            return true;
        }

        return TryCompileAndLoadModAtRuntime(pModDeclare);
    }

    public static void DisableMod(ModDeclare pModDeclare)
    {
        ModDepenSolveService.SetModDesiredEnabled(pModDeclare, false);
    }
}
