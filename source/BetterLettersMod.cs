using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
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
        static Harmony harmony;
        static LoadHarmony()
        {
            harmony = new Harmony(BetterLettersMod.modID);

            DFLog.Debug("Harmony patches patching...");

            // Patch the vanilla choice getters
            var patchClass = typeof(RemoveLetter_Patches);
            var type = typeof(Verse.ChoiceLetter);

            PostfixPatchGetter(type, patchClass, "Option_Close");
            PostfixPatchGetter(type, patchClass, "Option_JumpToLocation");
            PrefixPatchGetter(type, patchClass, "Option_ViewInQuestsTab");

            type = typeof(Verse.DeathLetter);
            PostfixPatchGetter(type, patchClass, "Option_ReadMore");
        }

        static MethodInfo GetGetter(Type t, string propName)
        {
            return t.GetProperty(propName, AccessTools.all).GetGetMethod(true);
        }

        static HarmonyMethod GetPatch(Type t, string methodName)
        {
            return new HarmonyMethod(t.GetMethod(methodName));
        }

        /// <summary>
        /// Automatically gets the appropriate methods and applies a postfix patch to a property getter
        /// </summary>
        /// <param name="t">The type to patch the property of</param>
        /// <param name="patchClass">A class containing patches with matching names to the desired properties</param>
        /// <param name="propName">The property to patch. Must match the method name in the patchClass.</param>
        static void PostfixPatchGetter(Type t, Type patchClass, string propName)
        {
            var original = GetGetter(t, propName);
            var patch = GetPatch(patchClass, propName);
            harmony.Patch(original, postfix: patch);
        }
        /// <summary>
        /// Automatically gets the appropriate methods and applies a prefix patch to a property getter
        /// </summary>
        /// <param name="t">The type to patch the property of</param>
        /// <param name="patchClass">A class containing patches with matching names to the desired properties</param>
        /// <param name="propName">The property to patch. Must match the method name in the patchClass.</param>
        static void PrefixPatchGetter(Type t, Type patchClass, string propName)
        {
            var original = GetGetter(t, propName);
            var patch = GetPatch(patchClass, propName);
            harmony.Patch(original, prefix: patch);
        }
    }
}