using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;

namespace BetterLetters
{
    class Dialog_NodeTreeConstructor
    {
        public static void ConstructorPostfix()
        {
            // This is a very simple patch. All it does is clear the stored reference to the current letter so that any non-letter dialogs don't display a pin button when they shouldn't.
            DialogDrawNode_Patch.curLetter = null;
        }
    }
}
