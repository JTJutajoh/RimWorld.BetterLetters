using JetBrains.Annotations;

namespace BetterLetters.LetterIconOverrideResolvers;

[UsedImplicitly]
internal class RoyaltyIconOverrideResolver : LetterIconOverrideResolver
{
    protected override void ResolvePath(out string? iconPath, params object[] context)
    {
        iconPath = null;
    }
}
