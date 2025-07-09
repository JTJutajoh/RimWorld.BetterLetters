using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse.AI;

namespace BetterLetters.Patches;

[StaticConstructorOnStartup]
[HarmonyPatch]
[HarmonyPatchCategory("LetterIconCaching")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_LetterStack_OverrideIcons
{
    // ReSharper disable AssignNullToNotNullAttribute
    internal static readonly Dictionary<string, Texture2D> LetterIcons = new()
    {
        { "LetterMentalBreak", ContentFinder<Texture2D>.Get("UI/Letters/LetterMentalBreak") },
        { "LetterSolarFlare", ContentFinder<Texture2D>.Get("UI/Letters/LetterSolarFlare") },
        { "LetterFlashstorm", ContentFinder<Texture2D>.Get("UI/Letters/LetterFlashstorm") },
        { "LetterVolcanicWinter", ContentFinder<Texture2D>.Get("UI/Letters/LetterVolcanicWinter") },
        { "LetterEclipse", ContentFinder<Texture2D>.Get("UI/Letters/LetterEclipse") },
        { "LetterRaid", ContentFinder<Texture2D>.Get("UI/Letters/LetterRaid") },
        { "LetterTraderCaravan", ContentFinder<Texture2D>.Get("UI/Letters/LetterTraderCaravan") },
        { "LetterTraderOrbital", ContentFinder<Texture2D>.Get("UI/Letters/LetterTraderOrbital") },
        { "LetterToxicFallout", ContentFinder<Texture2D>.Get("UI/Letters/LetterToxicFallout") },
        { "LetterColdSnap", ContentFinder<Texture2D>.Get("UI/Letters/LetterColdSnap") },
        { "LetterHeatWave", ContentFinder<Texture2D>.Get("UI/Letters/LetterHeatWave") },
        { "LetterAurora", ContentFinder<Texture2D>.Get("UI/Letters/LetterAurora") },
        { "LetterAnomaly", ContentFinder<Texture2D>.Get("UI/Letters/LetterAnomaly") },
        { "LetterIdeology", ContentFinder<Texture2D>.Get("UI/Letters/LetterIdeology") },
    };
    // ReSharper restore AssignNullToNotNullAttribute

    internal static readonly Dictionary<GameConditionDef, string> GameConditionLetterIcons = new()
    {
        { GameConditionDefOf.ColdSnap, "LetterColdSnap" },
        { GameConditionDefOf.HeatWave, "LetterHeatWave" },
        { GameConditionDefOf.PsychicDrone, "LetterPsychicDrone" },
        { GameConditionDefOf.PsychicSuppression, "LetterPsychicDrone" },
        { GameConditionDefOf.PsychicSoothe, "LetterPsychicSoothe" },
        { GameConditionDefOf.VolcanicWinter, "LetterVolcanicWinter" },
        { GameConditionDefOf.Aurora, "LetterAurora" },
        { GameConditionDefOf.EMIField, "LetterSolarFlare" },
#if !(v1_1 || v1_2 || v1_3 || v1_4)
        { GameConditionDefOf.DeathPall, "LetterAnomaly" },
        { GameConditionDefOf.GrayPall, "LetterAnomaly" },
        { GameConditionDefOf.BloodRain, "LetterAnomaly" },
        { GameConditionDefOf.HateChantDrone, "LetterAnomaly" },
        { GameConditionDefOf.UnnaturalDarkness, "LetterAnomaly" },
        { GameConditionDefOf.UnnaturalHeat, "LetterAnomaly" },
#endif
// #if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5)
//         { GameConditionDefOf.GillRot, "LetterGillRot" },
// #endif
    };

    internal static readonly Dictionary<IncidentDef, string> IncidentLetterIcons = new()
    {
        { IncidentDefOf.SolarFlare, "LetterSolarFlare" },
        { IncidentDefOf.Eclipse, "LetterEclipse" },
        { IncidentDefOf.RaidEnemy, "LetterRaid" },
        { IncidentDefOf.ToxicFallout, "LetterToxicFallout" },
        { MissingVanillaDefOf.Flashstorm, "LetterFlashstorm" },
//         { IncidentDefOf.ManhunterPack, "LetterManhunter" },
//         { IncidentDefOf.Infestation, "LetterInfestation" },
//         { IncidentDefOf.Infestation_Jelly, "LetterInfestation" },
//         { IncidentDefOf.MechCluster, "LetterMechCluster" },
#if !(v1_1 || v1_2 || v1_3 || v1_4)
        { IncidentDefOf.HarbingerTreeSpawn, "LetterAnomaly" },
        { IncidentDefOf.FrenziedAnimals, "LetterAnomaly" },
        { IncidentDefOf.PitGate, "LetterAnomaly" },
        { IncidentDefOf.ShamblerSwarm, "LetterAnomaly" },
        { IncidentDefOf.PsychicRitualSiege, "LetterAnomaly" },
        { IncidentDefOf.VoidCuriosity, "LetterAnomaly" },
#endif
// #if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5)
//         { IncidentDefOf.Drought, "LetterDrought" },
//         { IncidentDefOf.LavaFlow, "LetterLavaFlow" },
// #endif
    };

    internal static Dictionary<int, Texture2D> LetterIconsCache = new();

    internal static Letter? MostRecentLetter = null;

#if v1_1 || v1_2 || v1_3 || v1_4
    // LetterStack.ReceiveLetter method signature changed in 1.5
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
        new[] { typeof(Letter), typeof(string) })]
