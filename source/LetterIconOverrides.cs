using System.Collections.Generic;
using BetterLetters.Patches;
using RimWorld;
using UnityEngine;

namespace BetterLetters;

internal class SerializableLetterIconOverride : IExposable
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    // ReSharper disable InconsistentNaming
    internal LetterIconOverrideDef def;
    internal LetterIconOverrideResolver? resolver;
    // ReSharper restore InconsistentNaming
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public void ExposeData()
    {
        Scribe_Defs.Look(ref def, "def");
        Scribe_Deep.Look(ref resolver, "resolver");
    }
}

/// <summary>
///
/// </summary>
[StaticConstructorOnStartup]
public static class LetterIconOverrides
{
    private static readonly Dictionary<Def, LetterIconOverrideDef> DefLetterIconOverrides;

    internal static Dictionary<int, LetterIconOverrideDef> LetterIconsCache = new();

    internal static Dictionary<int, LetterIconOverrideResolver> ResolverCache = new();

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
    /// Reference to the most recent letter added to the stack. Set by the ReceiveLetter patch.
    /// Accessed try <see cref="TryOverrideMostRecentLetterIcon"/>, which automatically clears the reference as soon as
    /// it is used.
    /// </summary>
    internal static Letter? MostRecentLetter;

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
            LetterIconsCache[letter.ID] = iconOverrideDef;
            if (iconOverrideDef.IconResolver is { } resolver)
            {
                ResolverCache[letter.ID] = resolver;
            }
        }
        else
        {
            LetterIconsCache.Remove(letter.ID);
            ResolverCache.Remove(letter.ID);
        }
    }

    public static bool TryGetLetterIcon(this Letter letter, out Texture2D? icon)
    {
        return TryGetLetterIcon(letter.ID, out icon);
    }

    public static bool TryGetLetterIcon(int letterID, out Texture2D? icon)
    {
        var success = LetterIconsCache.TryGetValue(letterID, out var def);
        icon = def?.Icon;
        return success;
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
    }

    /// <summary>
    /// Called by <see cref="Patch_LetterStack_OverrideIcons.LetterIconsCacheExposeData"/> <br />
    /// Hijack <see cref="LetterStack" />'s own <see cref="LetterStack.ExposeData" /> call to inject <see cref="LetterIconsCache"/> into it.<br />
    /// Since references to <see cref="Texture2D"/> cannot be serialized, they need to be converted to/from strings.
    /// </summary>
    public static void ExposeData()
    {
        Scribe.EnterNode("BetterLetters");

        Dictionary<int, SerializableLetterIconOverride> serializableCache = new();

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            foreach (var kvp in LetterIconsCache)
            {
                if (kvp.Value is null) continue;

                serializableCache[kvp.Key] = new SerializableLetterIconOverride
                {
                    def = kvp.Value,
                    resolver = ResolverCache.TryGetValue(kvp.Key, out var resolver) ? resolver : null
                };
            }
        }

        Scribe_Collections.Look(ref serializableCache, "LetterIconsCache", LookMode.Value, LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            LetterIconsCache = new();
            ResolverCache = new();

            if (serializableCache != null)
            {
                foreach (var kvp in serializableCache)
                {
                    if (kvp.Value == null) continue;

                    LetterIconsCache[kvp.Key] = kvp.Value.def;

                    if (kvp.Value.resolver == null) continue;

                    ResolverCache[kvp.Key] = kvp.Value.resolver;
                    kvp.Value.def.IconResolver = kvp.Value.resolver;
                    kvp.Value.resolver.def = kvp.Value.def;
                }
            }
        }

        Scribe.ExitNode();
    }
}
