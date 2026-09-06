using NeoModLoader.utils;

namespace NeoModLoader.services;

/// <summary>
/// Compiler backend contract. Deliberately free of any Microsoft.CodeAnalysis type in its signature so that
/// every type on the precompiled-only path can be loaded without the compiler pack.
/// </summary>
internal interface ICompilerBackend
{
    bool IsPrepared { get; }
    void PrepareReferences(List<ModDependencyNode> pModNodes);
    void EnsureDefaultReferences();
    void PrepareRuntime(ModDependencyNode pModNode);
    void RegisterPrecompiled(string pUid, string pMainDll);
    bool CompileNode(ModDependencyNode pModNode, bool pForce = false);
}

/// <summary>
/// Facade over the Roslyn backend. The backend type (<c>NeoModLoader.services.RoslynBackend</c>) is resolved by
/// name through reflection, never by a direct token reference, so the JIT never touches Roslyn-typed
/// fields/signatures unless a source mod actually needs compiling and <see cref="CompilerPack"/> is loaded.
/// </summary>
internal static class ModCompiler
{
    private const string BackendTypeName = "NeoModLoader.services.RoslynBackend";
    private static ICompilerBackend _backend;

    private static ICompilerBackend Backend
    {
        get
        {
            if (_backend != null) return _backend;
            if (!CompilerPack.EnsureLoaded())
                throw new InvalidOperationException("Compiler pack is not loaded; cannot create the Roslyn backend");
            var type = typeof(ModCompiler).Assembly.GetType(BackendTypeName, true);
            _backend = (ICompilerBackend)Activator.CreateInstance(type, true);
            return _backend;
        }
    }

    internal static bool IsPrepared => _backend != null && _backend.IsPrepared;
    internal static void PrepareReferences(List<ModDependencyNode> pModNodes) => Backend.PrepareReferences(pModNodes);
    internal static void EnsureDefaultReferences() => Backend.EnsureDefaultReferences();
    internal static void PrepareRuntime(ModDependencyNode pModNode) => Backend.PrepareRuntime(pModNode);
    internal static void RegisterPrecompiled(string pUid, string pMainDll) => Backend.RegisterPrecompiled(pUid, pMainDll);
    internal static bool CompileNode(ModDependencyNode pModNode, bool pForce = false) => Backend.CompileNode(pModNode, pForce);
}
