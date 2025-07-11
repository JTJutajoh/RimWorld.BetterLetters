using UnityEngine;

namespace BetterLetters;

public abstract class LetterIconOverrideResolver : IExposable
{
    // ReSharper disable once InconsistentNaming
    public LetterIconOverrideDef? def;

    public virtual Texture2D Resolve(params object[] context)
    {
        if (def == null)
            throw new System.Exception("LetterIconOverrideResolver.Resolve called before def was set");
        return ContentFinder<Texture2D>.Get(ResolvedPath)!;
    }

    public virtual string ResolvedPath => def!.iconPath;

    public virtual void ExposeData()
    {
    }
}
