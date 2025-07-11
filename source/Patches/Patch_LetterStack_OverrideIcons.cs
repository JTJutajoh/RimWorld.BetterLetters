using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse.AI;

namespace BetterLetters.Patches;

/// <summary>
/// Patches associated with <see cref="LetterIconOverrides"/><br />
/// Performs a few tasks:
/// caches letters as soon as they're received,
/// injects cache into
/// <see cref="LetterStack"/>'s <see cref="LetterStack.ExposeData"/>,
/// and has a bunch of small patches that call <see cref="LetterIconOverrides.TryOverrideMostRecentLetterIcon"/> after
/// a letter has been sent by various sources.
/// </summary>
[StaticConstructorOnStartup]
[HarmonyPatch]
[HarmonyPatchCategory("LetterIconCaching")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_LetterStack_OverrideIcons
{
    /// <summary>
    /// Patch that catches every letter added to the <see cref="LetterStack"/> and saves it to <see cref="LetterIconOverrides.MostRecentLetter"/>
    /// to be used in the other patches in this class.
    /// </summary>
    /// <param name="let">The letter that was just added to the stack</param>
#if !(v1_1 || v1_2 || v1_3 || v1_4) // LetterStack.ReceiveLetter method signature changed in 1.5
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
        new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
#else
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
        new[] { typeof(Letter), typeof(string) })]
