using NeoModLoader.api;
using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;

namespace EncodeDecodeSnooper;

public class Main : BasicMod<Main>
{
    protected override void OnModLoad()
    {
        LogInfo("=== EncodeDecodeSnooper loaded ===");

        try
        {
            var asmCSharp = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (asmCSharp == null)
            {
                LogError("Assembly-CSharp not found");
                return;
            }

            var playerConfigType = asmCSharp.GetType("PlayerConfig");
            if (playerConfigType == null)
            {
                LogError("PlayerConfig not found");
                return;
            }

            LogInfo($"[+] Found PlayerConfig, {playerConfigType.GetMethods().Length} methods");

            // List all methods
            foreach (var method in playerConfigType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (method.Name.Contains("encode") || method.Name.Contains("decode") ||
                    method.Name.Contains("Encode") || method.Name.Contains("Decode") ||
                    method.Name.Contains("save") || method.Name.Contains("load") ||
                    method.Name.Contains("Save") || method.Name.Contains("Load"))
                {
                    LogInfo($"[!] Found interesting method: {method.Name}");

                    // Log the method signature
                    var parameters = method.GetParameters();
                    LogInfo($"    Parameters: {string.Join(", ", parameters.Select(p => p.ParameterType.Name))}");
                    LogInfo($"    Return type: {method.ReturnType.Name}");
                }
            }

            // Hook Harmony for encode/decode calls
            var harmony = new Harmony("EncodeDecodeSnooper");

            // Try to patch common encode/decode methods
            var methods = playerConfigType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.Name.ToLower().Contains("encode") || m.Name.ToLower().Contains("decode")).ToList();

            foreach (var method in methods)
            {
                try
                {
                    LogInfo($"[*] Attempting to patch: {method.Name}");

                    // This is a prefix that will log when the method is called
                    var prefixMethod = typeof(Main).GetMethod("LogMethodCall",
                        BindingFlags.Static | BindingFlags.Public);

                    if (prefixMethod != null)
                    {
                        harmony.Patch(method, new HarmonyMethod(prefixMethod));
                        LogInfo($"[+] Patched: {method.Name}");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"[-] Failed to patch {method.Name}: {ex.Message}");
                }
            }

            LogInfo("✓ Snooper initialized");
        }
        catch (Exception ex)
        {
            LogError($"Exception: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public static void LogMethodCall()
    {
        // Will log when encode/decode is called
        Main.LogInfo("[HOOK] Method called");
    }
}
