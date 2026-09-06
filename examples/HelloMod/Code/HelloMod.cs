using NeoModLoader.api;

namespace HelloMod;

/// <summary>
/// Minimal WBML mod. Built with tools/build_mod.sh into wbml.hellomod.dll next to mod.json.
/// </summary>
public class Main : BasicMod<Main>
{
    protected override void OnModLoad()
    {
        LogInfo("Hello from HelloMod! WBML precompiled mod is running.");
    }
}