#endif
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheMostRecentLetter(Letter let)
    {
        LetterIconOverrides.MostRecentLetter = let;
    }

    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ExposeData))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void LetterIconsCacheExposeData()
    {
        LetterIconOverrides.ExposeData();
    }

    // Below are all the minor patches that fire right after a letter is added to cache an override texture

    #region Letter Interceptions

    #region Generic Interceptions

    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.SendIncidentLetter))]
    [HarmonyPatchCategory("LetterIconCaching")]
    static class IncidentGenericLetterPatch
    {
        [UsedImplicitly]
        static void Prefix()
        {
            LetterIconOverrides.MostRecentLetter = null;
        }

        [UsedImplicitly]
        static void Postfix(IncidentDef def, IncidentParms parms)
        {
            if (def is null) return;

            LetterIconOverrides.TryOverrideIconForDef(def);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_MakeGameCondition), "TryExecuteWorker")]
    [HarmonyPatchCategory("LetterIconCaching")]
    static class GameConditionGenericLetterPatch
    {
        [HarmonyPrefix]
        [UsedImplicitly]
        static void Prefix()
        {
            LetterIconOverrides.MostRecentLetter = null;
        }

        [UsedImplicitly]
        static void Postfix(bool __result, IncidentParms parms, IncidentDef? ___def)
        {
            if (!__result || (___def?.letterDef is null && ___def?.gameCondition?.letterDef is null)) return;

            if (___def.letterDef is null) return;

            LetterIconOverrides.TryOverrideIconForDef(___def.gameCondition);
        }
    }

    #endregion Generic Interceptions

    #region Specific Interceptions

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    [HarmonyPatchCategory("LetterIconCaching")]
    static class MentalStateLetterPatch
    {
        [UsedImplicitly]
        static void Prefix()
        {
            LetterIconOverrides.MostRecentLetter = null;
        }

        [UsedImplicitly]
        static void Postfix(bool __result, MentalStateDef stateDef, bool transitionSilently, Pawn ___pawn)
        {
            if (!__result ||
                transitionSilently ||
                !PawnUtility.ShouldSendNotificationAbout(___pawn))
                return;

            LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.MentalBreak);
        }
    }

    [HarmonyPatch(typeof(Inspiration), "SendBeginLetter")]
    [HarmonyPatchCategory("LetterIconCaching")]
    static class InspirationLetterPatch
    {
        [UsedImplicitly]
        static void Prefix()
        {
            LetterIconOverrides.MostRecentLetter = null;
        }

        [UsedImplicitly]
        static void Postfix(InspirationDef ___def)
        {
            LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.Inspiration, ___def);
        }
    }

    [HarmonyPatch(typeof(HediffComp_Discoverable), "CheckDiscovered")]
    [HarmonyPatchCategory("LetterIconCaching")]
    static class DiscoverableDiseaseLetterPatch
    {
        [UsedImplicitly]
        static void Prefix()
        {
            LetterIconOverrides.MostRecentLetter = null;
        }

        [UsedImplicitly]
        static void Postfix(HediffComp_Discoverable __instance, bool ___discovered)
        {
            if (!___discovered ||
                !__instance.Props!.sendLetterWhenDiscovered ||
                !PawnUtility.ShouldSendNotificationAbout(__instance.Pawn!))
                return;

            LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.DiseaseDiscoverable,
                __instance.Props, __instance.parent!, __instance.Pawn!);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
    [HarmonyPatchCategory("LetterIconCaching")]
    static class RaidEnemyLetterPatch
    {
        [UsedImplicitly]
        static void Prefix()
        {
            LetterIconOverrides.MostRecentLetter = null;
        }

        [UsedImplicitly]
        static void Postfix(bool __result, IncidentParms parms)
        {
            if (!__result || parms.silent) return;

            LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.Raid, parms);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_OrbitalTraderArrival), "TryExecuteWorker")]
    [HarmonyPatchCategory("LetterIconCaching")]
    static class OrbitalTraderLetterPatch
    {
        [UsedImplicitly]
        static void Prefix()
        {
            LetterIconOverrides.MostRecentLetter = null;
        }

        [UsedImplicitly]
        static void Postfix(bool __result, IncidentParms parms)
        {
            if (!__result || parms.silent) return;

            LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.TraderOrbital, parms);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "SendLetter")]
    [HarmonyPatchCategory("LetterIconCaching")]
    static class TraderCaravanLetterPatch
    {
        [UsedImplicitly]
        static void Prefix()
        {
            LetterIconOverrides.MostRecentLetter = null;
        }

        [UsedImplicitly]
        static void Postfix(IncidentParms parms, TraderKindDef traderKind)
        {
            if (parms.silent) return;

            LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.TraderCaravan, parms,
                traderKind);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_PsychicSoothe), "DoConditionAndLetter")]
    [HarmonyPatchCategory("LetterIconCaching")]
    static class PsychicSootheLetterPatch
    {
        [UsedImplicitly]
        static void Prefix()
        {
            LetterIconOverrides.MostRecentLetter = null;
        }

        [UsedImplicitly]
        static void Postfix(IncidentParms parms, Gender gender)
        {
            if (parms.silent) return;

            LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.PsychicSoothe, parms, gender);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_PsychicDrone), "DoConditionAndLetter")]
    [HarmonyPatchCategory("LetterIconCaching")]
    static class PsychicDroneLetterPatch
    {
        [UsedImplicitly]
        static void Prefix()
        {
            LetterIconOverrides.MostRecentLetter = null;
        }

        [UsedImplicitly]
        static void Postfix(IncidentParms parms, Gender gender)
        {
            if (parms.silent) return;

            LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.PsychicDrone, parms, gender);
        }
    }

    [HarmonyPatch(typeof(ShortCircuitUtility))]
    [HarmonyPatchCategory("LetterIconCaching")]
    static class ShortCircuitLetterPatch
    {
        [UsedImplicitly]
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return typeof(ShortCircuitUtility).GetMethod(nameof(ShortCircuitUtility.DoShortCircuit))!;
            yield return typeof(ShortCircuitUtility).GetMethod(nameof(ShortCircuitUtility.TryShortCircuitInRain))!;
        }

        [UsedImplicitly]
        static void Prefix()
        {
            LetterIconOverrides.MostRecentLetter = null;
        }

        [UsedImplicitly]
        static void Postfix()
        {
            LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.ShortCircuit);
        }
    }

    #endregion Specific Interceptions

    #endregion Letter Interceptions
}
