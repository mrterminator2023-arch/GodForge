using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


public static class Extentions
{
    public static T GetWrappedComponent<T>(this GameObject obj)
    {
        return obj.GetComponent<T>();
    }
    public static A C<A>(this A a)
    {
        return a;
    }
    public static System.Collections.Generic.List<T> AsList<T>(this IEnumerable<T> Object)
    {
        return Object.ToList();
    }
    public static HashSet<A> Get<A, B>(this SimSystemManager<A, B> manager) where A : BaseSimObject, ILoadable<B>, new() where B : BaseObjectData, new()
    {
        return manager._container._hashSet;
    }
    public static HashSet<A> Get<A, B>(this MetaSystemManager<A, B> manager) where A : MetaObject<B>, new() where B : MetaObjectData, new()
    {
        return manager._hashset;
    }
    public static A? N<A>(this A a) where A : struct
    {
        return a;
    }
    public static void AddListener(this UnityEvent ev, Action action)
    {
        ev.AddListener(() => action());
    }
    public static T Cast<T>(this object obj)
    {
        return (T)obj;
    }
    public static void AddListener<T>(this UnityEvent<T> ev, Action<T> action)
    {
        ev.AddListener((T t) => action(t));
    }
    public static void setToggleAction(this GodPower button, PowerToggleAction action)
    {
        button.toggle_action = action;
    }
    public static GUIStyle Clone(this GUIStyle obj)
    {
        return obj;
    }
    public static List<Transform> GetChildren(this Transform transform)
    {
        List<Transform> list = new List<Transform>();
        foreach (Transform tr in transform)
        {
            list.Add(tr);
        }

        return list;
    }
}