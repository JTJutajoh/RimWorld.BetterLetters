using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using HarmonyLib;

namespace BetterLetters
{
    class BetterLettersMod : Verse.Mod
    {
        public const string modID = "Dark.MarkLetterUnread";

        public BetterLettersMod(ModContentPack content) : base(content)
        {
            // sorry, nothing
        }
    }

    [StaticConstructorOnStartup]
    static class LoadHarmony
    {
        static LoadHarmony()
        {
            Harmony harmony = new Harmony(BetterLettersMod.modID);

            DFLog.Debug("Harmony patches patching...");

            harmony.PatchAll();
        }
    }
}