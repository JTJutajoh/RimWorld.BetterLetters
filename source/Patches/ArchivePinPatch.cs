using RimWorld;
using Verse;

namespace BetterLetters.Patches
{
    // Relatively simple patch that adds letters back onto the LetterStack when they are pinned from the archive
    internal class ArchivePinPatch
    {
        public static void Pin(IArchivable archivable)
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