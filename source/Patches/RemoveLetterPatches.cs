using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BetterLetters.Patches
{
    /// A set of patches to disable the vanilla call to Find.LetterStack.RemoveLetter(this) in the vanilla Letter choices.<br />
    /// This is done by simply replacing the getter methods that return those Letter choices, with the only change being the omission of the above call.

    internal class RemoveLetterPatches
    {
        // ReSharper disable InconsistentNaming
        public static void Option_Close(ref DiaOption __result, Letter __instance)
        // ReSharper restore InconsistentNaming
        {
            __result.action = delegate
            {
                DismissIfNotPinned(__instance);
            };
        }
        
        // ReSharper disable InconsistentNaming
        public static void Option_JumpToLocation(ref DiaOption __result, ChoiceLetter __instance)
        // ReSharper restore InconsistentNaming
        {
            var target = __instance.lookTargets.TryGetPrimaryTarget();
            __result.action = delegate ()
            {
                DismissIfNotPinned(__instance);
#if v1_4 || v1_5 || v1_6
                CameraJumper.TryJumpAndSelect(target, CameraJumper.MovementMode.Pan);
#elif v1_3
                CameraJumper.TryJumpAndSelect(target);
#endif
            };
        }

        // ReSharper disable InconsistentNaming
        public static void Option_ReadMore(ref DiaOption __result, DeathLetter __instance)
        // ReSharper restore InconsistentNaming
        {
            var target = __instance.lookTargets.TryGetPrimaryTarget();
            __result.action = delegate ()
            {
                DismissIfNotPinned(__instance);
#if v1_4 || v1_5 || v1_6
                CameraJumper.TryJumpAndSelect(target, CameraJumper.MovementMode.Pan);
#elif v1_3
                CameraJumper.TryJumpAndSelect(target);
#endif
                InspectPaneUtility.OpenTab(typeof(ITab_Pawn_Log));
            };
        }

        /// Utility function called by letter choices to alter behavior of all buttons to factor in the pinned state of the letter
        private static void DismissIfNotPinned(Letter letter)
        {
            if (!letter.IsPinned())
                Find.LetterStack.RemoveLetter(letter);
        }

        /// Slightly different from the other methods since this one uses a normal method in vanilla rather than a Property getter
        // ReSharper disable InconsistentNaming
        public static void Option_ViewInQuestsTab(ref bool postpone, Letter __instance)
        // ReSharper restore InconsistentNaming
        {
            if (!__instance.IsPinned())
                return;
            // The vanilla method already has the ability to do what we want if the "postpone" parameter is true. So just set it to always be true.
            postpone = true;
        }
    }
}