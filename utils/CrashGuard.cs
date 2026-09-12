using System;
using System.Collections.Generic;
using System.IO;
using NeoModLoader.constants;
using NeoModLoader.services;

namespace NeoModLoader.utils;

/// <summary>
///     Keeps a bad mod from leaving the player with a game that will not start. A mod can take the process down
///     natively (a Harmony patch on a game method, a bad pointer), which no try/catch can catch, so instead we
///     record that a modded session was opened and only clear that record once the game is actually running.
///     Two openings in a row that never got cleared mean modded startup is crashing: the next launch loads no
///     mods and tells the player why.
/// </summary>
public static class CrashGuard
{
    /// <summary>Seconds of a running game after which the session counts as healthy.</summary>
    private const float HealthySeconds = 60f;

    private static string FilePath => Path.Combine(Paths.NMLPath, "crash_guard.txt");
    private static float _elapsed;
    private static bool _armed;

    /// <summary>True when the previous launches crashed: every mod gets switched off, the player re-enables what they want.</summary>
    public static bool SafeMode { get; private set; }

    /// <summary>Set for this session once the guard has switched all mods off, so the list can say why.</summary>
    public static bool DisabledAllThisSession { get; private set; }

    /// <summary>Switches every known mod off and clears the crash record; called once mods are discovered.</summary>
    public static void DisableAllMods(IEnumerable<string> pModUids)
    {
        if (!SafeMode) return;
        foreach (string uid in pModUids) ModInfoUtils.setModDisabled(uid, true, false);
        ModInfoUtils.SaveModRecords();
        SafeMode = false;                 // toggles work again: the player decides what to turn back on
        DisabledAllThisSession = true;
        Write(0);
    }

    /// <summary>Mods disabled by the guard, shown in the mod list so the player knows what happened.</summary>
    public static List<string> DisabledByGuard { get; } = new();

    /// <summary>Reads the record of the previous launch and decides whether to start without mods.</summary>
    public static void BeginSession()
    {
        int failures = 0;
        try
        {
            if (File.Exists(FilePath) && int.TryParse(File.ReadAllText(FilePath).Trim(), out int stored))
                failures = stored;
        }
        catch (Exception e)
        {
            LogService.LogWarning($"Crash guard cannot read its record: {e.Message}");
        }

        SafeMode = failures >= 2;
        if (SafeMode)
            LogService.LogWarning("The last two launches crashed before the game started: loading no mods this time. " +
                                  "Enable mods again in the mod list once you removed the faulty one.");

        Write(failures + 1);
        _armed = true;
        _elapsed = 0f;
    }

    /// <summary>Called every frame; once the game has run long enough the session is marked healthy.</summary>
    public static void Tick(float pDeltaTime)
    {
        if (!_armed) return;
        _elapsed += pDeltaTime;
        if (_elapsed < HealthySeconds) return;

        _armed = false;
        Write(0);
    }

    /// <summary>Clears the record, e.g. after the player re-enables mods knowingly.</summary>
    public static void Reset()
    {
        SafeMode = false;
        Write(0);
    }

    private static void Write(int pFailures)
    {
        try
        {
            Directory.CreateDirectory(Paths.NMLPath);
            File.WriteAllText(FilePath, pFailures.ToString());
        }
        catch (Exception e)
        {
            LogService.LogWarning($"Crash guard cannot write its record: {e.Message}");
        }
    }
}
