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
using UnityEngine;

namespace BetterLetters
{
    class BetterLettersMod : Verse.Mod
    {
        public static BetterLettersMod? Instance { get; private set; }

        public BetterLettersMod(ModContentPack content) : base(content)
        {
            Instance = this;
            LogPrefixed.Initialize(this, "cyan");

            GetSettings<Settings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            try
            {
                base.DoSettingsWindowContents(inRect);
                GetSettings<Settings>().DoWindowContents(inRect);
            }
            catch (Exception e)
            {
                LogPrefixed.Exception(e, "Error drawing mod settings window.", true);
            }
        }

        public override string SettingsCategory()
        {
            return "BetterLetters_SettingsCategory".Translate();
        }
    }

    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method)]
    public class ReloadableAttribute : Attribute { }

    [StaticConstructorOnStartup]
    internal static class LoadHarmony
    {
        internal static readonly Harmony Harmony;

        static LoadHarmony()
        {
            Harmony = new Harmony(BetterLettersMod.Instance!.Content.PackageId);

#if DEBUG
            // Harmony.DEBUG = true; // For debugging transpilers. DO NOT uncomment this unless you need it!
#endif

            LogPrefixed.Message("Running Harmony patches...");

            try
            {
                Patch_Vanilla();
            }
            catch (Exception e)
            {
                LogPrefixed.Exception(e,
                    "Error patching vanilla. This likely means either the wrong game version or a hard incompatibility with another mod.");
            }
            // Do any mod-specific patching (Vanilla Expanded...)

            LogPrefixed.Message("Harmony patching complete");
        }

        /// <summary>
        /// Contains all of the patches that should be run no matter what, regardless of what other mods are loaded
        /// </summary>
        private static void Patch_Vanilla()
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
            LogPrefixed.Trace("Manually patching Dialog_NodeTree Constructor");
            Harmony.Patch(
                type.GetConstructor(new Type[] { typeof(DiaNode), typeof(bool), typeof(bool), typeof(string) }),
                postfix: GetPatch(patchClass, "ConstructorPostfix")
            );

            // Patch Archive to add newly pinned letters back to the LetterStack
            patchClass = typeof(ArchivePinPatch);
            type = typeof(RimWorld.Archive);
            PostfixMethod(type, patchClass, "Pin");

            // Patch Letter buttons to draw the pin button and alter right-click behavior
            patchClass = typeof(LetterCanDismissWithRightClickPatch);
            type = typeof(Letter);
            PostfixGetter(type, patchClass, "CanDismissWithRightClick");

            patchClass = typeof(LetterDrawingPatches);
            type = typeof(Letter);
            TranspileMethod(type, patchClass, "CheckForMouseOverTextAt");
            // Patching this one manually since we have multiple patches on the same method
            LogPrefixed.Trace("Manually patching DrawButtonAt");
            Harmony.Patch(
                type.GetMethod("DrawButtonAt", AccessTools.all),
                postfix: GetPatch(patchClass, "DrawButtonAt_Postfix"),
                transpiler: GetPatch(patchClass, "DrawButtonAt_Transpiler")
            );

            // Patch letters to not allow culling if they are snoozed
            patchClass = typeof(LetterCanCullArchivedNowPatch);
            type = typeof(Letter);
            PostfixGetter(type, patchClass, "CanCullArchivedNow", interfaceType: typeof(IArchivable));

            // Patch the History window to show snoozed icon for snoozed letters
            patchClass = typeof(HistoryDoArchivableRowPatch);
            type = typeof(MainTabWindow_History);
            TranspileMethod(type, patchClass, "DoArchivableRow");
            
            // Patches to add the reminders menu
            patchClass = typeof(HistoryRemindersTabPatch);
            type = typeof(MainTabWindow_History);
            // Patching this one manually since we have multiple patches on the same method
            LogPrefixed.Trace("Manually patching DoMessagesPage");
            Harmony.Patch(
                type.GetMethod("DoMessagesPage", AccessTools.all),
                prefix: new HarmonyMethod(HistoryRemindersTabPatch.DoMessagesPage_Prefix),
                transpiler: new HarmonyMethod(HistoryRemindersTabPatch.DoMessagesPage_Transpiler)
            );

            // Patch Dialog_NodeTree to add pin texture
            patchClass = typeof(DialogDrawNodePatch);
            type = typeof(Dialog_NodeTree);
            PostfixMethod(typeof(Dialog_NodeTree), patchClass, "DoWindowContents");
            
            // Patches to the quest tab
            patchClass = typeof(QuestsTabPatches);
            type = typeof(MainTabWindow_Quests);
            TranspileMethod(type, patchClass, "DoSelectedQuestInfo");
            PrefixMethod(type, patchClass, "DoCharityIcon");
            TranspileMethod(type, patchClass, "DoDismissButton");

            // Patch to sort pinned letters always on the bottom
            patchClass = typeof(LetterStackReceiveLetterPatch);
            type = typeof(LetterStack);

#if v1_1 || v1_2 || v1_3 || v1_4
            _harmony.Patch(
                type.GetMethod("ReceiveLetter", new [] {typeof(Letter), typeof(string)}),
                postfix: GetPatch(patchClass, "ReceiveLetter")
                );
#elif v1_5 || v1_6
            Harmony.Patch(
                type.GetMethod("ReceiveLetter", new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) }),
                postfix: GetPatch(patchClass, "ReceiveLetter")
            );
