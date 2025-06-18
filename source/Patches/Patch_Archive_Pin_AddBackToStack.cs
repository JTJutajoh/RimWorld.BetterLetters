using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;

namespace BetterLetters.Patches
{
    /// Relatively simple patch that adds letters back onto the LetterStack when they are pinned from the archive
    [HarmonyPatch]
    [HarmonyPatchCategory("ArchivePin_AddBackToStack")]
    [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class Patch_Archive_Pin_AddBackToStack
    {
        /// Catch when a letter is pinned in the vanilla archive and add it back to the stack
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Archive), nameof(Archive.Pin))]
        [UsedImplicitly]
        static void Pin(IArchivable archivable)
        {
            if (archivable is not Letter letter) return;
            
            var letterStack = Find.LetterStack;
            if (!letterStack.LettersListForReading.Contains(letter))
            {
                letterStack.ReceiveLetter(letter);
            }
            LetterUtils.SortLetterStackByPinned();
        }
    }
}