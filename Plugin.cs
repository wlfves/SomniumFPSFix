using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SLua;

namespace SomniumFPSFix;

[BepInPlugin("somniumfpsfix.mods.wlf.moe", "Somnium FPS Fix", "1.2.0")]
[BepInProcess("AI_TheSomniumFiles.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static Plugin Instance;
    internal static new ManualLogSource Logger;

    internal static ConfigEntry<bool>   FixEnabled;
    internal static ConfigEntry<string> FixModuleName;
    internal static ConfigEntry<int>    ExpectedOriginalLength;
    internal static ConfigEntry<bool>   FallbackInMemoryPatch;

    // ------------------------------------------------------------------
    // What this mod fixes
    //
    // Inside a Somnium, time slows to a crawl while you stand still and runs at
    // normal speed while you move. The game tracks this with a single "movement
    // amount" number that eases between 0 (standing still) and 1 (moving), and
    // feeds that number into the Somnium's world time scale, stop effect, and audio.
    //
    // Every frame, the original script also runs this line:
    //
    //     amount = (amount + MIN) / (1 - MIN)      -- MIN = 0.01
    //
    // which feeds the number back into itself, slightly larger, once per frame
    // (it grows by about 1% each frame). At normal frame rates the separate
    // per-frame easing is strong enough to cancel that growth out. But above
    // ~100 FPS each frame covers so little time that the easing can no longer
    // keep up, so the number compounds every frame and runs away to infinity --
    // the game then overflows and crashes.
    //
    // The fix (see LuaPatch.cs):
    //   1. Remove the line that feeds the number back into itself.
    //   2. Set the standing-still and moving targets directly to the values the
    //      original would have settled on anyway:
    //        standing still -> 0.010101...   (a slow crawl, not a full stop)
    //        moving         -> 1.020202...
    // The result feels identical to the original game at normal frame rates and
    // can no longer run away at high frame rates. The fix is applied to the
    // script in memory as the game loads it -- no game files are read from or
    // written to disk, and none are shipped with this mod.
    //
    // Fallback: if that fix cannot be applied (for example, a future game update
    // changes the script), the mod instead blanks out just the runaway line in
    // the raw script. That still prevents the crash, but standing still becomes a
    // full freeze (and the rain sound goes silent) rather than the original slow
    // crawl. It is a safety net only.
    // ------------------------------------------------------------------

    private void Awake()
    {
        Instance = this;
        Logger   = base.Logger;

        FixEnabled = Config.Bind(
            "Fix", "Enabled", true,
            "Apply the high-FPS slow-motion fix to the Somnium script (the 'somnium' module).");

        FixModuleName = Config.Bind(
            "Fix", "ModuleName", "somnium",
            "Name of the script module to patch. The Somnium engine is 'somnium'; you should "
          + "not normally need to change this.");

        ExpectedOriginalLength = Config.Bind(
            "Fix", "ExpectedOriginalLength", 220953,
            "Size in bytes of the original Somnium script this fix was built for. The fix is "
          + "only applied when the script matches this size, so a future game update can't "
          + "have an out-of-date patch forced onto it. Set to 0 to skip this check (not "
          + "recommended).");

        FallbackInMemoryPatch = Config.Bind(
            "Fix", "FallbackInMemoryPatch", true,
            "If the main fix can't be applied (for example after a game update), fall back "
          + "to blanking out just the runaway line. This still prevents the crash, but "
          + "standing still becomes a full freeze (and the rain sound goes silent) instead "
          + "of the original slow crawl.");

        ModNotification.Register();
        new Harmony("somniumfpsfix.mods.wlf.moe").PatchAll();
        Logger.LogInfo(
            $"Somnium FPS Fix {Info.Metadata.Version} loaded. Fix={FixEnabled.Value} "
          + $"module='{FixModuleName.Value}' fallback={FallbackInMemoryPatch.Value}");
    }

    // Returns every position where 'needle' appears inside 'hay'.
    // (Written without LINQ to stay compatible with the game's older .NET runtime.)
    internal static int[] FindAll(byte[] hay, byte[] needle)
    {
        var hits = new List<int>();
        if (hay == null || needle == null || needle.Length == 0 || hay.Length < needle.Length)
            return hits.ToArray();

        int last = hay.Length - needle.Length;
        for (int i = 0; i <= last; i++)
        {
            int j = 0;
            for (; j < needle.Length; j++)
                if (hay[i + j] != needle[j]) break;
            if (j == needle.Length) hits.Add(i);
        }
        return hits.ToArray();
    }
}

// The game loads each Lua script module through LuaState.loadFile, which returns
// the script's raw bytes (already decrypted) just before the engine compiles them.
// Hooking here lets us hand back a fixed copy of the Somnium script without ever
// touching the game's files on disk.
[HarmonyPatch(typeof(LuaState), "loadFile")]
internal static class Patch_loadFile
{
    private static void Postfix(string fn, ref byte[] __result)
    {
        try
        {
            if (__result == null) return;
            if (!Plugin.FixEnabled.Value) return;

            // Only touch the Somnium engine module; leave every other script alone.
            if (!string.Equals(fn, Plugin.FixModuleName.Value, StringComparison.Ordinal)) return;

            // Compiled Lua starts with the byte 0x1B. The fallback below only makes
            // sense on compiled bytecode.
            bool isBytecode = __result.Length > 0 && __result[0] == 0x1B;

            // ---- Main fix: rewrite the script in memory ----
            int exp = Plugin.ExpectedOriginalLength.Value;
            if (exp > 0 && __result.Length != exp)
            {
                Plugin.Logger.LogWarning(
                    $"Somnium script '{fn}' is {__result.Length} bytes (expected {exp}); the game "
                  + "version may differ from the one this fix was built for. Trying the fallback.");
            }
            else
            {
                try
                {
                    __result = LuaPatch.Apply(__result);
                    Plugin.Logger.LogInfo($"Applied FPS fix to '{fn}' ({__result.Length} bytes).");
                    return;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning(
                        $"Main fix failed for '{fn}': {ex.Message} -- trying the fallback.");
                }
            }

            // ---- Fallback: blank out just the runaway line ----
            if (Plugin.FallbackInMemoryPatch.Value && isBytecode)
            {
                int[] hits = Plugin.FindAll(__result, LuaPatch.FeedbackPattern);
                if (hits.Length == 1)
                {
                    byte[] patched = (byte[])__result.Clone();
                    int off = hits[0];
                    for (int k = 0; k < LuaPatch.FeedbackPattern.Length; k++)
                        patched[off + k] = 0x00;
                    __result = patched;
                    Plugin.Logger.LogWarning(
                        $"Applied fallback fix to '{fn}' (full-stop variant): blanked the runaway "
                      + $"line at offset {off}.");
                    return;
                }

                Plugin.Logger.LogWarning(
                    $"Fallback could not safely locate the runaway line in '{fn}' (found "
                  + $"{hits.Length} matches, expected 1); leaving it unpatched. The game version "
                  + "likely differs from the one this fix was built for.");
                return;
            }

            Plugin.Logger.LogError(
                $"'{fn}' was not patched (main fix failed and the fallback is off or doesn't apply).");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Failed while patching '{fn}': {ex.Message}");
        }
    }
}
