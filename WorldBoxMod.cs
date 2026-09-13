using System.Reflection;
using NeoModLoader;
using HarmonyLib;
using NeoModLoader.AndroidCompatibilityModule;
using NeoModLoader.api;
using NeoModLoader.constants;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using NeoModLoader.ncms_compatible_layer;
using NeoModLoader.services;
using NeoModLoader.ui;
using NeoModLoader.utils;
using UnityEngine;
using Il2CppInterop.Runtime.Injection;
namespace GodForge;
/// <summary>
/// Main class
/// </summary>
[MelonLoader.RegisterTypeInIl2Cpp]
public class WorldBoxMod : BaseBehaviour
{
    public WorldBoxMod(IntPtr ptr) : base(ptr)
    {
    }
    public WorldBoxMod() : base(ClassInjector.DerivedConstructorPointer<WorldBoxMod>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }
    /// <summary>
    /// All successfully loaded mods.
    /// </summary>
    public static List<IMod> LoadedMods = new();

    /// <summary>
    /// Tries to get a loaded mod by declaration UID.
    /// </summary>
    /// <param name="pModDeclare">The target mod declaration.</param>
    /// <param name="pLoadedMod">The loaded mod instance when found.</param>
    /// <returns><see langword="true"/> when the mod is loaded; otherwise <see langword="false"/>.</returns>
    public static bool TryGetLoadedMod(ModDeclare pModDeclare, out IMod pLoadedMod)
    {
        if (pModDeclare != null)
        {
            foreach (var mod in LoadedMods)
            {
                if (mod.GetDeclaration().UID == pModDeclare.UID)
                {
                    pLoadedMod = mod;
                    return true;
                }
            }
        }

        pLoadedMod = null;
        return false;
    }

    internal static Dictionary<ModDeclare, ModState> AllRecognizedMods = new();
    internal static Transform Transform;
    internal static Transform InactiveTransform;
    internal static Assembly NeoModLoaderAssembly = Assembly.GetExecutingAssembly();
    private bool initialized = false;
    private bool initialized_successfully = false;

    private static void UnityExplorerFix() {
        HarmonyLib.Harmony harmony = new HarmonyLib.Harmony(Others.harmony_id);
        MethodInfo original = AccessTools.Method(typeof(Assembly), nameof(Assembly.LoadFrom), new[] { typeof(string) });
        MethodInfo standin = AccessTools.Method(typeof(WorldBoxMod), nameof(LoadFrom));
        ReversePatcher reversePatcher = harmony.CreateReversePatcher(original, new HarmonyMethod(standin));

        reversePatcher.Patch();
    }

