using Verse;

namespace BetterLetters.Patches;

/// <summary>
/// Harmony patch that marks snoozed letters as not being allowed to be culled from the archive
/// </summary>
public class LetterCanCullArchivedNowPatch
{
    // ReSharper disable InconsistentNaming
    public static void CanCullArchivedNow(ref bool __result, Letter __instance)
    // ReSharper restore InconsistentNaming
    {
        if (__instance.IsSnoozed())
        {
            __result = false;
        }
    }
}