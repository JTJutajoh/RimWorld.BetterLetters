using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace BetterLetters.LetterIconOverrideResolvers;

[UsedImplicitly]
internal class GenderIconOverrideResolver : LetterIconOverrideResolver
{
    private Gender? _gender;

    public override Texture2D Resolve(params object[] context)
    {
        if (def != null)
        {
            _gender ??= context.OfType<Gender>().FirstOrDefault();
            return ContentFinder<Texture2D>.Get(ResolvedPath)!;
        }

        return base.Resolve();
    }

    public override string ResolvedPath
    {
        get
        {
            return _gender switch
            {
                Gender.Female => def!.iconPath + "_Female",
                Gender.Male => def!.iconPath + "_Male",
                _ => def!.iconPath
            };
        }
    }

    /// <inheritdoc />
    public override void ExposeData()
    {
        Scribe_Values.Look(ref _gender, "gender");
    }
}
