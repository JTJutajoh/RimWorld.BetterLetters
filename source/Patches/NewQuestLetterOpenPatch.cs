using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterLetters.Patches
{
    internal class NewQuestLetterOpenPatch
    {
        private static readonly MethodInfo? AnchorMethod = typeof(Find).GetProperty("LetterStack")?.GetGetMethod();
        private static readonly FieldInfo? QuestField = typeof(ChoiceLetter).GetField("quest");
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
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, QuestField);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return CodeInstruction.CallClosure<Action<Quest, ChoiceLetter>>((quest, letter) =>
                    {
                        if (!Settings.KeepQuestLettersOnStack)
                        {
                            Find.LetterStack.RemoveLetter(letter);
                        }
                        
                    });
                    // Just replace the original call to RemoveLetter with a return
                    yield return new CodeInstruction(OpCodes.Ret);
                }
                // Otherwise, emit the original IL instruction
                yield return codes[i];
            }
        }
    }
}
