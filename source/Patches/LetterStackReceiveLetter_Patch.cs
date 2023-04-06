using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using HarmonyLib;

namespace BetterLetters
{
    class LetterStackReceiveLetter_Patch
    {
        public static void ReceiveLetter(ref List<Letter> ___letters, Letter let)
        {
            ___letters = ___letters.OrderBy(obj => obj.IsPinned()).ToList();
        }
    }
}
