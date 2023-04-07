using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection;

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
            Archive archive = Find.Archive;
            if (!archive.Contains(letter))
            {
                archive.Add(letter);
            }
            archive.Pin(letter);
            SortLetterStackByPinned();
        }

        public static void Unpin(this Letter letter)
        {
            Find.Archive.Unpin(letter);
            SortLetterStackByPinned();
        }

        static FieldInfo lettersField = typeof(LetterStack).GetField("letters", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void SortLetterStackByPinned()
        {
            List<Letter> letters = (List<Letter>)lettersField.GetValue(Find.LetterStack);
            letters = letters.OrderBy(obj => obj.IsPinned()).ToList();
            lettersField.SetValue(Find.LetterStack, letters);
        }
    }
}