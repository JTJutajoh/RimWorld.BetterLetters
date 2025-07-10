using UnityEngine;

namespace BetterLetters;

public class LetterIconOverrideResolver
{
    // ReSharper disable once InconsistentNaming
    public LetterIconOverrideDef? def;

    public virtual Texture2D Resolve(params object[] context)
    {
        if (def == null)
            throw new System.Exception("LetterIconOverrideResolver.Resolve called before def was set");
        return ContentFinder<Texture2D>.Get(def.iconPath)!;
    }
}
