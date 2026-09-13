// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Idel Nigmatullin and GodForge contributors
// This file is part of GodForge (GFML). See LICENSE for details.

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
    void ReleaseReferences();
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

    // Mod dlls (precompiled or taken from the compile cache) registered before any mod actually needed the
    // compiler. They are replayed into the backend the moment it is created, so a later source mod can still
    // reference them; until then nothing Roslyn-related is loaded.
    private static readonly Dictionary<string, string> _pending_refs = new();

    private static ICompilerBackend Backend
    {
        get
        {
            if (_backend != null) return _backend;
            if (!CompilerPack.EnsureLoaded())
                throw new InvalidOperationException("Compiler pack is not loaded; cannot create the Roslyn backend");
            var type = typeof(ModCompiler).Assembly.GetType(BackendTypeName, true);
            _backend = (ICompilerBackend)Activator.CreateInstance(type, true);
            foreach (var pair in _pending_refs) _backend.RegisterPrecompiled(pair.Key, pair.Value);
            _pending_refs.Clear();
            return _backend;
        }
    }

    internal static bool IsPrepared => _backend != null && _backend.IsPrepared;
    internal static void PrepareReferences(List<ModDependencyNode> pModNodes) => Backend.PrepareReferences(pModNodes);
    internal static void EnsureDefaultReferences() => Backend.EnsureDefaultReferences();
    internal static void PrepareRuntime(ModDependencyNode pModNode) => Backend.PrepareRuntime(pModNode);

    /// <summary>
    /// Make a ready mod dll visible to mods compiled from source. Does not load the compiler pack by itself.
    /// </summary>
    internal static void RegisterPrecompiled(string pUid, string pMainDll)
    {
        if (_backend != null) _backend.RegisterPrecompiled(pUid, pMainDll);
        else _pending_refs[pUid] = pMainDll;
    }

    internal static bool CompileNode(ModDependencyNode pModNode, bool pForce = false) => Backend.CompileNode(pModNode, pForce);

    /// <summary>
    /// Drop the in-memory metadata of every reference assembly once the startup compilation is over. Roslyn
    /// itself stays loaded, but the ~100 MB of prefetched images does not have to live for the whole session;
    /// a later runtime compile rebuilds them lazily.
    /// </summary>
    internal static void ReleaseReferences()
    {
        if (_backend == null) return;
        _backend.ReleaseReferences();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
