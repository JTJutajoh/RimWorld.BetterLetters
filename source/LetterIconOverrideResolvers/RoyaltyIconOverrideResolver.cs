using System.Linq;
using JetBrains.Annotations;
using RimWorld;

namespace BetterLetters.LetterIconOverrideResolvers;

[UsedImplicitly]
internal class RoyaltyIconOverrideResolver : LetterIconOverrideResolver
{
    protected override void ResolvePath(out string? iconPath, params object[] context)
    {
        iconPath = null;
    }
}
