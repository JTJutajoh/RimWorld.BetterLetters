using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BetterLetters.Patches;
using HarmonyLib;
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

    internal static readonly Dictionary<MethodInfo, LetterIconOverrideDef> GenericPatchedOverrideMethods = new();

    static LetterIconOverrides()
    {
        DefLetterIconOverrides = new Dictionary<Def, LetterIconOverrideDef>();

        var letterIconOverrideDefs = DefDatabase<LetterIconOverrideDef>.AllDefs;

        if (letterIconOverrideDefs == null)
        {
            Log.Error("Couldn't load LetterIconOverrideDefs");
            return;
        }

        Log.Trace("Caching letter icon override defs");
        foreach (var def in letterIconOverrideDefs)
        {
            CacheLetterIconOverrideDef(def);

            PatchLetterIconOverrideDefTargets(def);
        }

        PatchManager.Notify_PatchingComplete();
    }

    private static void CacheLetterIconOverrideDef(LetterIconOverrideDef? def)
    {
        if (def == null) return;
        Log.Trace($"\t- {def.defName}:");
        foreach (var triggeringDef in def.TriggeringDefs.OfType<Def>())
        {
            DefLetterIconOverrides[triggeringDef] = def;

            Log.Trace($"\t\tCached icon override for {triggeringDef.defName}: {def.defName}");
        }
    }

    private static void PatchLetterIconOverrideDefTargets(LetterIconOverrideDef? def)
    {
        if (def == null) return;

        var patchTargets = def.PatchTargets;

        if (def.patchWorker is null || patchTargets is null || patchTargets.Count == 0) return;

        Log.Trace(
            $"\t\t\tFound patch worker {def.patchWorker?.GetType()} on def {def.defName}\n\t\t\t\tTargets: {patchTargets.Join(p => p.TypeColonName)}");
        foreach (var patchTarget in patchTargets)
        {
            PatchGenericLetterIconOverride(patchTarget, def);
        }
    }

    private static void PatchGenericLetterIconOverride(PatchTarget patchTarget, LetterIconOverrideDef def)
    {
        Log.Trace($"Attempting to patch {patchTarget.TypeColonName} for {def.defName}");
        var targetMethod = patchTarget.TargetMethod;
        if (targetMethod is null)
            throw new InvalidOperationException(
                $"Target method {patchTarget.TypeColonName} not found in any loaded assemblies");

        try
        {
            Patch_GenericLetterSenderInterception.LetterSendingMethodName = patchTarget.LetterSendingMethod;
            var transpiler = new HarmonyMethod(typeof(Patch_GenericLetterSenderInterception), "Transpiler");
            PatchManager.Harmony.Patch(targetMethod, transpiler: transpiler);
            GenericPatchedOverrideMethods[targetMethod] = def;
        }
        catch (Exception e)
        {
            Log.Exception(e,
                $"Error patching {patchTarget.TypeColonName} for {def.defName}");
            Patch_GenericLetterSenderInterception.Cleanup(targetMethod, e);
            return;
        }

        Patch_GenericLetterSenderInterception.Cleanup(targetMethod, null);

        Log.Trace($"Patched {patchTarget.TypeColonName} for {def.defName}");
    }


    private static Letter? _mostRecentLetter;

    /// <summary>
    /// Reference to the most recent letter added to the stack. Set by the ReceiveLetter patch.
    /// Accessed try <see cref="TryOverrideMostRecentLetterIcon"/>, which automatically clears the reference as soon as
    /// it is used.
    /// </summary>
    internal static Letter? MostRecentLetter
    {
        get => _mostRecentLetter;
        set
        {
            Log.Trace($"Caching most recent letter {value?.Label}");
            if (value != null && _mostRecentLetter != null)
            {
                Log.Message($"Previous letter was not overridden: {_mostRecentLetter.Label}");
            }

            _mostRecentLetter = value;
        }
    }

    internal static void ClearMostRecentLetter()
    {
        MostRecentLetter = null;
    }

    internal static void TryOverrideMostRecentLetterIcon(LetterIconOverrideDef iconOverrideDef, params object[] context)
    {
        MostRecentLetter?.OverrideIcon(iconOverrideDef, context);
        MostRecentLetter = null;
    }

    public static void OverrideIcon(this Letter letter, LetterIconOverrideDef? iconOverrideDef, params object[] context)
    {
        if (iconOverrideDef != null)
        {
            LetterIconsCache[letter.ID] = iconOverrideDef;
            if (iconOverrideDef.resolverClass is { } resolverClass)
            {
                var resolver = (LetterIconOverrideResolver)Activator.CreateInstance(resolverClass)!;
                resolver.TryResolve(iconOverrideDef, context);
                ResolverCache[letter.ID] = resolver;
            }
        }
        else
        {
            LetterIconsCache.Remove(letter.ID);
            ResolverCache.Remove(letter.ID);
        }
    }


    public static bool HasLetterIconOverride(this Letter letter)
    {
        return HasLetterIconOverride(letter.ID);
    }

    public static bool HasLetterIconOverride(int letterID)
    {
        return LetterIconsCache.ContainsKey(letterID) || ResolverCache.ContainsKey(letterID);
    }


    public static bool TryGetIconOverrideDef(this Letter letter, out LetterIconOverrideDef? iconOverrideDef)
    {
        return TryGetIconOverrideDef(letter.ID, out iconOverrideDef);
    }

    public static bool TryGetIconOverrideDef(int letterID, out LetterIconOverrideDef? iconOverrideDef)
    {
        return LetterIconsCache.TryGetValue(letterID, out iconOverrideDef);
    }


    public static bool TryGetIconResolver(this Letter letter, out LetterIconOverrideResolver? resolver)
    {
        return TryGetIconResolver(letter.ID, out resolver);
    }

    public static bool TryGetIconResolver(int letterID, out LetterIconOverrideResolver? resolver)
    {
        return ResolverCache.TryGetValue(letterID, out resolver);
    }


    public static bool TryGetLetterIcon(this Letter letter, out Texture2D? icon)
    {
        return TryGetLetterIcon(letter.ID, out icon);
    }

    public static bool TryGetLetterIcon(int letterID, out Texture2D? icon)
    {
        var success = LetterIconsCache.TryGetValue(letterID, out var def);
        var hasResolver = ResolverCache.TryGetValue(letterID, out var resolver);
        icon = resolver?.Icon ?? def?.Icon;
        return success || hasResolver;
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
    /// <param name="context"></param>
    public static void TryOverrideIconForDef(Def? def, params object[] context)
    {
        if (def == null) return;

        if (TryGetIconOverrideDefForDef(def, out var iconOverrideDef) && iconOverrideDef != null)
        {
            TryOverrideMostRecentLetterIcon(iconOverrideDef, context);
        }
    }

    public static void ClearCaches()
    {
        LetterIconsCache.Clear();
        ResolverCache.Clear();
    }
}
