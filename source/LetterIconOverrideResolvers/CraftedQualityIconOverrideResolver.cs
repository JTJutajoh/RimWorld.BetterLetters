using System.Linq;
using JetBrains.Annotations;
using RimWorld;

namespace BetterLetters.LetterIconOverrideResolvers;

[UsedImplicitly]
internal class CraftedQualityIconOverrideResolver :LetterIconOverrideResolver
{
    protected override void ResolvePath(out string? iconPath, params object[] context)
    {
        var thing = context.OfType<Thing>().FirstOrDefault();
        if (thing is not null && thing.TryGetQuality(out var quality))
        {
            iconPath = quality switch
            {
                QualityCategory.Masterwork => def!.iconPath + "_Masterwork",
                QualityCategory.Legendary => def!.iconPath + "_Legendary",
                _ => null
            };
        }
        else
        {
            iconPath = null;
        }
    }
}
