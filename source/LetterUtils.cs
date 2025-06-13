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
    internal static class LetterUtils
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

        private static readonly FieldInfo? LettersField = typeof(LetterStack).GetField("letters", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void SortLetterStackByPinned()
        {
            var letters = (List<Letter>)(LettersField?.GetValue(Find.LetterStack) ?? new List<Letter>());
            letters = letters.OrderBy(obj => obj.IsPinned()).ToList();
            LettersField?.SetValue(Find.LetterStack, letters);
        }
    }
}