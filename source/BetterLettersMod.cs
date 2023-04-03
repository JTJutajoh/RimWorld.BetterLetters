using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Verse;
using RimWorld;
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
            // The type that the target method belongs to
            Type type;
            // The class that the patches belong to
            Type patchClass;

            // Patch the vanilla choice getters
            patchClass = typeof(RemoveLetter_Patches);
            type = typeof(Verse.ChoiceLetter);
            PostfixGetter(type, patchClass, "Option_Close");
            PostfixGetter(type, patchClass, "Option_JumpToLocation");
            PrefixMethod(type, patchClass, "Option_ViewInQuestsTab");

            type = typeof(Verse.DeathLetter);
            PostfixGetter(type, patchClass, "Option_ReadMore");

            // Patch new quest letters which don't show a dialog
            patchClass = typeof(NewQuestLetterOpen_Patch);
            type = typeof(Verse.NewQuestLetter);
            TranspileMethod(type, patchClass, "OpenLetter");

            // Patch in a new Dismiss and Pin choices to letters
            patchClass = typeof(OpenLetter_Patch);
            TranspileMethod(typeof(Verse.ChoiceLetter), patchClass, "OpenLetter");
            TranspileMethod(typeof(Verse.DeathLetter), patchClass, "OpenLetter");

            // Patch to clear any old letter reference when a dialog is opened
            patchClass = typeof(Dialog_NodeTreeConstructor);
            type = typeof(Dialog_NodeTree);
            harmony.Patch(type.GetConstructor(new Type[] { typeof(DiaNode), typeof(bool), typeof(bool), typeof(string) }),
                postfix: GetPatch(patchClass, "ConstructorPostfix")
                );

            // Patch Archive to add newly-pinned letters back to the LetterStack
            patchClass = typeof(ArchivePin_Patch);
            type = typeof(RimWorld.Archive);
            PostfixMethod(type, patchClass, "Pin");

            // Patch Letter buttons to draw the pin button and alter right click behavior
            patchClass = typeof(LetterCanDismissWithRightClick_Patch);
            type = typeof(Letter);
            PostfixGetter(type, patchClass, "CanDismissWithRightClick");

            patchClass = typeof(LetterDrawingPatches);
            type = typeof(Letter);
            TranspileMethod(type, patchClass, "CheckForMouseOverTextAt");
            // Patching this one manually since we have multiple patches on the same method
            harmony.Patch(
                type.GetMethod("DrawButtonAt", AccessTools.all),
                postfix: GetPatch(patchClass, "DrawButtonAt_Postfix"),
                transpiler: GetPatch(patchClass, "DrawButtonAt_Transpiler")
                );

            // Patch Dialog_NodeTree to add pin button
            patchClass = typeof(DialogDrawNode_Patch);
            type = typeof(Dialog_NodeTree);
            TranspileMethod(type, patchClass, "DrawNode");
        }

        static MethodInfo GetGetter(Type t, string propName)
        {
            return t.GetProperty(propName, AccessTools.all).GetGetMethod(true);
        }

        static HarmonyMethod GetPatch(Type t, string methodName)
        {
            return new HarmonyMethod(t.GetMethod(methodName,AccessTools.all));
        }

        /// <summary>
        /// Automatically gets the appropriate methods and applies a postfix patch to a property getter
        /// </summary>
        /// <param name="t">The type to patch the property of</param>
        /// <param name="patchClass">A class containing patches with matching names to the desired properties</param>
        /// <param name="propName">The property to patch. Must match the method name in the patchClass.</param>
        static void PostfixGetter(Type t, Type patchClass, string propName)
        {
            var original = GetGetter(t, propName);
            var patch = GetPatch(patchClass, propName);
            harmony.Patch(original, postfix: patch);
        }

        static void PrefixMethod(Type t, Type patchClass, string methodName)
        {
            harmony.Patch(
                t.GetMethod(methodName, AccessTools.all),
                prefix: GetPatch(patchClass, methodName)
                );
        }

        static void PostfixMethod(Type t, Type patchClass, string methodName)
        {
            harmony.Patch(
                t.GetMethod(methodName, AccessTools.all),
                postfix: GetPatch(patchClass, methodName)
                );
        }

        static void TranspileMethod(Type t, Type patchClass, string methodName)
        {
            harmony.Patch(
                t.GetMethod(methodName, AccessTools.all),
                transpiler: GetPatch(patchClass, methodName)
                );
        }
    }
}