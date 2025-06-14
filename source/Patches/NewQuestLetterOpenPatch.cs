using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace BetterLetters.Patches
{
    internal class NewQuestLetterOpenPatch
    {
        private static readonly MethodInfo? AnchorMethod = typeof(Find).GetProperty("LetterStack")?.GetGetMethod();
        /// Transpiler that skips the line calling Find.LetterStack.RemoveLetter(this);
        /// Keeping letters in the letter stack when opened, instead of the vanilla functionality that automatically
        /// removes them.
        public static IEnumerable<CodeInstruction> OpenLetter(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(AnchorMethod))
                {
                    // Skip the next 3 ILs:
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
