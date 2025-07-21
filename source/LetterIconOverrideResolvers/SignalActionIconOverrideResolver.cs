using System.Linq;
using JetBrains.Annotations;
using RimWorld;

namespace BetterLetters.LetterIconOverrideResolvers;

[UsedImplicitly]
internal class SignalActionIconOverrideResolver : LetterIconOverrideResolver
{
    /// <inheritdoc />
    protected override void ResolvePath(out string? iconPath, params object[] context)
    {
        var signalActionLetter = context.OfType<SignalAction_Letter>().FirstOrDefault();
        if (signalActionLetter is null)
        {
            iconPath = null;
            return;
        }

        switch (signalActionLetter.letterLabelKey)
        {
            case "LetterLabelAncientShrineWarning":
                iconPath = "UI/Letters/LetterAncientDanger";
                break;
            case "LetterLabelMechanitorCasketOpened":
                iconPath = "UI/Letters/LetterBiotech";
                break;
            case "LetterLabelDreadmeldWarning":
            case "LetterLabelObeliskDiscovered":
                iconPath = "UI/Letters/LetterAnomaly";
                break;
            case "LetterLabelInsectQueenWarning":
            case "LetterAncientGravEngineDiscoveredLabel":
                iconPath = "UI/Letters/LetterOdyssey";
                break;
            default:
                iconPath = null;
                break;
        }
    }
}
