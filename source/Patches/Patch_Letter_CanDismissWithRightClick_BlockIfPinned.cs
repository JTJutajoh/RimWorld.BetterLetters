using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;

namespace BetterLetters.Patches
{
    /// Patch that adds a bit of logic to the CanDismissWithRightClick property so that it returns false when the letter in question is pinned
    [HarmonyPatch]
    [HarmonyPatchCategory("Letter_CanDismissWithRightClick_BlockIfPinned")]
    [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class Patch_Letter_CanDismissWithRightClick_BlockIfPinned
    {
        [HarmonyPatch(typeof(Letter), "CanDismissWithRightClick", MethodType.Getter)]
        [HarmonyPostfix]
        [UsedImplicitly]
        static void CanDismissWithRightClick(ref bool __result, Letter __instance)
        {
            __result = __result && !__instance.IsPinned();
        }
    }
}
