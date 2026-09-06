using System;
using System.Collections.Generic;

using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

using NeoModLoader.AndroidCompatibilityModule;

using UnityEngine;
using UnityEngine.Events;

namespace NeoModLoader.compat;

/// <summary>
///     Glue that lets mods written against the PC (Mono) build of NeoModLoader compile unchanged on the
///     IL2CPP (Android) build.
///     <para>
///         The namespace is injected as a global using into every mod compilation
///         (see <c>RoslynBackend.compileMod</c>), so the extension methods below are always in scope and the
///         helpers can be referenced by the source fixer (<c>Il2CppSourceFixer</c>) without the mod importing
///         anything.
///     </para>
/// </summary>
public static class Il2CppPcCompat
{
    /// <summary>System.Type -&gt; Il2CppSystem.Type</summary>
    public static Il2CppSystem.Type Ty(Type pType)
    {
        return pType == null ? null : Il2CppType.From(pType);
    }

    /// <summary>System.Type(s) -&gt; the reference array IL2CPP APIs (e.g. <c>new GameObject(name, types)</c>) expect.</summary>
    public static Il2CppReferenceArray<Il2CppSystem.Type> Types(params Type[] pTypes)
    {
        pTypes ??= Array.Empty<Type>();
        var result = new Il2CppReferenceArray<Il2CppSystem.Type>((long)pTypes.Length);
        for (int i = 0; i < pTypes.Length; i++) result[i] = Il2CppType.From(pTypes[i]);
        return result;
    }

    /// <summary>Managed list/array/sequence -&gt; Il2CppSystem.Collections.Generic.List.</summary>
    public static Il2CppSystem.Collections.Generic.List<T> L<T>(IEnumerable<T> pSource)
    {
        var list = new Il2CppSystem.Collections.Generic.List<T>();
        if (pSource == null) return list;
        foreach (var item in pSource) list.Add(item);
        return list;
    }

    /// <summary>Managed sequence of value types -&gt; Il2CppStructArray.</summary>
    public static Il2CppStructArray<T> SA<T>(IEnumerable<T> pSource) where T : unmanaged
    {
        var buffer = pSource == null ? new List<T>() : new List<T>(pSource);
        var result = new Il2CppStructArray<T>(buffer.Count);
        for (int i = 0; i < buffer.Count; i++) result[i] = buffer[i];
        return result;
    }

    /// <summary>Managed sequence of IL2CPP objects -&gt; Il2CppReferenceArray.</summary>
    public static Il2CppReferenceArray<T> RA<T>(IEnumerable<T> pSource) where T : Il2CppObjectBase
    {
        var buffer = pSource == null ? new List<T>() : new List<T>(pSource);
        var result = new Il2CppReferenceArray<T>((long)buffer.Count);
        for (int i = 0; i < buffer.Count; i++) result[i] = buffer[i];
        return result;
    }

    /// <summary>Managed sequence of strings -&gt; Il2CppStringArray.</summary>
    public static Il2CppStringArray StrA(IEnumerable<string> pSource)
    {
        var buffer = pSource == null ? new List<string>() : new List<string>(pSource);
        var result = new Il2CppStringArray(buffer.Count);
        for (int i = 0; i < buffer.Count; i++) result[i] = buffer[i];
        return result;
    }

    /// <summary>IL2CPP <see cref="UnityAction" /> -&gt; managed <see cref="Action" /> (our prefab APIs take Action).</summary>
    public static Action Act(UnityAction pAction)
    {
        if (pAction == null) return null;
        return () => pAction.Invoke();
    }

    /// <summary>IL2CPP <see cref="UnityAction{T}" /> -&gt; managed <see cref="Action{T}" />.</summary>
    public static Action<T> Act<T>(UnityAction<T> pAction)
    {
        if (pAction == null) return null;
        return pValue => pAction.Invoke(pValue);
    }

    /// <summary>Managed <see cref="Action" /> -&gt; IL2CPP <see cref="UnityAction" />.</summary>
    public static UnityAction UAct(Action pAction)
    {
        return pAction == null ? null : IL2CPPHelper.C<UnityAction>(pAction);
    }

    /// <summary>
    ///     <c>Object.Instantiate(SomePrefab.Prefab, parent)</c> on PC returns the prefab type. Our prefabs are
    ///     <see cref="WrappedBehaviour" />s (not UnityEngine.Objects), so the generic overload does not bind and the
    ///     call degrades to <c>UnityEngine.Object</c>. This keeps the PC return type and the wrapper bookkeeping.
    /// </summary>
    public static T Instantiate<T>(T pOriginal, Transform pParent = null, bool pWorldPositionStays = false)
        where T : WrappedBehaviour
    {
        return WrapperHelper.Instantiate(pOriginal, pParent, pWorldPositionStays);
    }
}

/// <summary>
///     Extension methods that restore Mono signatures which IL2CPP interop replaced with Il2Cpp* container types.
///     Extension methods only kick in when no instance overload is applicable, so they never shadow working code.
/// </summary>
public static class PcCompatExtensions
{
    public static void SetVertices(this Mesh pMesh, IEnumerable<Vector3> pValues)
    {
        pMesh.SetVertices(Il2CppPcCompat.L(pValues));
    }

    public static void SetNormals(this Mesh pMesh, IEnumerable<Vector3> pValues)
    {
        pMesh.SetNormals(Il2CppPcCompat.L(pValues));
    }

    public static void SetTangents(this Mesh pMesh, IEnumerable<Vector4> pValues)
    {
        pMesh.SetTangents(Il2CppPcCompat.L(pValues));
    }

    public static void SetColors(this Mesh pMesh, IEnumerable<Color32> pValues)
    {
        pMesh.SetColors(Il2CppPcCompat.L(pValues));
    }

    public static void SetColors(this Mesh pMesh, IEnumerable<Color> pValues)
    {
        pMesh.SetColors(Il2CppPcCompat.L(pValues));
    }

    public static void SetUVs(this Mesh pMesh, int pChannel, IEnumerable<Vector2> pValues)
    {
        pMesh.SetUVs(pChannel, Il2CppPcCompat.L(pValues));
    }

    public static void SetUVs(this Mesh pMesh, int pChannel, IEnumerable<Vector3> pValues)
    {
        pMesh.SetUVs(pChannel, Il2CppPcCompat.L(pValues));
    }

    public static void SetTriangles(this Mesh pMesh, IEnumerable<int> pValues, int pSubMesh)
    {
        pMesh.SetTriangles(Il2CppPcCompat.SA(pValues), pSubMesh);
    }

    public static void SetTriangles(this Mesh pMesh, IEnumerable<int> pValues, int pSubMesh, bool pCalculateBounds)
    {
        pMesh.SetTriangles(Il2CppPcCompat.SA(pValues), pSubMesh, pCalculateBounds);
    }

    public static void SetTriangles(this Mesh pMesh, IEnumerable<int> pValues, int pSubMesh, bool pCalculateBounds,
        int pBaseVertex)
    {
        pMesh.SetTriangles(Il2CppPcCompat.SA(pValues), pSubMesh, pCalculateBounds, pBaseVertex);
    }

    public static void SetIndices(this Mesh pMesh, IEnumerable<int> pValues, MeshTopology pTopology, int pSubMesh)
    {
        pMesh.SetIndices(Il2CppPcCompat.SA(pValues), pTopology, pSubMesh);
    }
}
