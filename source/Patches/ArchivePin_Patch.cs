using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace BetterLetters
{
    // Relatively simple patch that adds letters back onto the LetterStack when they are pinned from the archive
    class ArchivePin_Patch
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
            }
        }
    }
}