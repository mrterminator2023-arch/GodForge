using System.IO.Compression;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using ModDeclaration;
using NCMS;

using NeoModLoader.api;
using NeoModLoader.constants;
using NeoModLoader.General;
using NeoModLoader.ncms_compatible_layer;
using NeoModLoader.utils;
using WorldBoxMod = GodForge.WorldBoxMod;
using UnityEngine;
using UnityEngine.Networking;

namespace NeoModLoader.services;
using NeoModLoader.AndroidCompatibilityModule;

/// <summary>
/// Roslyn-backed compiler for source mods. Everything that touches Microsoft.CodeAnalysis lives here.
/// This type is NEVER referenced directly from other code: <see cref="ModCompiler"/> instantiates it by name
/// via reflection after <see cref="CompilerPack.EnsureLoaded"/>, so no method reachable on the precompiled-only
/// path ever JIT-resolves a Microsoft.CodeAnalysis type (that used to throw TypeLoadException without the pack).
/// </summary>
internal sealed class RoslynBackend : ICompilerBackend
{
    private string[] _default_ref_path = null!;
    private readonly Dictionary<string, string> mod_inc_path = new();
    private readonly HashSet<string> _loaded_ref = new();

    private MetadataReference[] _default_ref = null!;
    private MetadataReference _publicized_assembly_ref = null!;
    private readonly Dictionary<string, MetadataReference> mod_ref = new();

    private bool compileMod(ModDeclare pModDecl, IEnumerable<MetadataReference> pDefaultInc,
        string[] pAddInc, Dictionary<string, MetadataReference> pModInc, out string pCompileErrors, bool pForce = false,
        bool pDisableOptionalDepen = false)
    {
        pCompileErrors = string.Empty;
        var available_optional_depens = pDisableOptionalDepen
            ? new List<string>()
            : pModDecl.OptionalDependencies.Where(pModInc.ContainsKey).ToList();
        var available_depens = pModDecl.Dependencies.Where(pModInc.ContainsKey).ToList();
        if (!pForce && !ModInfoUtils.doesModNeedRecompile(pModDecl, available_depens, available_optional_depens))
        {
            LoadAddInc();
            return true;
        }

        var preprocessor_symbols = new List<string>();

        List<MetadataReference> list = pDefaultInc.ToList();
        list.AddRange(pAddInc.Select(inc => MetadataReference.CreateFromFile(inc)));
        LoadAddInc();
        if (pModDecl.UsePublicizedAssembly && !Config.isAndroid)
        {
            list.Add(_publicized_assembly_ref);
        }

        foreach (var depen in available_depens)
        {
            list.Add(pModInc[depen]);

            if (pModInc[depen] != null) continue;
            LogService.LogError($"{pModDecl.UID}'s optional ref of {depen} instance is null");
            return false;
        }

        foreach (var option_depen in available_optional_depens)
        {
            list.Add(pModInc[option_depen]);
            preprocessor_symbols.Add(ModDependencyUtils.ParseDepenNameToPreprocessSymbol(option_depen));
            if (pModInc[option_depen] != null) continue;
            LogService.LogError($"{pModDecl.UID}'s optional ref of {option_depen} instance is null");
            return false;
        }

        var syntaxTrees = new List<SyntaxTree>();
        var code_files = SystemUtils.SearchFileRecursive(pModDecl.FolderPath,
            file_name =>
                file_name.EndsWith(".cs") && !file_name.StartsWith("."),
            dir_name => !dir_name.StartsWith(".") &&
                        !Paths.CompileIgnoreSearchDirectories.Contains(dir_name));
        var embeded_resources = new List<ResourceDescription>();

        bool is_ncms_mod = false;
        if (Others.IsIL2CPP)
        {
            preprocessor_symbols.Add("IL2CPP");
        }
        else
        {
           syntaxTrees.Add(CSharpSyntaxTree.ParseText("global using Il2CppSystem = System;"));
        }
        var parse_option = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: preprocessor_symbols);

