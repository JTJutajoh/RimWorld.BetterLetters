using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
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
    /// Patch that catches every letter added to the <see cref="LetterStack"/> and saves it to <see cref="MostRecentLetter"/>
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

    /// <summary>
    /// Make sure that the static reference <see cref="LetterIconOverrides.MostRecentLetter"/> is cleared after it's used.<br />
    /// Just clearing it every frame is the simplest way to make sure it gets cleared. It should only be used by postfix patches
    /// which run immediately after it was set, so it will never be needed across frames.
    /// </summary>
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.LetterStackUpdate))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void ClearCachedMostRecentLetter()
    {
        LetterIconOverrides.MostRecentLetter = null;
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
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheIncidentLetter(IncidentDef def, IncidentParms parms)
    {
        if (def?.letterDef is null) return;

        LetterIconOverrides.TryOverrideIconForDef(def);
    }

    [HarmonyPatch(typeof(IncidentWorker_MakeGameCondition), "TryExecuteWorker")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheGameConditionLetter(bool __result, IncidentParms parms, IncidentDef? ___def)
    {
        if (!__result || (___def?.letterDef is null && ___def?.gameCondition?.letterDef is null)) return;

        if (___def?.letterDef is null) return;

        LetterIconOverrides.TryOverrideIconForDef(___def.gameCondition);
    }

    #endregion Generic Interceptions

    #region Specific Interceptions

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheMentalStateLetter(bool __result, MentalStateDef stateDef, bool transitionSilently, Pawn ___pawn)
    {
        if (!__result ||
            transitionSilently ||
            !PawnUtility.ShouldSendNotificationAbout(___pawn))
            return;

        LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.MentalBreak);
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheRaidLetter(bool __result, IncidentParms parms)
    {
        if (!__result || parms.silent) return;

        LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.Raid, parms);
    }

    [HarmonyPatch(typeof(IncidentWorker_OrbitalTraderArrival), "TryExecuteWorker")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheTraderOrbitalLetter(bool __result, IncidentParms parms)
    {
        if (!__result || parms.silent) return;

        LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.TraderOrbital, parms);
    }

    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "SendLetter")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheTraderCaravanLetter(IncidentParms parms, TraderKindDef traderKind)
    {
        if (parms.silent) return;

        LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.TraderCaravan, parms, traderKind);
    }

    [HarmonyPatch(typeof(IncidentWorker_PsychicSoothe), "DoConditionAndLetter")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CachePsychicSootheLetter(IncidentParms parms, Gender gender)
    {
        if (parms.silent) return;

        LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.PsychicSoothe, parms, gender);
    }

    [HarmonyPatch(typeof(IncidentWorker_PsychicDrone), "DoConditionAndLetter")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CachePsychicDroneLetter(IncidentParms parms, Gender gender)
    {
        if (parms.silent) return;

        LetterIconOverrides.TryOverrideMostRecentLetterIcon(LetterIconOverrideDefOf.PsychicDrone, parms, gender);
    }

    #endregion Specific Interceptions

    #endregion Letter Interceptions
}
