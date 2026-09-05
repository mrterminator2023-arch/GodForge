using HarmonyLib;
using HarmonyLib.Public.Patching;
using MonoMod.Cil;
using NeoModLoader.constants;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
#pragma warning disable CS0618 // Type or member is obsolete

namespace NeoModLoader.AndroidCompatibilityModule.TranspilerSupport;
//TODO: preprocess the publicized assembly to have IL code that references il2cpp assemblies.
/// <summary>
/// tells TranspilerSupport to not replace the IL2CPP function you are transpiling with a managed one
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class IgnoreTranspilerSupport : Attribute
{
}

/// <summary>
/// A utility which replaces IL2CPP methods with managed methods (from the PC version) for transpilers. generic methods (or methods apart of generic classes) are NOT SUPPORTED
/// </summary>
public static class TranspilerSupport
{
    static string FileName(this Assembly assembly)
    {
        return Path.GetFileName(assembly.Location);
    }
    /// <summary>
    /// the universal IL2CPP to Managed IL transpiler. make sure your transpiler has less priority then it or it will be overwritten
    /// </summary>
    static readonly HarmonyMethod IL2CPP2Managed =
        AccessTools.Method(typeof(TranspilerSupport), nameof(il2cpp2managed));

    private static Harmony harmony;
    internal static void Initialize()
    {
        harmony = new Harmony("AndroidCompatibilityModule.TranspilerSupport");
        MirroredAssemblies.Init();
        harmony.Patch(
            AccessTools.Method(typeof(HarmonyManipulator), nameof(HarmonyManipulator.Manipulate),
                [typeof(MethodBase), typeof(PatchInfo), typeof(ILContext)]),
            new HarmonyMethod(typeof(TranspilerSupport), nameof(PatchPrefix))
        );
    }

    private static HashSet<MethodBase> MirroredMethods;
    /// <summary>
    ///  Generates a mirror method for your transpiler and replaces the transpiler param with the IL2CPP2Managed transpiler or null if already patched
    /// </summary>
    static bool CheckTranspiler(MethodBase original, MethodInfo transpiler)
    {
        if (transpiler.GetCustomAttribute<IgnoreTranspilerSupport>() != null ||
            transpiler.DeclaringType!.GetCustomAttribute<IgnoreTranspilerSupport>() != null)
        {
            return false;
        }
        if (MirroredMethods.Contains(original))
        {
            return false;
        }
        return true;
    }

    private static void PatchPrefix(MethodBase original, PatchInfo patchInfo)
    {
        if (original?.DeclaringType == null || !ShouldRedirect(original))
        {
            return;
        }
        if (patchInfo.transpilers == null || patchInfo.transpilers.Length == 0)
        {
            return;
        }
        bool Add = false;
        foreach (var patch in patchInfo.transpilers)
        {
            MethodInfo transpiler = patch.PatchMethod;
            if (CheckTranspiler(original, transpiler))
            {
                Add = true;
                MirroredMethods.Add(original);
            }
        }
        if (Add)
        {
            patchInfo.AddTranspiler(IL2CPP2Managed.method, harmony.Id, IL2CPP2Managed.priority, null, null, true);
        }
    }

