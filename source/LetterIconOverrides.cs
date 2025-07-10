using System;
using System.Collections.Generic;
using BetterLetters.Patches;
using RimWorld;
using UnityEngine;

namespace BetterLetters;

/// <summary>
///
/// </summary>
[StaticConstructorOnStartup]
public static class LetterIconOverrides
{
    private static readonly Dictionary<Def, LetterIconOverrideDef> DefLetterIconOverrides;

    /// <summary>
    /// Runtime cache of letters and their overriding icons.<para />
    /// Key is the "ID" property on <see cref="Letter"/>, value is a texture loaded from <see cref="LetterIcons"/><br />
    /// Serialized in the <see cref="Patch_LetterStack_OverrideIcons.LetterIconsCacheExposeData"/> patch.
    /// </summary>
    internal static Dictionary<int, Texture2D> LetterIconsCache = new();

    static LetterIconOverrides()
    {
        DefLetterIconOverrides = new Dictionary<Def, LetterIconOverrideDef>();

        var letterIconOverrideDefs = DefDatabase<LetterIconOverrideDef>.AllDefs;
        if (letterIconOverrideDefs == null)
        {
            Log.Error("Couldn't load LetterIconOverrideDefs");
            return;
        }

        Log.Trace("Caching letter icon overrides");
        foreach (var def in letterIconOverrideDefs)
        {
            Log.Trace($" - {def.defName}:");
            foreach (var triggeringDef in def.TriggeringDefs)
            {
                if (triggeringDef == null)
                    continue; // might be null if the def doesn't exist, such as in legacy RW versions or if a DLC isn't installed
                DefLetterIconOverrides[triggeringDef] = def;
                Log.Trace($"Cached icon override for {triggeringDef.defName}: {def.defName}");
            }
        }
    }

    /// <summary>
    /// Statically-loaded mapping of all letter icon overrides.<br />
    /// Keys are the "name" of the override.
    /// </summary>
    /// <remarks>Keys MUST match the filename of the texture exactly, as it is used during deserialization.</remarks>
    [Obsolete] private static readonly Dictionary<string, Texture2D> LetterIcons = new()
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


    /// <summary>
    /// Reference to the most recent letter added to the stack. Set by the <see cref="ReceiveLetter"/> patch.
    /// Accessed try <see cref="TryOverrideMostRecentLetterIcon"/>, which automatically clears the reference as soon as
    /// it is used.
    /// </summary>
    internal static Letter? MostRecentLetter = null;
    //BUG: MostRecentLetter isn't always null when it should be.
    // maybe implement something that clears it the next frame, since it's only used to reference a letter on the same frame that it was added

    /// <summary>
    /// Attempts to set the override icon for <see cref="MostRecentLetter"/> to a texture in <see cref="LetterIcons"/>
    /// using <paramref name="iconName"/> as the key.<para />
    /// If <see cref="MostRecentLetter"/> is null (no letter has been received by the stack since the last one was overridden),
    /// nothing happens.<br />
    /// Clears the <see cref="MostRecentLetter"/> reference so that it is only used once.
    /// </summary>
    internal static void TryOverrideMostRecentLetterIcon(LetterIconOverrideDef iconOverrideDef, params object[] context)
    {
        MostRecentLetter?.OverrideIcon(iconOverrideDef, context);
        MostRecentLetter = null;
    }

    public static void OverrideIcon(this Letter letter, LetterIconOverrideDef? iconOverrideDef, params object[] context)
    {
        if (iconOverrideDef != null)
        {
            iconOverrideDef.ResolveIcon(context);
            LetterIconsCache[letter.ID] = iconOverrideDef.Icon;
        }
        else
        {
            LetterIconsCache.Remove(letter.ID);
        }
    }

    /// <summary>
    /// Extension method that saves the provided texture to <see cref="LetterIconsCache"/> to be used in
    /// <see cref="Patch_Letter_DrawButton_LetterStackAppearance"/>
    /// </summary>
    [Obsolete]
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
        {
            LetterIconsCache.Remove(letter.ID);
        }
    }

    public static bool TryGetLetterIcon(this Letter letter, out Texture2D? icon)
    {
        return TryGetLetterIcon(letter.ID, out icon);
    }

    public static bool TryGetLetterIcon(int letterID, out Texture2D? icon)
    {
        return LetterIconsCache.TryGetValue(letterID, out icon);
    }

    internal static bool TryGetIconOverrideDefForDef(Def? def, out LetterIconOverrideDef? iconOverrideDef)
    {
        iconOverrideDef = null;

        if (def == null) return false;

        return DefLetterIconOverrides.TryGetValue(def, out iconOverrideDef);
    }

    /// <summary>
    /// Generic method that tries to set the icon override based simply on its def as the key in <see cref="DefLetterIconOverrides"/>
    /// for simple cases where all that's needed is the def.
    /// </summary>
    /// <param name="def">The def (usually <see cref="GameConditionDef"/> or <see cref="IncidentDef"/>) that just fired
    /// the letter to override the icon of.</param>
    public static void TryOverrideIconForDef(Def? def)
    {
        if (def == null) return;

        if (TryGetIconOverrideDefForDef(def, out var iconOverrideDef) && iconOverrideDef != null)
        {
            TryOverrideMostRecentLetterIcon(iconOverrideDef);
        }
        else
        {
            Log.Trace($"Tried to override the icon for def {def.defName} but no icon override was found.");
        }
    }

    /// <summary>
    /// Called by <see cref="Patch_LetterStack_OverrideIcons.LetterIconsCacheExposeData"/> <br />
    /// Hijack <see cref="LetterStack" />'s own <see cref="LetterStack.ExposeData" /> call to inject <see cref="LetterIconsCache"/> into it.<br />
    /// Since references to <see cref="Texture2D"/> cannot be serialized, they need to be converted to/from strings.
    /// </summary>
    public static void ExposeData()
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
}
