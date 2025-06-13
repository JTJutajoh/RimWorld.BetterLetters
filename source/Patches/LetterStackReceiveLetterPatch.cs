using System.Collections.Generic;
using System.Linq;
using Verse;

namespace BetterLetters.Patches
{
    class LetterStackReceiveLetterPatch
    {
        // Patch that sorts the letters in the letter stack by pinned status
        public static void ReceiveLetter(ref List<Letter> ___letters, Letter let)
        {
            ___letters = ___letters.OrderBy(obj => obj.IsPinned()).ToList();
        }
    }
}
