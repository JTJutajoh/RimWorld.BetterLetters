using System;
using System.Reflection;
using DarkLog;
using Verse;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace BetterLetters
{
    [UsedImplicitly]
    internal class BetterLettersMod : Mod
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
        /// Contains all the patches that should be run no matter what, regardless of what other mods are loaded
        /// </summary>
        internal static void Patch_Vanilla()
        {
            PatchCategory("Letter_RemoveLetter_KeepOnStack");
            PatchCategory("Letter_OpenLetter_AddDiaOptions");
            PatchCategory("Dialog_AddIcons");
            PatchCategory("ArchivePin_AddBackToStack");
            PatchCategory("Letter_CanDismissWithRightClick_BlockIfPinned");
            PatchCategory("Letter_DrawButton_Pinned");
            PatchCategory("Letter_CanCull_KeepSnoozes");

#if !(v1_1 || v1_2 || v1_3)
            PatchCategory("HistoryArchivableRow");
#else
            LogPrefixed.Warning("MainTabWindow_History.DoArchivableRow patch skipped, requires RimWorld 1.4+. Message History tab will not display snooze/reminder buttons in rows.");
#endif
            PatchCategory("HistoryFiltersAndButtons");
#if !(v1_1 || v1_2)
            PatchCategory("QuestsTab_Buttons");
#else
            LogPrefixed.Warning("Pin/Snooze buttons on Quests tab are only available in RimWorld 1.3+");
#endif
            PatchCategory("LetterStack_SortPinned");
            PatchCategory("PlaySettings_CreateReminderButton");

            //TODO: Patch to remove/change quest letters upon expiry/completion

            //TODO: FEATURE to change severity of individual letters
            //TODO: FEATURE custom reminder-specific LetterDef
            //TODO: FEATURE alert that shows number of snoozes
        }

        /// <summary>
        /// Wrapper for <see cref="Harmony"/>.<see cref="Harmony.PatchCategory(string)"/> that logs any errors that occur and
        /// skips patches that are disabled in the mod's configs.
        /// </summary>
        /// <param name="category">Name of the category to pass to <see cref="Harmony"/>.<see cref="Harmony.PatchCategory(string)"/></param>
        /// <param name="condition">Optional condition that must be true or the patch will be skipped.<br />
        /// Used for conditionally skipping patches based on mod configs.</param>
        private static void PatchCategory(string category, bool condition = true)
        {
            if (!condition) //TODO: Come up with a way to conditionally RE-patch categories if they're enabled in settings without requiring a restart
            {
                LogPrefixed.Message($"Patch \"{category}\" skipped, disabled in mod config.");
                return;
            }

            try
            {
                LogPrefixed.Trace($"Patching category \"{category}\"...");
                Harmony.PatchCategory(category);
            }
            catch (Exception e)
            {
                LogPrefixed.Exception(e, $"Error patching category {category}");
            }
        }
        
        /// <summary>
        /// Helper method to get the method info of an interface property for patching
        /// </summary>
        public static MethodBase? GetInterfaceProperty(this Type type, Type interfaceType, string propName)
        {
            var interfaceProp = interfaceType.GetProperty(propName, AccessTools.all);
            var map = type.GetInterfaceMap(interfaceType);
            var interfaceMethod = interfaceProp?.GetGetMethod(true);
        
            var index = Array.IndexOf(map.InterfaceMethods, interfaceMethod);

            return index != -1 ? map.TargetMethods[index] : null;
        }
    }
}