#else
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
        new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
#endif
    [HarmonyPostfix]
    [UsedImplicitly]
    static void ReceiveLetter(Letter let)
    {
        MostRecentLetter = let;
    }

    /// <summary>
    /// Extension method that saves the provided texture to <see cref="LetterIconsCache"/> to be used in
    /// <see cref="Patch_Letter_DrawButton_LetterStackAppearance"/>
    /// </summary>
    internal static void OverrideIcon(this Letter letter, string? iconName)
    {
        if (iconName != null)
        {
            if (LetterIcons.TryGetValue(iconName, out var tex))
                LetterIconsCache[letter.ID] = tex;
            else
                Log.Warning($"Couldn't find icon '{iconName}' for letter: '{letter.Label}'");
        }
        else
            LetterIconsCache.Remove(letter.ID);
    }

    // Hijack LetterStack's own ExposeData call to inject the cache into it
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ExposeData))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void ExposeData()
    {
        Dictionary<int, string> letterIconsCacheAsNames = new();
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            foreach (var kvp in LetterIconsCache)
            {
                if (kvp.Value == null) continue;
                letterIconsCacheAsNames[kvp.Key] = kvp.Value.name;
            }
        }

        Scribe_Collections.Look(ref letterIconsCacheAsNames, "BetterLetters_LetterIconsCache", LookMode.Value,
            LookMode.Value);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            LetterIconsCache = new Dictionary<int, Texture2D>();
            if (letterIconsCacheAsNames == null) return;

            foreach (var kvp in letterIconsCacheAsNames)
            {
                if (LetterIcons.TryGetValue(kvp.Value, out var tex))
                    LetterIconsCache[kvp.Key] = tex;
            }
        }
    }

    #region Letter Interceptions
    //BUG: For all of the "Try" ones, need to check if it succeeded before assuming it did.
    // Maybe simply clear the MostRecentLetter reference each time that it's used so that it can't

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheMentalStateLetter(MentalStateDef stateDef)
    {
        if (!Settings.ReplaceLetterIconsInXML) return;

        MostRecentLetter?.OverrideIcon("LetterMentalBreak");
        MostRecentLetter = null;
    }


    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.SendIncidentLetter))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheIncidentLetter(IncidentDef def, IncidentParms parms)
    {
        if (def?.letterDef is null) return;

        string? iconName = null;

        if (IncidentLetterIcons.TryGetValue(def, out var iconNameOverride))
        {
            iconName = iconNameOverride;
        }

        if (iconName != null)
        {
            MostRecentLetter?.OverrideIcon(iconName);
            MostRecentLetter = null;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_MakeGameCondition), "TryExecuteWorker")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheGameConditionLetter(IncidentParms parms, IncidentDef ___def)
    {
        if (___def?.letterDef is null && ___def?.gameCondition?.letterDef is null) return;

        string? iconName = null;

        if (GameConditionLetterIcons.TryGetValue(___def.gameCondition, out var iconNameOverride))
        {
            iconName = iconNameOverride;
        }

        if (iconName != null)
        {
            MostRecentLetter?.OverrideIcon(iconName);
            MostRecentLetter = null;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheRaidLetter(IncidentParms parms)
    {
        MostRecentLetter?.OverrideIcon("LetterRaid");
        MostRecentLetter = null;
    }

    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "SendLetter")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheTraderCaravanLetter(IncidentParms parms, TraderKindDef traderKind)
    {
        MostRecentLetter?.OverrideIcon("LetterTraderCaravan");
        MostRecentLetter = null;
    }

    [HarmonyPatch(typeof(IncidentWorker_OrbitalTraderArrival), "TryExecuteWorker")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheTraderOrbitalLetter(IncidentParms parms)
    {
        MostRecentLetter?.OverrideIcon("LetterTraderOrbital");
        MostRecentLetter = null;
    }

    #endregion Letter Interceptions
}
