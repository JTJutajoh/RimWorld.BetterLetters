using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using UnityEngine;

namespace BetterLetters
{
    // Patch that adds a bit of logic to the CanDismissWithRightClick property so that it returns false when the letter in question is pinned
    class LetterCanDismissWithRightClick_Patch
    {
        public static void CanDismissWithRightClick(ref bool __result, Letter __instance)
        {
            __result = !__instance.IsPinned();
        }
    }
}
