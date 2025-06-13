using RimWorld;
using Verse;

namespace BetterLetters.Patches
{
    // Relatively simple patch that adds letters back onto the LetterStack when they are pinned from the archive
    class ArchivePinPatch
    {
        public static void Pin(IArchivable archivable)
        {
            if (archivable is Letter letter)
            {
                LetterStack letterStack = Find.LetterStack;
                if (!letterStack.LettersListForReading.Contains(letter))
                {
                    letterStack.ReceiveLetter(letter);
                }
                LetterUtils.SortLetterStackByPinned();
            }
        }
    }
}