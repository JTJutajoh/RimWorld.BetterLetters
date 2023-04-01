using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace BetterLetters
{
    /// <summary>
    /// A utility class for my mods that helps automate some debug logging stuff.
    /// Mostly just a wrapper for Verse.Log
    /// </summary>
    [StaticConstructorOnStartup]
    static class DFLog
    {
        static string Prefix { get => "[" + BetterLettersMod.modID + "] "; }

        static DFLog()
        {
#if DEBUG
            Error("DEBUG DEFINED! You forgot to build in a Release config!");
#endif
        }

        public static void Error(string text)
        {
            Log.Error(Prefix + text);
        }

        public static void ErrorOnce(string text, int key)
        {
            Log.ErrorOnce(Prefix + text, key);
        }

        public static void Warning(string text)
        {
            Log.Warning(Prefix + text);
        }

        public static void WarningOnce(string text, int key)
        {
            Log.WarningOnce(Prefix + text, key);
        }

        public static void Message(string text)
        {
            Log.Message(Prefix + text);
        }

        /// <summary>
        /// Sends a debug message, but only if project was built with the DEBUG constant defined.
        /// Use this for logspam.
        /// </summary>
        /// <param name="warning">If true, sends as a yellow warning message for visibility.</param>
        public static void Debug(string text, bool warning=false)
        {
#if DEBUG
            if (warning)
                Warning(text);
            else
                Message(text);
#endif
            return;
        }
    }
}
