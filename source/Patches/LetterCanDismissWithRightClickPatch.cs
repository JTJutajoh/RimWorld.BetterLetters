using Verse;

namespace BetterLetters.Patches
{
    // Patch that adds a bit of logic to the CanDismissWithRightClick property so that it returns false when the letter in question is pinned
    class LetterCanDismissWithRightClickPatch
    {
        public static void CanDismissWithRightClick(ref bool __result, Letter __instance)
        {
            __result = !__instance.IsPinned();
        }
    }
}
