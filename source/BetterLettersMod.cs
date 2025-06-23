using System;
using System.Reflection;
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
            // ReSharper disable once RedundantArgumentDefaultValue
            Log.Initialize(this, "cyan");

            GetSettings<Settings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            try
            {
                base.DoSettingsWindowContents(inRect);
                GetSettings<Settings>()!.DoWindowContents(inRect);
            }
            catch (Exception e)
            {
                Log.Exception(e, "Error drawing mod settings window.", true);
                Widgets.DrawBoxSolid(inRect, new Color(0, 0, 0, 0.5f));
                var errorRect = inRect.MiddlePart(0.4f, 0.25f);
                Widgets.DrawWindowBackground(errorRect);
                Widgets.Label(errorRect.ContractedBy(16f),
                    $"Error rendering settings window:\n\"{e.Message}\", see log for stack trace.\nPlease report this to the mod author.");
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

        private static int _loadedPatches;
        private static int _failedPatches;

        static LoadHarmony()
        {
            Harmony = new Harmony(BetterLettersMod.Instance!.Content!.PackageId!);

#if DEBUG
            // Harmony.DEBUG = true; // For debugging transpilers. DO NOT uncomment this unless you need it!
#endif

            Log.Message("Running Harmony patches...");

            try
            {
                Patch_Vanilla();
            }
            catch (Exception e)
            {
                Log.Exception(e,
                    "Error patching vanilla. This likely means either the wrong game version or a hard incompatibility with another mod.");
            }

            Log.Message($"{_loadedPatches}/{_loadedPatches + _failedPatches} Harmony patches successful.");
            if (_failedPatches > 0)
                Log.Warning($"{_failedPatches} Harmony patches failed! The mod/game might behave in undesirable ways.");
        }

        /// <summary>
        /// Contains all the patches that should be run no matter what, regardless of what other mods are loaded
        /// </summary>
        private static void Patch_Vanilla()
        {
            PatchCategory("Letter_RemoveLetter_KeepOnStack");
            PatchCategory("Letter_OpenLetter_AddDiaOptions");
            PatchCategory("Dialog_AddIcons");
            PatchCategory("ArchivePin_AddBackToStack");
            PatchCategory("Letter_CanDismissWithRightClick_BlockIfPinned");
            PatchCategory("Letter_DrawButton_Pinned");
            PatchCategory("Letter_CanCull_KeepSnoozes");
            PatchCategory("LetterStack_SortPinned");
            PatchCategory("PlaySettings_CreateReminderButton");
            PatchCategory("HistoryFiltersAndButtons");
            PatchCategory("ExpireQuestLetters");

#if !(v1_1 || v1_2 || v1_3)
            PatchCategory("BundleLetters");
#else
            Log.Warning("BundleLetters patch skipped, doesn't exist in 1.1-1.3");
            _failedPatches++;
#endif
#if !(v1_1 || v1_2 || v1_3)
            PatchCategory("HistoryArchivableRow");
#else
            Log.Warning(
                "MainTabWindow_History.DoArchivableRow patch skipped, requires RimWorld 1.4+. Message History tab will not display snooze/reminder buttons in rows.");
            _failedPatches++;
#endif


#if !(v1_1 || v1_2)
            PatchCategory("QuestsTab_Buttons");
            PatchCategory("Letter_RemoveLetter_KeepOnStack_QuestLetter");
#else
            Log.Warning("Pin/Snooze buttons on Quests tab are only available in RimWorld 1.3+");
            _failedPatches++;
            _failedPatches++;
#endif
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
            if (!condition) //MAYBE: Come up with a way to conditionally RE-patch categories if they're enabled in settings without requiring a restart
            {
                Log.Message($"Patch \"{category}\" skipped, disabled in mod config.");
                return;
            }

            try
            {
                Log.Trace($"Patching category \"{category}\"...");
                Harmony.PatchCategory(category);
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Error patching category {category}");
                _failedPatches++;
                return;
            }

            _loadedPatches++;
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
