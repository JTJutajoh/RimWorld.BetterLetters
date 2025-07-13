using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace BetterLetters.Patches;

/// <summary>
/// Patches associated with <see cref="LetterIconOverrides"/><br />
/// Performs a few tasks:
/// caches letters as soon as they're received,
/// injects cache into
/// <see cref="LetterStack"/>'s <see cref="LetterStack.ExposeData"/>,
/// and has a bunch of small patches that call <see cref="LetterIconOverrides.TryOverrideMostRecentLetterIcon"/> after
/// a letter has been sent by various sources.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("LetterIconCaching")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_LetterStack_ReceiveLetter_CacheLetter
{
    // LetterStack.ReceiveLetter signature changed in RW 1.5+ so target method needs to be calculated dynamically
    [UsedImplicitly]
    static MethodBase? TargetMethod()
    {
        if ((LegacySupport.CurrentRWVersion & (RWVersion.v1_5 | RWVersion.v1_6)) != 0)
            return AccessTools.Method(
                typeof(LetterStack),
                nameof(LetterStack.ReceiveLetter),
                new[]
                {
                    typeof(Letter),
                    typeof(string),
                    typeof(int),
                    typeof(bool)
                });
        if ((LegacySupport.CurrentRWVersion &
             (RWVersion.v1_0 | RWVersion.v1_1 | RWVersion.v1_2 | RWVersion.v1_3 | RWVersion.v1_4)) != 0)
            return AccessTools.Method(
                typeof(LetterStack),
                nameof(LetterStack.ReceiveLetter),
                new[]
                {
                    typeof(Letter),
                    typeof(string)
                });

        Log.Error(
            "Unknown RimWorld version, cannot patch LetterStack.ReceiveLetter. If RimWorld has recently updated, update LegacySupport.cs");
        return null;
    }

    /// <summary>
    /// Patch that catches every letter added to the <see cref="LetterStack"/> and saves it to <see cref="LetterIconOverrides.MostRecentLetter"/>
    /// to be used in the other patches in this class.
    /// </summary>
    /// <param name="let">The letter that was just added to the stack</param>
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheMostRecentLetter(Letter let)
    {
        LetterIconOverrides.MostRecentLetter = let;
    }
}