        foreach (var code_file in code_files)
        {
            SourceText sourceText = SourceText.From(File.ReadAllText(code_file), Encoding.UTF8);
            SyntaxTree syntaxTree =
                CSharpSyntaxTree.ParseText(
                    sourceText,
                    parse_option,
                    code_file.Substring(pModDecl.FolderPath.Length + 1)
                );
            syntaxTrees.Add(syntaxTree);
            if (!is_ncms_mod)
            {
                is_ncms_mod = IsNCMSMod(syntaxTree);
            }
        }


        if (is_ncms_mod)
        {
            // Load Manifest Files
            string embeded_resource_folder = Path.Combine(pModDecl.FolderPath, Paths.NCMSModEmbededResourceFolderName);
            if (Directory.Exists(embeded_resource_folder))
            {
                var embeded_resource_files = Directory.GetFiles(
                    embeded_resource_folder, "*",
                    SearchOption.AllDirectories);
                foreach (var file in embeded_resource_files)
                {
                    var relative_path = file.Substring(embeded_resource_folder.Length + 1);
                    var resource_name =
                        $"{pModDecl.Name}.Resources.{relative_path.Replace('\\', '.').Replace('/', '.')}";
                    var resource_desc = new ResourceDescription(
                        resource_name,
                        () => File.OpenRead(file),
                        true
                    );
                    embeded_resources.Add(resource_desc);
                }
            }

            // Load Global Object
            SourceText global_object_sourceText = SourceText.From(NCMSCompatibleLayer.modGlobalObject, Encoding.UTF8);
            SyntaxTree global_object_syntaxTree =
                CSharpSyntaxTree.ParseText(
                    global_object_sourceText,
                    parse_option,
                    $"{pModDecl.Name}.GlobalObject.cs"
                );
            syntaxTrees.Add(global_object_syntaxTree);
        }

        pModDecl.IsNCMSMod = is_ncms_mod;

        void LoadAddInc()
        {
            foreach (var inc in pAddInc)
            {
                string file_name = Path.GetFileName(inc);
                if (file_name == "Assembly-CSharp.dll" && !Config.isAndroid)
                {
                    continue;
                }

                if (_loaded_ref.Contains(file_name)) continue;
                _loaded_ref.Add(file_name);
                try
                {
                    var loaded_inc = Assembly.LoadFrom(inc);
                    LogService.LogInfo($"Load {loaded_inc.FullName}");
                }
                catch (Exception e)
                {
                    LogService.LogWarning($"Failed to load Assembly {file_name} for mod {pModDecl.UID}");
                    LogService.LogWarning(e.Message);
                    LogService.LogWarning(e.StackTrace);
                }
            }
        }


        var identity = new AssemblyIdentity(
            pModDecl.UID, pModDecl.ParseVersion(), null
        );