    private static bool ShouldRedirect(MethodBase method)
    {
        return method.DeclaringType.Assembly.FileName() == "Assembly-CSharp.dll";
    }
    [IgnoreTranspilerSupport]
    [HarmonyPriority(100000000)]
    private static IEnumerable<CodeInstruction> il2cpp2managed(
        IEnumerable<CodeInstruction> instructions,
        MethodBase original,
        ILGenerator generator)
    {
        MethodBase mirror = MirroredAssemblies.GetMirror(original);
        List<CodeInstruction> codes =
            PatchProcessor.GetOriginalInstructions(mirror);
        var locals = mirror.GetMethodBody()?.LocalVariables ?? [];
        var localMap = new Dictionary<int, LocalBuilder>();
        foreach (var local in locals)
        {
            localMap[local.LocalIndex] =
                generator.DeclareLocal(local.LocalType!, local.IsPinned);
        }
        var labelMap = new Dictionary<Label, Label>();
        Label GetOrCreateLabel(Label oldLabel)
        {
            if (!labelMap.TryGetValue(oldLabel, out var newLabel))
            {
                newLabel = generator.DefineLabel();
                labelMap[oldLabel] = newLabel;
            }
            return newLabel;
        }
        foreach (var instr in codes)
        {
            var clone = new CodeInstruction(instr);
            if (clone.labels != null && clone.labels.Count > 0)
            {
                clone.labels = clone.labels
                    .Select(GetOrCreateLabel)
                    .ToList();
            }
            if (clone.blocks is { Count: > 0 })
            {
                clone.blocks = new List<ExceptionBlock>(clone.blocks);
            }
            clone.operand = clone.operand switch
            {
                LocalBuilder lb =>
                    localMap[lb.LocalIndex],

                Label lbl =>
                    GetOrCreateLabel(lbl),

                Label[] lbls =>
                    lbls.Select(GetOrCreateLabel).ToArray(),

                _ => clone.operand
            };
            yield return clone;
        }
    }
}
/// <summary>
/// The Managed mirrored assemblies, all loaded in a separate context.
/// </summary>
public class MirroredAssemblies : AssemblyLoadContext
{
    /// <summary>
    /// gets the mirror of a method from assembly-csharp-publicized, throws if not found
    /// </summary>
    public static MethodBase GetMirror(MethodBase original)
    {
        var mirrorType = RemapType(original.DeclaringType);
        if (mirrorType == null)
        {
            throw new MissingMethodException("The Managed type does not exist!");
        }
        var paramList = original.GetParameters()
            .Select(p => p.ParameterType)
            .ToList();
        if (!original.IsStatic)
            paramList.Insert(0, original.DeclaringType);
            
        MethodInfo mirrorMethod = mirrorType.GetMethod(
            original.Name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
            null,
            paramList.ToArray(),
            null
        );
        if (mirrorMethod == null)
        {
            throw new MissingMethodException("The Managed method does not exist!");
        }
        return mirrorMethod;
    }
    /// <summary>
    /// the assembly from the PC version
    /// </summary>
    public static Assembly ManagedAssembly { get; internal set; }

    /// <summary>
    /// the IL2CPP Assembly
    /// </summary>
    public static Assembly NativeAssembly { get; internal set; }
    private MirroredAssemblies() : base("ManagedAssemblies")
    {
    }

    protected override Assembly Load(AssemblyName assemblyName) => null;
    private static MirroredAssemblies Instance;
    internal static void Init()
    {
        Instance = new MirroredAssemblies();
        ManagedAssembly = LoadMirrorAssembly(Paths.PublicizedAssemblyPath);
        NativeAssembly = typeof(Actor).Assembly;
    }

    public static Assembly LoadMirrorAssembly(string path)
    {
        return Instance.LoadFromAssemblyPath(Path.GetFullPath(path));
    }
   private static readonly Dictionary<Type, Type> NativeToManaged = new();

   public static Type RemapType(Type type, Type declaringType = null)
   {
       if (NativeToManaged.TryGetValue(type, out var cached))
           return cached;

       Type result;

       if (type.IsByRef)
       {
           result = RemapType(type.GetElementType(), declaringType)
               ?.MakeByRefType();
       }
       else if (type.IsPointer)
       {
           result = RemapType(type.GetElementType(), declaringType)
               ?.MakePointerType();
       }
       else if (type.IsArray)
       {
           var element = RemapType(type.GetElementType(), declaringType);

           int rank = type.GetArrayRank();

           result = rank == 1
               ? element?.MakeArrayType()
               : element?.MakeArrayType(rank);
       }
       else if (type.IsGenericParameter)
       {
           result = declaringType != null &&
                    declaringType.IsGenericType
               ? declaringType.GetGenericArguments()[type.GenericParameterPosition]
               : type;
       }
       else if (type.IsGenericType && !type.IsGenericTypeDefinition)
       {
           var def = RemapType(type.GetGenericTypeDefinition(), declaringType);

           var args = type.GetGenericArguments()
               .Select(t => RemapType(t, declaringType))
               .ToArray();

           result = def?.MakeGenericType(args);
       }
       else if (type.IsNested)
       {
           var declaring = RemapType(type.DeclaringType, declaringType);

           var nested = declaring?.GetNestedType(
               type.Name,
               BindingFlags.Public | BindingFlags.NonPublic);

           if (nested != null &&
               type.IsGenericType &&
               !type.IsGenericTypeDefinition)
           {
               var args = type.GetGenericArguments()
                   .Select(t => RemapType(t, declaringType))
                   .ToArray();
               nested = nested.MakeGenericType(args);
           }
           result = nested;
       }
       else
       {
           result = ManagedAssembly.GetType(type.FullName, false);
       }
       NativeToManaged[type] = result;
       return result;
   }
}