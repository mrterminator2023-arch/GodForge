using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.Events;

namespace NeoModLoader.utils;

/// <summary>
///     Runtime helpers used by <see cref="PcModRewriter" /> when it adapts a PC-written mod to Il2Cpp.
///     Mods can also call these directly.
/// </summary>
public static class PcModCompat
{
    /// <summary>Copies a managed list into the Il2Cpp list expected by Unity APIs.</summary>
    public static Il2CppSystem.Collections.Generic.List<T> L<T>(List<T> pList)
    {
        var result = new Il2CppSystem.Collections.Generic.List<T>();
        if (pList == null) return result;
        foreach (T item in pList) result.Add(item);
        return result;
    }

    /// <summary>Copies a managed list into the Il2Cpp array expected by Unity APIs.</summary>
    public static Il2CppStructArray<T> A<T>(List<T> pList) where T : unmanaged
    {
        // Il2CppStructArray's array constructor copies the whole block at once, unlike per-element writes.
        return new Il2CppStructArray<T>(pList?.ToArray() ?? Array.Empty<T>());
    }

    /// <summary>Wraps an Il2Cpp UnityAction so that it can be passed where a managed delegate is expected.</summary>
    public static Action Act(UnityAction pAction)
    {
        return pAction == null ? null : () => pAction.Invoke();
    }

    /// <summary>Builds the Il2Cpp type array that Il2Cpp constructors take instead of a params Type[].</summary>
    public static Il2CppReferenceArray<Il2CppSystem.Type> T(params Type[] pTypes)
    {
        int count = pTypes?.Length ?? 0;
        var result = new Il2CppReferenceArray<Il2CppSystem.Type>(count);
        for (int i = 0; i < count; i++) result[i] = Il2CppType.From(pTypes[i]);
        return result;
    }
}
