using System.Collections.Generic;
using UnityEngine;

namespace BetterLetters;


public abstract class LetterIconOverrideResolver : IExposable
{
    // ReSharper disable once InconsistentNaming
    public LetterIconOverrideDef? def;
    private Texture2D? _iconInt;
    private string? _resolvedPathInt;

    /// The final resolved icon, lazy-loaded based on the resolved path. If no path has been resolved by <see cref="ResolvePath"/>,
    /// then the icon from the def will be used instead.
    public Texture2D Icon
    {
        get
        {
            if (_iconInt != null) return _iconInt;

            if (_resolvedPathInt is not null)
            {
                _iconInt = ContentFinder<Texture2D>.Get(_resolvedPathInt);
            }

            return _iconInt ?? def!.Icon;
        }
    }

    /// <summary>
    /// Method to be overridden in sub-classes that, given a list of variables for context, determines which icon should be used.<br />
    /// </summary>
    /// <param name="iconPath">The final resolved path, which will be saved to <see cref="_resolvedPathInt"/>.<br />
    /// If null, the default path defined in the def will be used.</param>
    /// <param name="context">A params list of objects (and boxed values) used to resolve which icon should be used.<br />
    /// In many cases, the list will be all of the arguments that were passed to the method that sent the letter. See <see cref="Patches.Patch_GenericLetterSenderInterception"/></param>
    protected virtual void ResolvePath(out string? iconPath, params object[] context)
    {
        iconPath = null;
    }

    /// <summary>
    /// Internal method called when the letter override is triggered.
    /// </summary>
    /// <param name="overrideDef">The def that this resolverClass was instantiated for</param>
    /// <param name="context">The same as the arg with the same name as on <see cref="ResolvePath"/></param>
    public void TryResolve(LetterIconOverrideDef overrideDef, params object[] context)
    {
        def = overrideDef;
        _iconInt = null;
        ResolvePath(out _resolvedPathInt, context);
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref def, "def");
        Scribe_Values.Look(ref _resolvedPathInt, "iconPath");
    }
}
