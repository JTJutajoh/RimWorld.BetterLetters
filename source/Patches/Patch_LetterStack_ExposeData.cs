using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;

namespace BetterLetters.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("LetterIconCaching")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_LetterStack_ExposeData
{
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ExposeData))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void LetterIconsCacheExposeData()
    {
        LetterIconOverrides.ExposeData();
    }
}