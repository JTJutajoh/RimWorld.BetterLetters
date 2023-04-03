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
                //TODO: Put in some sort of whitelist/blacklist here to exclude certain letters from this behavior (Like growth moments)
                LetterStack letterStack = Find.LetterStack;
                if (!letterStack.LettersListForReading.Contains(letter))
                {
                    letterStack.ReceiveLetter(letter);
                }
            }
        }
    }
}