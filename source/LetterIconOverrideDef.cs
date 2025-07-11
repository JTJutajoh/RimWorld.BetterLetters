using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace BetterLetters;

[UsedImplicitly]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
[SuppressMessage("ReSharper", "ConvertToConstant.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class LetterIconOverrideDef : Def
{
    public Texture2D Icon => ResolvedIcon ?? ContentFinder<Texture2D>.Get(iconPath)!;

    private Texture2D? _resolvedIcon;

    private Texture2D? ResolvedIcon
    {
        get
        {
            if (_resolvedIcon != null)
                return _resolvedIcon;
            return IconResolver?.Resolve();
        }
    }

    private LetterIconOverrideResolver? _iconResolverInt;

    public LetterIconOverrideResolver? IconResolver
    {
        get
        {
            if (iconResolverClass == null)
                return null;
            if (_iconResolverInt == null)
            {
                _iconResolverInt = (LetterIconOverrideResolver)Activator.CreateInstance(iconResolverClass)!;
                _iconResolverInt.def = this;
            }

            return _iconResolverInt;
        }
        set => _iconResolverInt = value;
    }

    public List<Def> TriggeringDefs
    {
        get
        {
            List<Def> defs = new();
            if (this.gameConditions != null)
                defs.AddRange(this.gameConditions);
            if (this.incidents != null)
                defs.AddRange(this.incidents);
            return defs;
        }
    }

    public void ResolveIcon(params object[] context)
    {
        _resolvedIcon = IconResolver?.Resolve(context);
    }


    // public override IEnumerable<string> ConfigErrors()
    // {
    //     if (base.ConfigErrors() is { } errors)
    //         foreach (var configError in errors)
    //             yield return configError;
    // }

    // ReSharper disable UnassignedField.Global
    [NoTranslate] public string iconPath = "UI/Letters/LetterUnopened";

    public List<GameConditionDef> gameConditions;

    public List<IncidentDef> incidents;

    public Type iconResolverClass;
}
