using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppList = Il2CppSystem.Collections.Generic.List<object>;

// Global namespace on purpose: mods do not add usings for us, and the older Extentions class lives here too.
/// <summary>
///     Query helpers typed on the collection itself. The older helpers hang off <see cref="Il2CppObjectBase" />,
///     which carries no element type, so the compiler cannot infer T for calls like list.FirstOrDefault(k =&gt; ...)
///     that PC mods are written with. These overloads take the concrete collection, so T comes for free.
/// </summary>
public static class TypedLinq
{
    public static T FirstOrDefault<T>(this Il2CppSystem.Collections.Generic.List<T> pList)
    {
        return pList != null && pList.Count > 0 ? pList[0] : default;
    }

    public static T FirstOrDefault<T>(this Il2CppSystem.Collections.Generic.List<T> pList, Func<T, bool> pPredicate)
    {
        if (pList == null) return default;
        for (int i = 0; i < pList.Count; i++)
            if (pPredicate(pList[i]))
                return pList[i];
        return default;
    }

    public static T First<T>(this Il2CppSystem.Collections.Generic.List<T> pList, Func<T, bool> pPredicate)
    {
        T found = pList.FirstOrDefault(pPredicate);
        if (found == null) throw new InvalidOperationException("No element matches the condition");
        return found;
    }

    public static bool Any<T>(this Il2CppSystem.Collections.Generic.List<T> pList, Func<T, bool> pPredicate)
    {
        return pList.FirstOrDefault(pPredicate) != null;
    }

    public static int Count<T>(this Il2CppSystem.Collections.Generic.List<T> pList, Func<T, bool> pPredicate)
    {
        int count = 0;
        if (pList == null) return 0;
        for (int i = 0; i < pList.Count; i++)
            if (pPredicate(pList[i]))
                count++;
        return count;
    }

    public static List<T> Where<T>(this Il2CppSystem.Collections.Generic.List<T> pList, Func<T, bool> pPredicate)
    {
        var result = new List<T>();
        if (pList == null) return result;
        for (int i = 0; i < pList.Count; i++)
            if (pPredicate(pList[i]))
                result.Add(pList[i]);
        return result;
    }

    public static List<R> Select<T, R>(this Il2CppSystem.Collections.Generic.List<T> pList, Func<T, R> pSelector)
    {
        var result = new List<R>();
        if (pList == null) return result;
        for (int i = 0; i < pList.Count; i++) result.Add(pSelector(pList[i]));
        return result;
    }

    public static List<T> ToList<T>(this Il2CppSystem.Collections.Generic.List<T> pList)
    {
        var result = new List<T>();
        if (pList == null) return result;
        for (int i = 0; i < pList.Count; i++) result.Add(pList[i]);
        return result;
    }

    public static List<T> ToList<T>(this Il2CppReferenceArray<T> pArray) where T : Il2CppObjectBase
    {
        var result = new List<T>();
        if (pArray == null) return result;
        for (int i = 0; i < pArray.Count; i++) result.Add(pArray[i]);
        return result;
    }

    public static T FirstOrDefault<T>(this Il2CppReferenceArray<T> pArray, Func<T, bool> pPredicate)
        where T : Il2CppObjectBase
    {
        if (pArray == null) return default;
        for (int i = 0; i < pArray.Count; i++)
            if (pPredicate(pArray[i]))
                return pArray[i];
        return default;
    }

    public static List<T> Where<T>(this Il2CppReferenceArray<T> pArray, Func<T, bool> pPredicate)
        where T : Il2CppObjectBase
    {
        var result = new List<T>();
        if (pArray == null) return result;
        for (int i = 0; i < pArray.Count; i++)
            if (pPredicate(pArray[i]))
                result.Add(pArray[i]);
        return result;
    }

    // Game iterators (Finder.getUnitsFromChunk and friends) come back as Il2Cpp IEnumerable<T>, where the
    // element type is on the receiver, so these overloads let T be inferred instead of demanded.
    public static IEnumerable<T> AsManaged<T>(this Il2CppSystem.Collections.Generic.IEnumerable<T> pSource)
    {
        if (pSource == null) yield break;
        Il2CppSystem.Collections.Generic.IEnumerator<T> enumerator = pSource.GetEnumerator();
        while (enumerator.MoveNext()) yield return enumerator.Current;
    }

    public static T FirstOrDefault<T>(this Il2CppSystem.Collections.Generic.IEnumerable<T> pSource,
                                      Func<T, bool> pPredicate)
    {
        foreach (T item in pSource.AsManaged())
            if (pPredicate(item))
                return item;
        return default;
    }

    public static bool Any<T>(this Il2CppSystem.Collections.Generic.IEnumerable<T> pSource, Func<T, bool> pPredicate)
    {
        return pSource.FirstOrDefault(pPredicate) != null;
    }

    public static int Count<T>(this Il2CppSystem.Collections.Generic.IEnumerable<T> pSource, Func<T, bool> pPredicate)
    {
        int count = 0;
        foreach (T item in pSource.AsManaged())
            if (pPredicate(item))
                count++;
        return count;
    }

    public static List<T> Where<T>(this Il2CppSystem.Collections.Generic.IEnumerable<T> pSource,
                                   Func<T, bool> pPredicate)
    {
        var result = new List<T>();
        foreach (T item in pSource.AsManaged())
            if (pPredicate(item))
                result.Add(item);
        return result;
    }

    public static List<R> Select<T, R>(this Il2CppSystem.Collections.Generic.IEnumerable<T> pSource,
                                       Func<T, R> pSelector)
    {
        var result = new List<R>();
        foreach (T item in pSource.AsManaged()) result.Add(pSelector(item));
        return result;
    }

}
