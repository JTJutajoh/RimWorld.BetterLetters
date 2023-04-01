using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace BetterLetters
{
    class NewQuestLetterOpen_Patch
    {
        static MethodInfo anchorMethod = typeof(Find).GetProperty("LetterStack").GetGetMethod();
        // Transpiler that skips the line calling Find.LetterStack.RemoveLetter(this);
        public static IEnumerable<CodeInstruction> OpenLetter(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(anchorMethod))
                {
                    // Skip the next 3 ILs
                    // IL_003a: call class Verse.LetterStack Verse.Find::get_LetterStack()
                    // IL_003f: ldarg.0
                    // IL_0040: callvirt instance void Verse.LetterStack::RemoveLetter(class Verse.Letter)
                    i += 3;
                }
                // Otherwise, emit the original IL instruction
                yield return codes[i];
            }
        }
    }
}
