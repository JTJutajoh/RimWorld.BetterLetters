using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BetterLetters.Patches;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace BetterLetters;

/// <summary>
/// Def used to define an override for a letter's icon from XML.<br />
/// </summary>
[UsedImplicitly]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
[SuppressMessage("ReSharper", "ConvertToConstant.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class LetterIconOverrideDef : Def
{
    /// <summary>
    /// The default icon that should be used as the override.
    /// </summary>
    /// <remarks>
    /// For defs that have <see cref="resolverClass"/> defined, this property will not return the resolved icon,
    /// only the base icon.
    /// </remarks>
    public Texture2D Icon => ContentFinder<Texture2D>.Get(iconPath)!;

    /// <summary>
    /// A combined list of all defs that are considered as causes of the letter(s) that this override def
    /// is overriding.
    /// </summary>
    /// <remarks>
    /// Currently, all defs will be either <see cref="GameConditionDef"/> or <see cref="IncidentDef"/>.<br />
    /// More specific triggers should be defined through <see cref="patchTargets"/>
    /// </remarks>
    public List<Def> TriggeringDefs
    {
        get
        {
            List<Def> defs = new();
            if (gameConditions != null)
                defs.AddRange(gameConditions);
            if (incidents != null)
                defs.AddRange(incidents);
            if (interactions != null)
                defs.AddRange(interactions);
            if (things != null)
                defs.AddRange(things);
            if (questScripts != null)
                defs.AddRange(questScripts);
            if (gatherings != null)
                defs.AddRange(gatherings);
            return defs;
        }
    }

    /// <summary>
    /// A list of methodinfos representing the methods that should be patched by
    /// <see cref="Patch_GenericLetterSenderInterception"/> to intercept the letter sent within the given method(s)
    /// and override its icon.
    /// </summary>
    public List<PatchTarget> PatchTargets
    {
        get
        {
            if (patchWorker is null)
                return new List<PatchTarget>();
            return patchWorker.GetPatchTargetsForDef(this) ?? new List<PatchTarget>();
        }
    }

    public override IEnumerable<string> ConfigErrors()
    {
        if (iconPath == null || iconPath.Trim() == "")
            yield return "IconPath is null or empty";

        if (patchWorker?.ConfigErrors() is { } patchErrors)
        {
            foreach (var configError in patchErrors)
                if (configError is not null)
                    yield return configError;
        }

        if (base.ConfigErrors() is { } baseErrors)
            foreach (var configError in baseErrors)
                if (configError is not null)
                    yield return configError;
    }

    // XML defined fields
    // ReSharper disable UnassignedField.Global
    [NoTranslate] public string iconPath = "UI/Letters/LetterUnopened";

    public List<GameConditionDef> gameConditions;

    public List<IncidentDef> incidents;

    public List<InteractionDef> interactions;

    public List<ThingDef> things;

    public List<QuestScriptDef> questScripts;

    public List<GatheringDef> gatherings;

    public PatchWorker patchWorker;

    public Type resolverClass;
}