#endif
        }

        private static MethodInfo? GetGetter(Type t, string propName)
        {
            try
            {
                LogPrefixed.Trace($"Patching {propName} property getter");
                return t.GetProperty(propName, AccessTools.all)?.GetGetMethod(true) ?? null;
            }
            catch (Exception e)
            {
                LogPrefixed.Error($"Error getting getter for property: {t.Name}.{propName}");
                throw;
            }
        }

        private static MethodInfo? GetGetter(Type implementingType, Type interfaceType, string propName)
        {
            try
            {
                LogPrefixed.Trace($"Patching interface property {propName} getter");
                var interfaceProp = interfaceType.GetProperty(propName, AccessTools.all);
                if (interfaceProp == null)
                    return null;

                var map = implementingType.GetInterfaceMap(interfaceType);
                var interfaceMethod = interfaceProp.GetGetMethod(true);

                var index = Array.IndexOf(map.InterfaceMethods, interfaceMethod);
                return index != -1 ? map.TargetMethods[index] : null;
            }
            catch (Exception e)
            {
                LogPrefixed.Error($"Error getting interface getter for property: {implementingType.Name}.{propName}");
                throw;
            }
        }

        private static HarmonyMethod GetPatch(Type t, string methodName)
        {
            try
            {
                LogPrefixed.Trace($"Patching {methodName} method");
                var method = t.GetMethod(methodName, AccessTools.all);
                var harmonyMethod = new HarmonyMethod(method);
                return harmonyMethod;
            }
            catch (Exception e)
            {
                LogPrefixed.Error($"Error getting patch method: {t.Name}.{methodName}");
                throw;
            }
        }

        /// <summary>
        /// Automatically gets the appropriate methods and applies a postfix patch to a property getter
        /// </summary>
        /// <param name="t">The type to patch the property of</param>
        /// <param name="patchClass">A class containing patches with matching names to the desired properties</param>
        /// <param name="propName">The property to patch. Must match the method name in the patchClass.</param>
        private static void PostfixGetter(Type t, Type patchClass, string propName, Type? interfaceType = null)
        {
            try
            {
                var original = interfaceType is null ? GetGetter(t, propName) : GetGetter(t, interfaceType, propName);
                var patch = GetPatch(patchClass, propName);
                Harmony.Patch(original, postfix: patch);
            }
            catch (Exception e)
            {
                LogPrefixed.Error($"Error patching property getter: {t.Name}.{propName}");
                throw;
            }
        }

        private static void PrefixMethod(Type t, Type patchClass, string methodName)
        {
            try
            {
                Harmony.Patch(
                    t.GetMethod(methodName, AccessTools.all),
                    prefix: GetPatch(patchClass, methodName)
                );
            }
            catch (Exception e)
            {
                LogPrefixed.Error($"Error patching method: {t.Name}.{methodName}");
                throw;
            }
        }

        private static void PostfixMethod(Type t, Type patchClass, string methodName)
        {
            try
            {
                Harmony.Patch(
                    t.GetMethod(methodName, AccessTools.all),
                    postfix: GetPatch(patchClass, methodName)
                );
            }
            catch (Exception e)
            {
                LogPrefixed.Error($"Error patching method: {t.Name}.{methodName}");
                throw;
            }
        }

        private static void TranspileMethod(Type t, Type patchClass, string methodName)
        {
            try
            {
                var originalMethod = t.GetMethod(methodName, AccessTools.all);
                var transpilerMethod = GetPatch(patchClass, methodName);
                Harmony.Patch(
                    originalMethod,
                    transpiler: transpilerMethod
                );
            }
            catch (Exception e)
            {
                LogPrefixed.Error($"Error transpiling method: {t.Name}.{methodName}");
                throw;
            }
        }
    }
}