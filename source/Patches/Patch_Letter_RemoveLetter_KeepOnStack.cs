using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace BetterLetters.Patches
{
    /// A set of patches to disable the vanilla call to Find.LetterStack.RemoveLetter(this) in the vanilla Letter choices.<br />
    /// This is done by simply replacing the getter methods that return those Letter choices, with the only change being the omission of the above call.
    [HarmonyPatch]
    [HarmonyPatchCategory("Letter_RemoveLetter_KeepOnStack")]
    [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class Patch_Letter_RemoveLetter_KeepOnStack
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChoiceLetter), "Option_Close", MethodType.Getter)]
        [UsedImplicitly]
        static void Option_Close(ref DiaOption __result, Letter __instance)
        {
            __result.action = delegate { DismissIfNotPinned(__instance); };
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChoiceLetter), "Option_JumpToLocation", MethodType.Getter)]
        [UsedImplicitly]
        static void Option_JumpToLocation(ref DiaOption __result, ChoiceLetter __instance)
        {
            var target = __instance.lookTargets.TryGetPrimaryTarget();
            __result.action = delegate
            {
                DismissIfNotPinned(__instance);
                CameraJumper.TryJumpAndSelect(target);
            };
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DeathLetter), "Option_ReadMore", MethodType.Getter)]
        [UsedImplicitly]
        static void Option_ReadMore(ref DiaOption __result, DeathLetter __instance)
        {
            var target = __instance.lookTargets.TryGetPrimaryTarget();
            __result.action = delegate
            {
                DismissIfNotPinned(__instance);
                CameraJumper.TryJumpAndSelect(target);
                InspectPaneUtility.OpenTab(typeof(ITab_Pawn_Log));
            };
        }

        /// Utility function called by letter choices to alter behavior of all buttons to factor in the pinned state of the letter
        static void DismissIfNotPinned(Letter letter)
        {
            if (!letter.IsPinned())
                Find.LetterStack.RemoveLetter(letter);
        }

        /// Slightly different from the other methods since this one uses a normal method in vanilla rather than a Property getter
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChoiceLetter), "Option_ViewInQuestsTab")]
        [UsedImplicitly]
        static void Option_ViewInQuestsTab(ref bool postpone, Letter __instance)
        {
            if (!__instance.IsPinned())
                return;
            // The vanilla method already has the ability to do what we want if the "postpone" parameter is true. So just set it to always be true.
            postpone = Settings.KeepQuestLettersOnStack;
        }
    }
}