using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using BetterLetters.Patches;
using DarkLog;
using Verse;
using RimWorld;
using HarmonyLib;

namespace BetterLetters
{
    class BetterLettersMod : Verse.Mod
    {
        public static BetterLettersMod? Instance { get; private set; }

        public BetterLettersMod(ModContentPack content) : base(content)
        {
            LogPrefixed.modInst = this;
            Instance = this;
        }
    }

    [StaticConstructorOnStartup]
    internal static class LoadHarmony
    {
        private static Harmony _harmony;
        static LoadHarmony()
        {
            _harmony = new Harmony(BetterLettersMod.Instance!.Content.PackageId);

#if DEBUG
            Harmony.DEBUG = false; // For debugging transpilers
#endif

            LogPrefixed.Message("Running Harmony patches...");

            Patch_Vanilla();
            // Do any mod-specific patching (Vanilla Expanded...)

            LogPrefixed.Message("Harmony patching complete");
        }

        /// <summary>
        /// Contains all of the patches that should be run no matter what, regardless of what other mods are loaded
        /// </summary>
        static void Patch_Vanilla()
        {
            // The type that the target method belongs to
            var type = typeof(Verse.ChoiceLetter);
            // The class that the patches belong to
            var patchClass = typeof(RemoveLetterPatches);
            
            PostfixGetter(type, patchClass, "Option_Close");
            PostfixGetter(type, patchClass, "Option_JumpToLocation");
            PrefixMethod(type, patchClass, "Option_ViewInQuestsTab");

            type = typeof(Verse.DeathLetter);
            PostfixGetter(type, patchClass, "Option_ReadMore");

            // Patch new quest letters which don't show a dialog
            patchClass = typeof(NewQuestLetterOpenPatch);
            type = typeof(Verse.NewQuestLetter);
            TranspileMethod(type, patchClass, "OpenLetter");

            // Patch to track the current Letter since the Dialog doesn't know what Letter it is displaying info for (if any)
            patchClass = typeof(OpenLetterPatch);
            TranspileMethod(typeof(Verse.ChoiceLetter), patchClass, "OpenLetter");
            TranspileMethod(typeof(Verse.DeathLetter), patchClass, "OpenLetter");

            // Patch to clear any old letter reference when a dialog is opened
            patchClass = typeof(Dialog_NodeTreeConstructor);
            type = typeof(Dialog_NodeTree);
            _harmony.Patch(type.GetConstructor(new Type[] { typeof(DiaNode), typeof(bool), typeof(bool), typeof(string) }),
                postfix: GetPatch(patchClass, "ConstructorPostfix")
                );

            // Patch Archive to add newly-pinned letters back to the LetterStack
            patchClass = typeof(ArchivePinPatch);
            type = typeof(RimWorld.Archive);
            PostfixMethod(type, patchClass, "Pin");

            // Patch Letter buttons to draw the pin button and alter right click behavior
            patchClass = typeof(LetterCanDismissWithRightClickPatch);
            type = typeof(Letter);
            PostfixGetter(type, patchClass, "CanDismissWithRightClick");

            patchClass = typeof(LetterDrawingPatches);
            type = typeof(Letter);
            TranspileMethod(type, patchClass, "CheckForMouseOverTextAt");
            // Patching this one manually since we have multiple patches on the same method
            _harmony.Patch(
                type.GetMethod("DrawButtonAt", AccessTools.all),
                postfix: GetPatch(patchClass, "DrawButtonAt_Postfix"),
                transpiler: GetPatch(patchClass, "DrawButtonAt_Transpiler")
                );

            // Patch Dialog_NodeTree to add pin button
            patchClass = typeof(DialogDrawNodePatch);
            type = typeof(Dialog_NodeTree);
            PostfixMethod(typeof(Dialog_NodeTree), patchClass, "DoWindowContents");

            // Patch to sort pinned letters always on the bottom
            patchClass = typeof(LetterStackReceiveLetterPatch);
            type = typeof(LetterStack);

#if v1_1 || v1_2 || v1_3 || v1_4

            harmony.Patch(
                type.GetMethod("ReceiveLetter", new [] {typeof(Letter), typeof(string)}),
                postfix: GetPatch(patchClass, "ReceiveLetter")
                );
#elif v1_5 || v1_6
            _harmony.Patch(
                type.GetMethod("ReceiveLetter", new [] {typeof(Letter), typeof(string), typeof(int), typeof(bool)}),
                postfix: GetPatch(patchClass, "ReceiveLetter")
                );
#endif
        }

        static MethodInfo? GetGetter(Type t, string propName)
        {
            LogPrefixed.Trace($"Patching {propName} property getter");
            return t.GetProperty(propName, AccessTools.all)?.GetGetMethod(true) ?? null;
        }

        static HarmonyMethod GetPatch(Type t, string methodName)
        {
            LogPrefixed.Trace($"Patching {methodName} method");
            return new HarmonyMethod(t.GetMethod(methodName,AccessTools.all));
        }

        /// <summary>
        /// Automatically gets the appropriate methods and applies a postfix patch to a property getter
        /// </summary>
        /// <param name="t">The type to patch the property of</param>
        /// <param name="patchClass">A class containing patches with matching names to the desired properties</param>
        /// <param name="propName">The property to patch. Must match the method name in the patchClass.</param>
        private static void PostfixGetter(Type t, Type patchClass, string propName)
        {
            var original = GetGetter(t, propName);
            var patch = GetPatch(patchClass, propName);
            _harmony.Patch(original, postfix: patch);
        }

        private static void PrefixMethod(Type t, Type patchClass, string methodName)
        {
            _harmony.Patch(
                t.GetMethod(methodName, AccessTools.all),
                prefix: GetPatch(patchClass, methodName)
                );
        }

        private static void PostfixMethod(Type t, Type patchClass, string methodName)
        {
            _harmony.Patch(
                t.GetMethod(methodName, AccessTools.all),
                postfix: GetPatch(patchClass, methodName)
                );
        }

        private static void TranspileMethod(Type t, Type patchClass, string methodName)
        {
            _harmony.Patch(
                t.GetMethod(methodName, AccessTools.all),
                transpiler: GetPatch(patchClass, methodName)
                );
        }
    }
}