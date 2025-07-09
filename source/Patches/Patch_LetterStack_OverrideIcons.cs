using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse.AI;

namespace BetterLetters.Patches;

/// <summary>
/// Class responsible for all of the patching and logic for replacing letter icons.<para />
/// The actual patch that uses the custom icons is in <see cref="Patch_Letter_DrawButton_LetterStackAppearance"/>.<para />
/// Composed of four main parts:
/// <list type="number">
///     <item>
///         <term><see cref="LetterIcons"/>: </term>
///         <description>Cache of all the actual static-loaded custom letter icons, accessible based on their name.</description>
///     </item>
///     <item>
///         <term><see cref="GameConditionLetterIcons"/> and <see cref="IncidentLetterIcons"/>: </term>
///         <description>Mappings of conditions/incidents and their corresponding letter replacements, as strings which
///             match keys in <see cref="LetterIcons"/><br />
///             These are used in the <see cref="CacheIncidentLetter"/> and <see cref="CacheGameConditionLetter"/> patches
///             which together catch a lot of vanilla letters.</description>
///     </item>
///     <item>
///         <term><see cref="LetterIconsCache"/>: </term>
///         <description>Runtime-populated cache of letter IDs mapped to replacement icons.<br />
///             Serialized in <see cref="LetterIconsCacheExposeData"/>, which is a patch for <see cref="LetterStack"/> that injects the
///             contents of <see cref="LetterIconsCache"/> into the save file.</description>
///     </item>
///     <item>
///         <term>Post-<see cref="ReceiveLetter"/> patches: </term>
///         <description>A bunch of small patches that fire immediately after letters are sent to the <see cref="LetterStack"/>
///             and uses the <see cref="MostRecentLetter"/> reference to cache corresponding icon overrides.</description>
///     </item>
/// </list>
/// </summary>
[StaticConstructorOnStartup]
[HarmonyPatch]
[HarmonyPatchCategory("LetterIconCaching")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_LetterStack_OverrideIcons
{
    /// <summary>
    /// Statically-loaded mapping of all letter icon overrides.<br />
    /// Keys are the "name" of the override.
    /// </summary>
    /// <remarks>Keys MUST match the filename of the texture exactly, as it is used during deserialization.</remarks>
    // ReSharper disable AssignNullToNotNullAttribute
    internal static readonly Dictionary<string, Texture2D> LetterIcons = new()
    {
        { "LetterMentalBreak", ContentFinder<Texture2D>.Get("UI/Letters/LetterMentalBreak") },
        { "LetterSolarFlare", ContentFinder<Texture2D>.Get("UI/Letters/LetterSolarFlare") },
        { "LetterFlashstorm", ContentFinder<Texture2D>.Get("UI/Letters/LetterFlashstorm") },
        { "LetterVolcanicWinter", ContentFinder<Texture2D>.Get("UI/Letters/LetterVolcanicWinter") },
        { "LetterEclipse", ContentFinder<Texture2D>.Get("UI/Letters/LetterEclipse") },
        { "LetterToxicFallout", ContentFinder<Texture2D>.Get("UI/Letters/LetterToxicFallout") },
        { "LetterColdSnap", ContentFinder<Texture2D>.Get("UI/Letters/LetterColdSnap") },
        { "LetterHeatWave", ContentFinder<Texture2D>.Get("UI/Letters/LetterHeatWave") },
        { "LetterAurora", ContentFinder<Texture2D>.Get("UI/Letters/LetterAurora") },
        { "LetterPsychicDrone", ContentFinder<Texture2D>.Get("UI/Letters/LetterPsychicDrone") },
        { "LetterPsychicSoothe", ContentFinder<Texture2D>.Get("UI/Letters/LetterPsychicSoothe") },

        { "LetterRaid", ContentFinder<Texture2D>.Get("UI/Letters/LetterRaid") },

        { "LetterTraderCaravan", ContentFinder<Texture2D>.Get("UI/Letters/LetterTraderCaravan") },
        { "LetterTraderOrbital", ContentFinder<Texture2D>.Get("UI/Letters/LetterTraderOrbital") },

        { "LetterIdeology", ContentFinder<Texture2D>.Get("UI/Letters/LetterIdeology") },

        { "LetterAnomaly", ContentFinder<Texture2D>.Get("UI/Letters/LetterAnomaly") },
    };
    // ReSharper restore AssignNullToNotNullAttribute

    /// <summary>
    /// Mapping of <see cref="GameConditionDef"/>s to the key in <see cref="LetterIcons"/> of the icon that should override
    /// its letter.
    /// </summary>
    private static readonly Dictionary<GameConditionDef, string> GameConditionLetterIcons = new()
    {
        { GameConditionDefOf.ColdSnap, "LetterColdSnap" },
        { GameConditionDefOf.HeatWave, "LetterHeatWave" },
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

    /// <summary>
    /// Mapping of <see cref="IncidentDef"/>s to the key in <see cref="LetterIcons"/> of the icon that should override
    /// its letter.
    /// </summary>
    private static readonly Dictionary<IncidentDef, string> IncidentLetterIcons = new()
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

    /// <summary>
    /// Runtime cache of letters and their overriding icons.<para />
    /// Key is the "ID" property on <see cref="Letter"/>, value is a texture loaded from <see cref="LetterIcons"/><br />
    /// Serialized in the <see cref="LetterIconsCacheExposeData"/> patch.
    /// </summary>
    internal static Dictionary<int, Texture2D> LetterIconsCache = new();

    /// <summary>
    /// Reference to the most recent letter added to the stack. Set by the <see cref="ReceiveLetter"/> patch.
    /// Accessed try <see cref="TryOverrideMostRecentLetterIcon"/>, which automatically clears the reference as soon as
    /// it is used.
    /// </summary>
    private static Letter? MostRecentLetter = null;

    /// <summary>
    /// Attempts to set the override icon for <see cref="MostRecentLetter"/> to a texture in <see cref="LetterIcons"/>
    /// using <paramref name="iconName"/> as the key.<para />
    /// If <see cref="MostRecentLetter"/> is null (no letter has been received by the stack since the last one was overridden),
    /// nothing happens.<br />
    /// Clears the <see cref="MostRecentLetter"/> reference so that it is only used once.
    /// </summary>
    private static void TryOverrideMostRecentLetterIcon(string? iconName)
    {
        MostRecentLetter?.OverrideIcon(iconName);
        MostRecentLetter = null;
    }

    /// <summary>
    /// Patch that catches every letter added to the <see cref="LetterStack"/> and saves it to <see cref="MostRecentLetter"/>
    /// to be used in the other patches in this class.
    /// </summary>
    /// <param name="let">The letter that was just added to the stack</param>
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
        //BUG: What happens if this reference is not used by whatever added it, and the next time it's accessed it should be null?
        // for example if Letter A has no override (reference is stored but not cleared) and Letter B does not get added to the stack but a patch runs and thinks that it did (like if a mental state fails to get added, for example)
    }

    /// <summary>
    /// Extension method that saves the provided texture to <see cref="LetterIconsCache"/> to be used in
    /// <see cref="Patch_Letter_DrawButton_LetterStackAppearance"/>
    /// </summary>
    public static void OverrideIcon(this Letter letter, string? iconName)
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

    /// <summary>
    /// Hijack <see cref="LetterStack" />'s own <see cref="LetterStack.ExposeData" /> call to inject <see cref="LetterIconsCache"/> into it.<br />
    /// Since references to <see cref="Texture2D"/> cannot be serialized, they need to be converted to/from strings.
    /// </summary>
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ExposeData))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void LetterIconsCacheExposeData()
    {
        Dictionary<int, string> letterIconsCacheAsNames = new();
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            foreach (var kvp in LetterIconsCache)
            {
                if (kvp.Value == null) continue;
                // Use the "name" field on the texture, which matches its filename
                letterIconsCacheAsNames[kvp.Key] = kvp.Value.name;
            }
        }

        // Include a prefix to prevent data collisions
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


    // Below are all the minor patches that fire right after a letter is added to cache an override texture

    #region Letter Interceptions

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheMentalStateLetter(bool __result, MentalStateDef stateDef, bool transitionSilently, Pawn ___pawn)
    {
        if (!__result ||
            transitionSilently ||
            !PawnUtility.ShouldSendNotificationAbout(___pawn))
            return;

        TryOverrideMostRecentLetterIcon("LetterMentalBreak");
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
            TryOverrideMostRecentLetterIcon(iconName);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_MakeGameCondition), "TryExecuteWorker")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheGameConditionLetter(bool __result, IncidentParms parms, IncidentDef ___def)
    {
        if (!__result || (___def?.letterDef is null && ___def?.gameCondition?.letterDef is null)) return;

        string? iconName = null;

        if (GameConditionLetterIcons.TryGetValue(___def.gameCondition, out var iconNameOverride))
        {
            iconName = iconNameOverride;
        }

        if (iconName != null)
        {
            TryOverrideMostRecentLetterIcon(iconName);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheRaidLetter(bool __result, IncidentParms parms)
    {
        if (!__result) return;

        TryOverrideMostRecentLetterIcon("LetterRaid");
    }

    [HarmonyPatch(typeof(IncidentWorker_OrbitalTraderArrival), "TryExecuteWorker")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheTraderOrbitalLetter(bool __result, IncidentParms parms)
    {
        if (!__result) return;

        TryOverrideMostRecentLetterIcon("LetterTraderOrbital");
    }

    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "SendLetter")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CacheTraderCaravanLetter(IncidentParms parms, TraderKindDef traderKind)
    {
        TryOverrideMostRecentLetterIcon("LetterTraderCaravan");
    }

    [HarmonyPatch(typeof(IncidentWorker_PsychicSoothe), "DoConditionAndLetter")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CachePsychicSootheLetter(IncidentParms parms, Gender gender)
    {
        TryOverrideMostRecentLetterIcon("LetterPsychicSoothe");
    }

    [HarmonyPatch(typeof(IncidentWorker_PsychicDrone), "DoConditionAndLetter")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CachePsychicDroneLetter(IncidentParms parms, Gender gender)
    {
        TryOverrideMostRecentLetterIcon("LetterPsychicDrone");
    }

    #endregion Letter Interceptions
}