    private static Assembly LoadFrom(string path) => Assembly.LoadFrom(path);
    private void Start()
    {
        Others.unity_player_enabled = true;
        Transform = transform;
        InactiveTransform = new GameObject("Inactive").transform;
        InactiveTransform.SetParent(Transform);
        InactiveTransform.gameObject.SetActive(false);
        if (Config.isAndroid)
        {
            GameObject services = GameObject.Find("Services");
            GameObject modloader = new GameObject("ModLoader");
            modloader.transform.parent = services.transform;
        }
        LogService.Init();
        if (ReflectionHelper.IsAssemblyLoaded("0Harmony") && !Config.isAndroid) {
            UnityExplorerFix();
        }
        fileSystemInitialize();
        LogService.LogInfo($"{Branding.Signature} (commit {InternalResourcesGetter.GetCommit()})");
    }
    private void Update()
    {
        if (!Config.game_loaded) return;
        // A session that reaches gameplay and keeps running is a healthy one; that clears the crash record.
        CrashGuard.Tick(UnityEngine.Time.unscaledDeltaTime);
        if (initialized_successfully)
        {
            TabManager._checkNewTabs();
        }
        
        if (initialized)
        {
            return;
        }

        initialized = true;
        
        HarmonyUtils._init();
        Harmony.CreateAndPatchAll(typeof(LM), Others.harmony_id);
        Harmony.CreateAndPatchAll(typeof(ResourcesPatch), Others.harmony_id);
     
        if (!SmoothLoader.isLoading()) SmoothLoader.prepare();
        // Each init step is isolated so that one failure (e.g. stripped ICall) does not skip the rest.
        AddSafeStep(ResourcesPatch.Initialize, "Initialize Resources");
        AddSafeStep(() => { LoadLocales(); LM.ApplyLocale(); }, "Load Locales");
        AddSafeStep(TabManager._init, "Initialize Tabs");
        AddSafeStep(WindowCreator.init, "Initialize Windows");
        AddSafeStep(WrappedPowersTab._init, "Initialize Powers Tab");
        AddSafeStep(NCMSCompatibleLayer.PreInit, "NCMS PreInit");
        AddSafeStep(ModInfoUtils.InitializeModCompileCache, "Initialize Mod Cache");
        AddSafeStep(AndroidHelper.Init, "Initialize Android Helper");
        ModEnablePlan startup_enable_plan = null;
        List<ModDependencyNode> mod_nodes = new();
        SmoothLoaderHelper.add(() =>
        {
            // Records this modded launch; two launches that never reached gameplay mean mods are crashing it.
            CrashGuard.BeginSession();
            ModInfoUtils.findAndPrepareMods();
            ModDepenSolveService.InitializeGraph(AllRecognizedMods.Keys);
            startup_enable_plan = ModDepenSolveService.BuildStartupEnablePlan();
            mod_nodes.AddRange(startup_enable_plan.LoadOrder);

            ModCompileLoadService.prepareCompile(mod_nodes);
        }, "Load Mods Info And Prepare Mods");
        SmoothLoaderHelper.add(() =>
        {
            var mods_to_load = new List<ModDeclare>();
            foreach (var mod in mod_nodes)
            {
                SmoothLoaderHelper.add(() =>
                {
                    if (ModCompileLoadService.compileMod(mod))
                    {
                        mods_to_load.Add(mod.mod_decl);
                    }
                    else
                    {
                        LogService.LogError($"Failed to compile mod {mod.mod_decl.Name}");
                    }
                }, "Compile Mod " + mod.mod_decl.Name);
            }
            // Everything is compiled by now; free the reference images Roslyn prefetched (they are rebuilt on
            // demand if a mod is compiled later at runtime).
            SmoothLoaderHelper.add(ModCompiler.ReleaseReferences, "Release Compiler References");
            AssetLinker Linker = new();
            foreach (var mod in mod_nodes)
            {
                SmoothLoaderHelper.add(() =>
                {
                    if (mods_to_load.Contains(mod.mod_decl))
                    {
                        ResourcesPatch.LoadResourceFromFolder(Path.Combine(mod.mod_decl.FolderPath,
                            Paths.ModResourceFolderName), Linker);
                        ResourcesPatch.LoadResourceFromFolder(Path.Combine(mod.mod_decl.FolderPath,
                            Paths.NCMSAdditionModResourceFolderName), Linker);
                        ResourcesPatch.LoadAssetBundlesFromFolder(Path.Combine(mod.mod_decl.FolderPath,
                            Paths.ModAssetBundleFolderName));
                    }
                }, "Load Resources From Mod " + mod.mod_decl.Name);
            }

            SmoothLoaderHelper.add(() =>
            {
                ModCompileLoadService.loadMods(mods_to_load);
                Linker.AddAssets();
                if (startup_enable_plan != null)
                {
                    if (startup_enable_plan.RequestedRoots.All(ModCompileLoadService.IsModLoaded))
                    {
                        ModDepenSolveService.CommitEnablePlan(startup_enable_plan);
                    }
                    else
                    {
                        ModDepenSolveService.RollbackEnablePlan(startup_enable_plan);
                    }
                }
                ModInfoUtils.SaveModRecords();
                NCMSCompatibleLayer.Init();
                var successfulInit = new Dictionary<IMod, bool>();
                foreach (IMod mod in LoadedMods.Where(mod => mod is IStagedLoad))
                {
                    SmoothLoaderHelper.add(() =>
                    {
                        successfulInit.Add(mod, ModCompileLoadService.TryInitMod(mod));
                    }, "Init Mod " + mod.GetDeclaration().Name);
                }
                foreach (IMod mod in LoadedMods.Where(mod => mod is IStagedLoad))
                {
                    SmoothLoaderHelper.add(() =>
                    {
                        if (successfulInit.ContainsKey(mod) && successfulInit[mod])
                        {
                            ModCompileLoadService.PostInitMod(mod);
                        }
                    }, "Post-Init Mod " + mod.GetDeclaration().Name);
                }
            }, "Load Mods");

            SmoothLoaderHelper.add(() =>
            {
                UIManager.init();


                LM.ApplyLocale();
                initialized_successfully = true;
            }, Branding.Name + " Post Initialize");
        }, "Compile Mods And Load resources");
    }
    
