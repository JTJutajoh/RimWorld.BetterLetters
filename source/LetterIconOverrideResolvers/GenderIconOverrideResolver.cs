using System.Linq;
using JetBrains.Annotations;

namespace BetterLetters.LetterIconOverrideResolvers;

[UsedImplicitly]
internal class GenderIconOverrideResolver : LetterIconOverrideResolver
{
    protected override void ResolvePath(out string? iconPath, params object[] context)
    {
        var gender = context.OfType<Gender>().FirstOrDefault();
        iconPath = gender switch
        {
            Gender.Female => def!.iconPath + "_Female",
            Gender.Male => def!.iconPath + "_Male",
            _ => null
        };
    }
}
