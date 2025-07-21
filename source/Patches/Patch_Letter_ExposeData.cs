using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;

namespace BetterLetters.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("ExposeDataPatches")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_Letter_ExposeData
{
    [HarmonyPatch(typeof(Archive), nameof(Archive.ExposeData))]
    [HarmonyPrefix]
    [UsedImplicitly]
    static void ClearCachesBeforeLoading()
    {
        if (Scribe.mode != LoadSaveMode.LoadingVars) return;

        WorldComponent_SnoozeManager.ClearCaches();
        LetterIconOverrides.ClearCaches();
    }

    [HarmonyPatch(typeof(Letter), nameof(Letter.ExposeData))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void ExposeModLetterData(Letter __instance)
    {
        Scribe.EnterNode("BetterLetters");


        Snooze? snooze = null;
        var wasEverSnoozed = false;
        var wasReminder = false;
        LetterIconOverrideDef? iconOverrideDef = null;
        LetterIconOverrideResolver? iconOverrideResolver = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            WorldComponent_SnoozeManager.Snoozes.TryGetValue(__instance, out snooze);
            wasEverSnoozed = __instance.WasEverSnoozed();
            wasReminder = __instance.IsReminder();
            __instance.TryGetIconOverrideDef(out iconOverrideDef);
            __instance.TryGetIconResolver(out iconOverrideResolver);
        }

        // ReSharper disable RedundantArgumentDefaultValue
        if (Scribe.mode != LoadSaveMode.Saving || snooze is not null)
            Scribe_Deep.Look(ref snooze, false, "Snooze");
        Scribe_Values.Look(ref wasEverSnoozed, "WasEverSnoozed", false);
        Scribe_Values.Look(ref wasReminder, "WasReminder", false);
        Scribe_Defs.Look(ref iconOverrideDef, "IconOverrideDef");
        if (Scribe.mode != LoadSaveMode.Saving || iconOverrideResolver is not null)
            Scribe_Deep.Look(ref iconOverrideResolver, false, "IconOverrideResolver");
        // ReSharper restore RedundantArgumentDefaultValue
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            if (snooze is not null)
            {
                WorldComponent_SnoozeManager.Snoozes[__instance] = snooze;
            }

            if (wasEverSnoozed)
            {
                WorldComponent_SnoozeManager.AllSnoozesSeen.Add(__instance);
            }

            if (wasReminder)
            {
                WorldComponent_SnoozeManager.AllRemindersSeen.Add(__instance);
            }

            if (iconOverrideDef is not null)
            {
                LetterIconOverrides.LetterIconsCache[__instance.ID] = iconOverrideDef;
            }

            if (iconOverrideResolver is not null)
            {
                LetterIconOverrides.ResolverCache[__instance.ID] = iconOverrideResolver;
            }
        }

        Scribe.ExitNode();
    }
}
