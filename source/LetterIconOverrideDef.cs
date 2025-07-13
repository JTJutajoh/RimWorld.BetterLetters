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
    /// For defs that have <see cref="iconResolverClass"/> defined, this property will not return the resolved icon,
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
            return defs;
        }
    }

    /// <summary>
    /// A list of methodinfos representing the methods that should be patched by
    /// <see cref="Patch_GenericLetterSenderInterception"/> to intercept the letter sent within the given method(s)
    /// and override its icon.
    /// </summary>
    public List<PatchTarget> PatchTargets => patchTargets ?? new List<PatchTarget>();

    public override IEnumerable<string> ConfigErrors()
    {
        if (iconPath == null || iconPath.Trim() == "")
            yield return "IconPath is null or empty";

        if (patchTargets is not null)
        {
            if (patchTargets.Count == 0)
                yield return "PatchTargets is empty";
            foreach (var configError in patchTargets.SelectMany(patchTarget => patchTarget.ConfigErrors()))
                yield return configError;
        }

        if (base.ConfigErrors() is { } errors)
            foreach (var configError in errors)
                if (configError is not null) yield return configError;
    }

    // XML defined fields
    // ReSharper disable UnassignedField.Global
    [NoTranslate] public string iconPath = "UI/Letters/LetterUnopened";

    public List<GameConditionDef> gameConditions;

    public List<IncidentDef> incidents;

    public List<PatchTarget> patchTargets;

    public Type iconResolverClass;
}
