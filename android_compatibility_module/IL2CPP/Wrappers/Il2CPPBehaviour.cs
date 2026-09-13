using System.Reflection;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Fields;
using MelonLoader;
using NeoModLoader.utils.Collections;
using UnityEngine;

namespace NeoModLoader.AndroidCompatibilityModule;
[RegisterTypeInIl2Cpp]
public sealed class Il2CPPBehaviour : MonoBehaviour
{
    public Il2CPPBehaviour(IntPtr ptr) : base(ptr)
    {
    }

    public Il2CPPBehaviour() : base(ClassInjector.DerivedConstructorPointer<Il2CPPBehaviour>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    public void OnEnable()
    {
        if (WrappedBehaviour == null) return;
        onenable?.Invoke(WrappedBehaviour);
    }

    public void Start()
    {
        if (WrappedBehaviour == null) return;
        start?.Invoke(WrappedBehaviour);
    }

    public void OnDisable()
    {
        if (WrappedBehaviour == null) return;
        ondisable?.Invoke(WrappedBehaviour);
    }

    private bool canawake;
    public void Awake()
    {
        if (!canawake || WrappedBehaviour == null) return;
        awake?.Invoke(WrappedBehaviour);
        canawake = false;
    }
    public void OnDestroy()
    {
        if (WrappedBehaviour == null) return;
        ondestroy?.Invoke(WrappedBehaviour);
    }

    public void Update()
    {
        if (WrappedBehaviour == null) return;
        if (WrappedBehaviour.HasPendingInvokations) WrappedBehaviour.HandleInvokations(Time.deltaTime);
        update?.Invoke(WrappedBehaviour);
    }

    public void LateUpdate()
    {
        if (WrappedBehaviour == null) return;
        lateupdate?.Invoke(WrappedBehaviour);
    }

    public void OnGUI()
    {
        if (WrappedBehaviour == null) return;
        ongui?.Invoke(WrappedBehaviour);
    }
    [HideFromIl2Cpp]
    WrappedAction GetWrappedMethod(string Method)
    {
        return WrappedMethodCollection.Get(WrappedType)[Method];
    }
    /// <summary>
    ///     How many wrappers were ever attached. Zero means no mod behaviour exists yet, so the Instantiate
    ///     postfix (see <see cref="WrapperHelper.Resolve"/>) can return without touching the hierarchy.
    /// </summary>
    public static int LiveWrapperCount { [HideFromIl2Cpp] get; [HideFromIl2Cpp] private set; }

    [HideFromIl2Cpp]
    public B SetWrappedBehaviour<B>(B Behaviour) where B : WrappedBehaviour
    {
        LiveWrapperCount++;
        WrappedBehaviour = Behaviour;
        WrappedType = Behaviour.GetType();
        Behaviour.Wrapper = this;
        update = GetWrappedMethod("Update");
        start = GetWrappedMethod("Start");
        awake = GetWrappedMethod("Awake");
        ongui = GetWrappedMethod("OnGUI");
        onenable = GetWrappedMethod("OnEnable");
        ondisable = GetWrappedMethod("OnDisable");
        lateupdate = GetWrappedMethod("LateUpdate");
        ondestroy = GetWrappedMethod("OnDestroy");
        canawake = true;
        if (gameObject.activeInHierarchy)
        {
            Awake();
        }
        return Behaviour;
    }
    [HideFromIl2Cpp]
    public WrappedBehaviour CreateWrapperIfNull(Type WrappedType)
    {
        return WrappedBehaviour ?? SetWrappedBehaviour((WrappedBehaviour)Activator.CreateInstance(WrappedType));
    }
    [HideFromIl2Cpp]
    public W CreateWrapper<W>() where W : WrappedBehaviour
    {
        return SetWrappedBehaviour(Activator.CreateInstance<W>());
    }
    private WrappedAction update;
    private WrappedAction start;
    private WrappedAction awake;
    private WrappedAction ongui;
    private WrappedAction onenable;
    private WrappedAction ondisable;
    private WrappedAction lateupdate;
    private WrappedAction ondestroy;
    public Type WrappedType { [HideFromIl2Cpp] get; [HideFromIl2Cpp] private set; }
    public WrappedBehaviour WrappedBehaviour { [HideFromIl2Cpp] get; [HideFromIl2Cpp] private set; }
}
public delegate void WrappedAction(WrappedBehaviour instance);