using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;

namespace BetterLetters.Patches
{
    /// Patch that sorts the letters in the letter stack by pinned status whenever a new letter is received
    [HarmonyPatch]
    [HarmonyPatchCategory("LetterStack_SortPinned")]
    [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class Patch_LetterStack_ReceiveLetter_SortPinned
    {
#if v1_1 || v1_2 || v1_3 || v1_4
        // LetterStack.ReceiveLetter method signature changed in 1.5
        [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string) })]
#else
        [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
#endif
        [HarmonyPostfix]
        [UsedImplicitly]
        static void ReceiveLetter(ref List<Letter> ___letters, Letter let)
        {
            ___letters = ___letters.OrderBy(obj => obj.IsPinned()).ToList();
        }
    }
}