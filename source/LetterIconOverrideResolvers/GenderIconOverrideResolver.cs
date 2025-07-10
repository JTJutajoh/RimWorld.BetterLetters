using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace BetterLetters.LetterIconOverrideResolvers;

[UsedImplicitly]
internal class GenderIconOverrideResolver : LetterIconOverrideResolver
{
    public override Texture2D Resolve(params object[] context)
    {
        if (def != null)
        {
            var baseIconPath = def.iconPath;
            var gender = context.OfType<Gender>().FirstOrDefault();
            if (gender == Gender.Female)
                return ContentFinder<Texture2D>.Get(baseIconPath + "_Female")!;
            if (gender == Gender.Male)
                return ContentFinder<Texture2D>.Get(baseIconPath + "_Male")!;
        }

        return base.Resolve();
    }
}
