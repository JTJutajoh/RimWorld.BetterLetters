using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace BetterLetters
{
    // A set of patches to disable the vanilla call to Find.LetterStack.RemoveLetter(this) in the vanilla Letter choices.
    // This is done by simply replacing the getter methods that return those Letter choices, with the only change being the omission of the above call.
    
    class RemoveLetter_Patches
    {
        public static void Option_Close(ref DiaOption __result)
        {
            __result.action = null;
        }
        
        public static void Option_JumpToLocation(ref DiaOption __result, ChoiceLetter __instance)
        {
            GlobalTargetInfo target = __instance.lookTargets.TryGetPrimaryTarget();
            __result.action = delegate ()
            {
                CameraJumper.TryJumpAndSelect(target, CameraJumper.MovementMode.Pan);
            };
        }

        public static void Option_ViewInQuestsTab(ref bool postpone)
        {
            // The vanilla method already has the ability to do what we want if the "postpone" parameter is true. So just set it to always be true.
            postpone = true;
        }

        public static void Option_ReadMore(ref DiaOption __result, DeathLetter __instance)
        {
            GlobalTargetInfo target = __instance.lookTargets.TryGetPrimaryTarget();
            __result.action = delegate ()
            {
                CameraJumper.TryJumpAndSelect(target, CameraJumper.MovementMode.Pan);
                InspectPaneUtility.OpenTab(typeof(ITab_Pawn_Log));
            };
        }
    }
}
