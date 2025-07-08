using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;

namespace BetterLetters.Patches;

/// Patch New Quest letters that don't show the dialog to prevent them from removing their letter from the stack
[HarmonyPatch]
[HarmonyPatchCategory("Letter_RemoveLetter_KeepOnStack_QuestLetter")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_NewQuestLetter_OpenLetter_KeepOnStack
{
    [UsedImplicitly]
    static bool Prepare()
    {
        if (LegacySupport.CurrentRWVersion < RWVersion.v1_3)
        {
            Log.Warning($"{nameof(Patch_HistoryTab_DoArchivableRow)} requires RimWorld 1.3+.\nQuest letters will not stay on the stack when opened.");
            return false;
        }
        return true;
    }

    private static readonly MethodInfo? LetterStackGetterAnchor =
        typeof(Find).GetProperty("LetterStack")?.GetGetMethod();

    /// Transpiler that skips the line calling Find.LetterStack.RemoveLetter(this);
    /// Keeping letters in the letter stack when opened, instead of the vanilla functionality that automatically
    /// removes them.
    [HarmonyPatch(typeof(NewQuestLetter), nameof(NewQuestLetter.OpenLetter))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    public static IEnumerable<CodeInstruction> OpenLetter(IEnumerable<CodeInstruction> instructions)
    {
        if (LetterStackGetterAnchor == null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(LetterStackGetterAnchor)} method for {nameof(Patch_NewQuestLetter_OpenLetter_KeepOnStack)}.{MethodBase.GetCurrentMethod()} patch");

        var codes = new List<CodeInstruction>(instructions);
        // ReSharper disable once ForCanBeConvertedToForeach
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i]!.Calls(LetterStackGetterAnchor))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return CodeInstruction.CallClosure<Action<ChoiceLetter>>((letter) =>
                {
                    if (Settings.KeepQuestLettersOnStack) return;
                    Find.LetterStack?.RemoveLetter(letter);
                })!;
                // Replace the original call to RemoveLetter with a return to skip it
                yield return new CodeInstruction(OpCodes.Ret);
            }

            yield return codes[i]!;
        }
    }
}
