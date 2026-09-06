using NeoModLoader.General;
using NeoModLoader.utils;
using UnityEngine;

namespace NeoModLoader.ui;

internal static class UIManager
{
    public static void init()
    {
        InformationWindow.CreateWindow("Information", "Information Title");
        ModListWindow.CreateAndInit("GodForgeModList");
        ModConfigureWindow.CreateAndInit("ModConfigure");
        PowerButtonCreator.AddButtonToTab(
            PowerButtonCreator.CreateWindowButton("GodForge_ModsList", "GodForgeModList",
                                                  InternalResourcesGetter.GetIcon()),
            PowerButtonCreator.GetTab(PowerTabNames.Main),
          22);
    }
}