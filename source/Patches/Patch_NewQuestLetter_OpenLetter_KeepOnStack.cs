using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace BetterLetters.Patches
{
    /// Patch New Quest letters that don't show the dialog to prevent them from removing their letter from the stack 
    [HarmonyPatch]
    [HarmonyPatchCategory("Letter_RemoveLetter_KeepOnStack_QuestLetters")]
    [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class Patch_NewQuestLetter_OpenLetter_KeepOnStack
    {
        private static readonly MethodInfo? AnchorMethod = typeof(Find).GetProperty("LetterStack")?.GetGetMethod();
        private static readonly FieldInfo? QuestField = typeof(ChoiceLetter).GetField("quest");
        /// Transpiler that skips the line calling Find.LetterStack.RemoveLetter(this);
        /// Keeping letters in the letter stack when opened, instead of the vanilla functionality that automatically
        /// removes them.
        [HarmonyPatch(typeof(NewQuestLetter), nameof(NewQuestLetter.OpenLetter))]
        [HarmonyTranspiler]
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> OpenLetter(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(AnchorMethod))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, QuestField);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return CodeInstruction.CallClosure<Action<Quest, ChoiceLetter>>((quest, letter) =>
                    {
                        //TODO: Remove unused "quest" parameter and test it doesn't break everything
                        Find.LetterStack.RemoveLetter(letter);
                    });
                    // Replace the original call to RemoveLetter with a return to skip it, since we replaced it fully
                    yield return new CodeInstruction(OpCodes.Ret);
                }
                
                yield return codes[i];
            }
        }
    }
}
