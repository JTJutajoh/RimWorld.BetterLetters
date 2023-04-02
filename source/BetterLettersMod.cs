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
        public const string modID = "Dark.BetterLetters";

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

            Patch_Vanilla();
            // Do any mod-specific patching (Vanilla Expanded...)
        }

        /// <summary>
        /// Contains all of the patches that should be run no matter what, regardless of what other mods are loaded
        /// </summary>
        static void Patch_Vanilla()
        {
            // Patch the vanilla choice getters
            var patchClass = typeof(RemoveLetter_Patches);

            var type = typeof(Verse.ChoiceLetter);
            PostfixPatchGetter(type, patchClass, "Option_Close");
            PostfixPatchGetter(type, patchClass, "Option_JumpToLocation");
            PrefixPatchMethod(type, patchClass, "Option_ViewInQuestsTab");

            type = typeof(Verse.DeathLetter);
            PostfixPatchGetter(type, patchClass, "Option_ReadMore");

            // Patch new quest letters which don't show a dialog
            patchClass = typeof(NewQuestLetterOpen_Patch);

            type = typeof(Verse.NewQuestLetter);
            TranspilePatchMethod(type, patchClass, "OpenLetter");

            // Patch in a new Dismiss choice to letters
            patchClass = typeof(ChoiceLetterOpenLetter_Patch);

            type = typeof(Verse.ChoiceLetter);
            TranspilePatchMethod(type, patchClass, "OpenLetter");
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

        static void PrefixPatchMethod(Type t, Type patchClass, string methodName)
        {
            harmony.Patch(
                t.GetMethod(methodName, AccessTools.all),
                prefix: GetPatch(patchClass, methodName)
                );
        }

        static void TranspilePatchMethod(Type t, Type patchClass, string methodName)
        {
            harmony.Patch(
                t.GetMethod(methodName, AccessTools.all),
                transpiler: GetPatch(patchClass, methodName)
                );
        }
    }
}