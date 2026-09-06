using NeoModLoader.api;

namespace HelloMod;

/// <summary>
/// Minimal GodForge mod. Built with tools/build_mod.sh into godforge.hellomod.dll next to mod.json.
/// </summary>
public class Main : BasicMod<Main>
{
    protected override void OnModLoad()
    {
        LogInfo("Hello from HelloMod! GodForge precompiled mod is running.");
    }
}
