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
    static class LogPrefixed
    {
        static string Prefix { get => "[" + BetterLettersMod.Instance.Content.PackageIdPlayerFacing + "] "; }
        static string PrefixColor => "cyan";

        static string PrefixedMessage(string message) => $"<color={PrefixColor}>{Prefix}</color> {message}";

        static LogPrefixed()
        {
#if DEBUG
            Error("DEBUG LOGSPAM ENABLED!");
#endif
        }

        public static void Error(string text)
        {
            Log.Error(PrefixedMessage(text));
        }

        public static void ErrorOnce(string text, int key)
        {
            Log.ErrorOnce(PrefixedMessage(text), key);
        }

        public static void Warning(string text)
        {
            Log.Warning(PrefixedMessage(text));
        }

#if v1_4
        public static void WarningOnce(string text, int key)
        {
            Log.WarningOnce(PrefixedMessage(text), key);
        }
#endif

        public static void Message(string text)
        {
            Log.Message(PrefixedMessage(text));
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
                Warning(PrefixedMessage(text));
            else
                Message(PrefixedMessage(text));
#endif
            return;
        }
    }
}
