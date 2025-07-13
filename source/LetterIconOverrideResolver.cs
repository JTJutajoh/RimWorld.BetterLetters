using UnityEngine;

namespace BetterLetters;

public abstract class LetterIconOverrideResolver : IExposable
{
    // ReSharper disable once InconsistentNaming
    public LetterIconOverrideDef? def;

    private Texture2D? _iconInt;
    private string? _resolvedPathInt;

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

    protected abstract void ResolvePath(out string? iconPath, params object[] context);

    public void TryResolve(LetterIconOverrideDef overrideDef, params object[] context)
    {
        def = overrideDef;
        ResolvePath(out _resolvedPathInt, context);
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref def, "def");
        Scribe_Values.Look(ref _resolvedPathInt, "iconPath");
    }
}
