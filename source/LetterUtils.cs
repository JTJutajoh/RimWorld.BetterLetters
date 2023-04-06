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
    static class LetterUtils
    {
        public static bool IsPinned(this Letter letter)
        {
            return Find.Archive.IsPinned(letter);
        }

        public static void Pin(this Letter letter)
        {
            Find.Archive.Pin(letter);
        }

        public static void Unpin(this Letter letter)
        {
            Find.Archive.Unpin(letter);
        }
    }
}