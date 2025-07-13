using UnityEngine;

namespace BetterLetters;

public abstract class LetterIconOverrideResolver : IExposable
{
    // ReSharper disable once InconsistentNaming
    public LetterIconOverrideDef? def;

    private Texture2D? iconInt = null;
    private string? resolvedPathInt = null;

    public Texture2D Icon
    {
        get
        {
            if (iconInt != null) return iconInt;

            if (resolvedPathInt is not null)
            {
                iconInt = ContentFinder<Texture2D>.Get(resolvedPathInt);
            }

            return iconInt ?? def!.Icon;
        }
    }

    protected abstract void ResolvePath(out string? iconPath, params object[] context);

    public void TryResolve(LetterIconOverrideDef overrideDef, params object[] context)
    {
        def = overrideDef;
        ResolvePath(out resolvedPathInt, context);
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref def, "def");
        Scribe_Values.Look(ref resolvedPathInt, "iconPath");
    }
}