        var compilation_options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: true, deterministic: true, assemblyIdentityComparer: AssemblyIdentityComparer.Default);
        var compilation = CSharpCompilation.Create($"{pModDecl.UID}", syntaxTrees, list, compilation_options);

        // Mods written for the PC loader hit Il2Cpp-only type mismatches (lambdas, managed Type/List, Object results).
        // Let the compiler point at them, rewrite exactly those expressions and try again.
        if (Others.IsIL2CPP)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                var errors = compilation.GetDiagnostics()
                                        .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                if (errors.Count == 0) break;

                List<SyntaxTree> rewritten = PcModRewriter.Rewrite(errors, syntaxTrees, parse_option, compilation, out int fixes);
                if (rewritten == null || fixes == 0) break;

                LogService.LogInfo($"PC-mod compatibility: adapted {fixes} expression(s) in {pModDecl.Name}");
                syntaxTrees = rewritten;
                compilation = CSharpCompilation.Create($"{pModDecl.UID}", syntaxTrees, list, compilation_options);
            }
        }

        using MemoryStream dllms = new MemoryStream();
        using MemoryStream pdbms = new MemoryStream();

        string dll_path = Path.Combine(Paths.CompiledModsPath, $"{pModDecl.UID}.dll");
        string pdb_path = Path.Combine(Paths.CompiledModsPath, $"{pModDecl.UID}.pdb");

        var result = compilation.Emit(dllms, pdbms,
            manifestResources: embeded_resources,
            options: new EmitOptions(
                debugInformationFormat: DebugInformationFormat
                    .PortablePdb,
                pdbFilePath: pdb_path
            )
        );

        if (!result.Success)
        {
            pCompileErrors = CollectCompileErrors(result.Diagnostics);
            return false;
        }

        using var dll_fs = new FileStream(dll_path, FileMode.Create, FileAccess.Write);
        dllms.Seek(0, SeekOrigin.Begin);
        dllms.WriteTo(dll_fs);

        using var pdb_fs = new FileStream(pdb_path, FileMode.Create, FileAccess.Write);
        pdbms.Seek(0, SeekOrigin.Begin);
        pdbms.WriteTo(pdb_fs);

        ModInfoUtils.RecordMod(pModDecl, available_depens, available_optional_depens, false, false);
        return true;
    }

    private static string CollectCompileErrors(IEnumerable<Diagnostic> pDiagnostics)
    {
        StringBuilder diags = new StringBuilder();
        foreach (var diagnostic in pDiagnostics)
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error) continue;
            diags.AppendLine(diagnostic.ToString());
        }

        return diags.ToString().TrimEnd();
    }

    private static void LogCompileFailure(string pModUid, string pCompileErrors)
    {
        if (string.IsNullOrWhiteSpace(pCompileErrors))
        {
            LogService.LogError($"Failed to compile mod {pModUid}");
            return;
        }

        LogService.LogError($"Failed to compile mod {pModUid}:\n{pCompileErrors}");
    }

    private static void LogCompileFailureWithOptionalDependencies(string pModUid, string pCompileErrors)
    {
        if (string.IsNullOrWhiteSpace(pCompileErrors))
        {
            LogService.LogWarning($"Failed to compile mod {pModUid} with optional dependencies, but succeeded after disabling them");
            return;
        }

        LogService.LogWarning(
            $"Failed to compile mod {pModUid} with optional dependencies, but succeeded after disabling them:\n{pCompileErrors}");
    }

    /// <summary>
    /// Prepare references for mod nodes
    /// </summary>
    public void PrepareReferences(List<ModDependencyNode> pModNodes)
    {
        foreach (var mod_node in pModNodes)
        {
            mod_inc_path[mod_node.mod_decl.UID] =
                Path.Combine(Paths.CompiledModsPath, $"{mod_node.mod_decl.UID}.dll");
        }

        EnsureDefaultReferences();
    }

    public bool IsPrepared => _default_ref != null;

    /// <summary>
    /// Build the default reference set (game, MelonLoader, Il2Cpp assemblies, GodForge itself). Idempotent.
    /// </summary>
    public void EnsureDefaultReferences()
    {
        if (_default_ref != null) return;
        var default_ref_path_list = new List<string>();
        default_ref_path_list.AddRange(Directory.GetFiles(Paths.NMLAssembliesPath, "*.dll"));
        default_ref_path_list.AddRange(Directory.GetFiles(Paths.CompilerPackPath, "*.dll")
            .Where(f => !Path.GetFileName(f).StartsWith("Assembly-CSharp-Publicized")));
        if (Config.isAndroid)
        {
            default_ref_path_list.AddRange(Directory.GetFiles(Paths.MelonAssemblies, "*.dll"));
            default_ref_path_list.AddRange(Directory.GetFiles(Paths.Il2CppAssemblies, "*.dll"));
        }
        default_ref_path_list.AddRange(Directory.GetFiles(Paths.ManagedPath, "*.dll"));
        default_ref_path_list.Add(Paths.NMLModPath);
        // Files can land on the device with a zero length (interrupted copy); Roslyn then fails the whole
        // compilation with CS0009 and every mod stops building, so drop them and say which ones.
        var unusable = default_ref_path_list.Where(f => new FileInfo(f).Length == 0).ToList();
        foreach (string file in unusable)
            LogService.LogWarning($"Reference {Path.GetFileName(file)} is empty on disk and will be ignored");
        default_ref_path_list.RemoveAll(f => unusable.Contains(f));

        _default_ref_path = default_ref_path_list.ToArray();

        _default_ref = new MetadataReference[_default_ref_path.Length];
        for (int i = 0; i < _default_ref_path.Length; i++)
        {
            try
            {
                _default_ref[i] = MetadataReference.CreateFromFile(_default_ref_path[i]);
                if (_default_ref[i] == null) throw new Exception("Ref created is null");
            }
            catch (Exception e)
            {
                LogService.LogError($"Error when load default reference {_default_ref_path[i]}: {e.Message}");
            }
        }
        if (File.Exists(Paths.PublicizedAssemblyPath))
            _publicized_assembly_ref = MetadataReference.CreateFromFile(Paths.PublicizedAssemblyPath);
        else
            LogService.LogWarning($"Assembly-CSharp-Publicized.dll not found in compiler pack ({Paths.CompilerPackPath}); UsePublicizedAssembly mods will not get it");
    }

    /// <summary>
    /// Prepare references for a single mod node
    /// </summary>

    /// <summary>
    /// Prepare references for a single mod node
    /// </summary>
    public void PrepareRuntime(ModDependencyNode pModNode)
    {
        mod_inc_path[pModNode.mod_decl.UID] =
            Path.Combine(Paths.CompiledModsPath, $"{pModNode.mod_decl.UID}.dll");
    }

    /// <summary>
    /// Public mod compiling method
    /// </summary>
    /// <param name="pModNode">The mod to compile</param>
    /// <param name="pForce">Wheather recompile when the mod does not need to recompile</param>

    /// <summary>
    /// Register a precompiled mod dll as a reference for mods compiled from source.
    /// </summary>
    public void RegisterPrecompiled(string pUid, string pMainDll)
    {
        mod_ref[pUid] = MetadataReference.CreateFromFile(pMainDll);
    }

    public bool CompileNode(ModDependencyNode pModNode, bool pForce = false)
    {
        EnsureDefaultReferences();
        bool compile_result;
        bool has_available_optional_depen = pModNode.mod_decl.OptionalDependencies.Any(mod_ref.ContainsKey);
        string compile_errors;
        compile_result =
            compileMod(pModNode.mod_decl, _default_ref,
                pModNode.GetAdditionReferences().ToArray(), mod_ref, out compile_errors, pForce
            );
        if (compile_result)
        {
            mod_ref[pModNode.mod_decl.UID] =
                MetadataReference.CreateFromFile(Path.Combine(Paths.CompiledModsPath,
                    $"{pModNode.mod_decl.UID}.dll"));
        }
        else if (has_available_optional_depen)
        {
            LogService.LogWarning(
                $"Cannot compile mod {pModNode.mod_decl.UID} with Optional Dependencies, try to disable them");
            string compile_errors_without_optional_depen;
            compile_result =
                compileMod(pModNode.mod_decl, _default_ref,
                    pModNode.GetAdditionReferences(false).ToArray(), mod_ref, out compile_errors_without_optional_depen,
                    pForce, true
                );
            if (compile_result)
            {
                mod_ref[pModNode.mod_decl.UID] =
                    MetadataReference.CreateFromFile(Path.Combine(Paths.CompiledModsPath,
                        $"{pModNode.mod_decl.UID}.dll"));
                LogCompileFailureWithOptionalDependencies(pModNode.mod_decl.UID, compile_errors);
            }
            else
            {
                LogCompileFailure(pModNode.mod_decl.UID, compile_errors_without_optional_depen);
            }
        }
        else
        {
            LogCompileFailure(pModNode.mod_decl.UID, compile_errors);
        }

        if (!compile_result)
        {
            mod_inc_path.Remove(pModNode.mod_decl.UID);
            pModNode.mod_decl.FailReason.AppendLine(
                "Compile Failed\n Check Log for details\n All mods compiled before it will be recompiled next time");
            File.WriteAllText(Paths.ModCompileRecordPath, "");
        }

        return compile_result;
    }

    private static bool IsNCMSMod(SyntaxTree syntaxTree)
    {
        var root = syntaxTree.GetCompilationUnitRoot();
        foreach (var classdecl in root.DescendantNodes())
        {
            if (classdecl is not ClassDeclarationSyntax classDeclarationSyntax) continue;
            if (classDeclarationSyntax.AttributeLists.Any(a =>
                    a.Attributes.Any(a => a.Name.ToString().Contains("ModEntry"))))
                return true;
        }

        return false;
    }
}
