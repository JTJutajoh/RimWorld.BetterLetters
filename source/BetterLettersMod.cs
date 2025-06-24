using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace BetterLetters
{
    internal class HarmonyPatchConditionAttribute : Attribute
    {
        internal RWVersion SupportedVersion { get; private set; }

        internal bool IsSupported => (LegacySupport.CurrentRWVersion & SupportedVersion) != 0;

        internal string? UnsupportedString = null;

        internal HarmonyPatchConditionAttribute(
            RWVersion supportedVersion = RWVersion.All,
            RWVersion unsupportedVersion = RWVersion.None,
            string? unsupportedString = null
        )
        {
            SupportedVersion = supportedVersion & ~unsupportedVersion;
            UnsupportedString = unsupportedString;
        }
    }

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
        private static int _skippedPatches;

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

            var totalPatches = _loadedPatches + _failedPatches + _skippedPatches;
            Log.Message($"{_loadedPatches}/{totalPatches} Harmony patches successful.");
            if (_skippedPatches > 0)
                Log.Message($"{_skippedPatches}/{totalPatches} Harmony patches skipped.");
            if (_failedPatches > 0)
                Log.Warning(
                    $"{_failedPatches}/{totalPatches} Harmony patches failed! The mod/game might behave in undesirable ways.");
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
            PatchCategory("BundleLetters");
            PatchCategory("HistoryArchivableRow");
            PatchCategory("QuestsTab_Buttons");
            PatchCategory("Letter_RemoveLetter_KeepOnStack_QuestLetter");
        }

        /// <summary>
        /// Wrapper for <see cref="Harmony"/>.<see cref="Harmony.PatchCategory(string)"/> that logs any errors that occur and
        /// skips patches that are disabled in the mod's configs.
        /// </summary>
        /// <param name="category">Name of the category to pass to <see cref="Harmony"/>.<see cref="Harmony.PatchCategory(string)"/></param>
        /// <param name="condition">Optional condition that must be true or the patch will be skipped.<br />
        /// Used for conditionally skipping patches based on mod configs.</param>
        private static void PatchCategory(string category)
        {
            var patchTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(HarmonyPatchCategory), true)
                    .Cast<HarmonyPatchCategory>()
                    .Any(attr => attr.info?.category == category))
                .ToList();

            // Find any classes in the assembly with a [HarmonyPatchCategory] attribute that matches the category
            var numMethods = patchTypes.SelectMany(t =>
                    t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Count(m => m.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0);

            // Find any [HarmonyPatchCondition] attributes on all the types in the category
            var conditions = patchTypes
                .SelectMany(t => t.GetCustomAttributes(typeof(HarmonyPatchConditionAttribute), true)
                    .Cast<HarmonyPatchConditionAttribute>())
                .ToList();

            // bitwise AND all of their supportedVersion
            var supportedVersions = conditions.Aggregate(RWVersion.All,
                (current, condition) => current & condition.SupportedVersion);

            // If the result is not a supported version, fail
            if ((supportedVersions & LegacySupport.CurrentRWVersion) == 0)
            {
                Log.Warning(
                    $"Patch category \"{category}\" ({numMethods} methods) skipped.\nOnly supported on RimWorld versions: {supportedVersions.ToString().Replace("_", ".").Replace("v", "")}.");
                _skippedPatches += numMethods;

                foreach (var condition in conditions)
                {
                    if (condition.UnsupportedString != null)
                    {
                        Log.Message(condition.UnsupportedString);
                    }
                }

                return;
            }


            try
            {
                Log.Trace($"Patching category \"{category}\" ({numMethods} methods)...");
                Harmony.PatchCategory(category);
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Error patching category {category}");
                _failedPatches += numMethods;
                return;
            }

            _loadedPatches += numMethods;
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