    private static void AddSafeStep(Action pAction, string pId)
    {
        SmoothLoaderHelper.add(() =>
        {
            try
            {
                pAction();
            }
            catch (Exception e)
            {
                // Write the reason into our own log: Debug.LogException goes to Unity's logcat sink, which is
                // not readable on this device, so the failure would otherwise be invisible.
                LogService.LogError($"Step '{pId}' failed: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }, pId);
    }

    private void LoadLocales()
    {
        string[] resources = NeoModLoaderAssembly.GetManifestResourceNames();
        string locale_path = InternalResourcesGetter.Resource + ".locales.";
        foreach (string resource_path in resources)
        {
            if (!resource_path.StartsWith(locale_path)) continue;

            LM.LoadLocale(resource_path.Replace(locale_path, "").Replace(".json", ""),
                NeoModLoaderAssembly.GetManifestResourceStream(resource_path));
        }
    }

    private void fileSystemInitialize()
    {
        if (!Directory.Exists(Paths.ModsPath))
        {
            Directory.CreateDirectory(Paths.ModsPath);
            LogService.LogInfo($"Create Mods folder at {Paths.ModsPath}");
        }
        if (!Directory.Exists(Paths.UserModsPath))
        {
            Directory.CreateDirectory(Paths.UserModsPath);
            LogService.LogInfo($"Create user Mods folder at {Paths.UserModsPath}");
        }

        if (!Directory.Exists(Paths.CompiledModsPath))
        {
            Directory.CreateDirectory(Paths.CompiledModsPath);
            LogService.LogInfo($"Create CompiledMods folder at {Paths.CompiledModsPath}");
        }

        if (!Directory.Exists(Paths.ModsConfigPath))
        {
            Directory.CreateDirectory(Paths.ModsConfigPath);
            LogService.LogInfo($"Create mods_config folder at {Paths.ModsConfigPath}");
        }

        if (!File.Exists(Paths.ModCompileRecordPath))
        {
            File.Create(Paths.ModCompileRecordPath).Close();
            LogService.LogInfo($"Create mod_compile_records.json at {Paths.ModCompileRecordPath}");
        }
        void extractAssemblies()
        {
            var resources = NeoModLoaderAssembly.GetManifestResourceNames();
            foreach (var resource in resources)
            {
                if (resource.EndsWith(".dll"))
                {
                    if (resource.Contains("Assembly-CSharp-Publicized")) continue;
                    var file_name = resource.Replace(InternalResourcesGetter.Resource + ".assemblies.", "");
                    var file_path = Path.Combine(Paths.NMLAssembliesPath, file_name).Replace("-renamed", "");

                    using var stream = NeoModLoaderAssembly.GetManifestResourceStream(resource);
                    using var file = new FileStream(file_path, FileMode.Create, FileAccess.Write);
                    stream.CopyTo(file);
                    // LogService.LogInfo($"Extract {file_name} to {file_path}");
                }
            }
        }

        if (!Directory.Exists(Paths.NMLAssembliesPath))
        {
            Directory.CreateDirectory(Paths.NMLAssembliesPath);
            LogService.LogInfo($"Create NMLAssemblies folder at {Paths.NMLAssembliesPath}");
            extractAssemblies();
        }
        else
        {
            var modupdate_time = new FileInfo(Paths.NMLModPath).LastWriteTime;
            var assemblyupdate_time = new DirectoryInfo(Paths.NMLAssembliesPath).CreationTime;
            if (modupdate_time > assemblyupdate_time)
            {
                LogService.LogInfo($"{Branding.Name}.dll is newer than assemblies in NMLAssemblies folder, " +
                                   $"re-extract assemblies from {Branding.Name}.dll");
                Debug.Log(Paths.NMLAssembliesPath);
                Directory.Delete(Paths.NMLAssembliesPath, true);
                Directory.CreateDirectory(Paths.NMLAssembliesPath);
                LogService.LogInfo($"Create new NMLAssemblies folder at {Paths.NMLAssembliesPath}");
                extractAssemblies();
            }
        }
        foreach (var file_full_path in Directory.GetFiles(Paths.NMLAssembliesPath, "*.dll"))
        {
            try
            {
                LoadFrom(file_full_path);
            }
            catch (BadImageFormatException)
            {
                LogService.LogError($"" +
                                    $"BadImageFormatException: " +
                                    $"The file {file_full_path} is not a valid assembly.");
            }
            catch (Exception e)
            {
                LogService.LogError($"Exception: " +
                                    $"Failed to load assembly {file_full_path}.");
                LogService.LogError(e.Message);
                LogService.LogError(e.StackTrace);
            }
        }

        File.WriteAllText(Paths.NMLCommitPath, InternalResourcesGetter.GetCommit());
    }
}
