using System.Linq;
using JetBrains.Annotations;
using RimWorld;

namespace BetterLetters.LetterIconOverrideResolvers;

[UsedImplicitly]
internal class DiscoverableDiseaseIconOverrideResolver : LetterIconOverrideResolver
{
    protected override void ResolvePath(out string? iconPath, params object[] context)
    {
        iconPath = null;

        var hediff = context.OfType<HediffWithComps>().FirstOrDefault()?.def;
        if (hediff is null)
        {
            var hediffComp = context.OfType<HediffComp_Discoverable>().FirstOrDefault();
            hediff = hediffComp?.parent?.def;
        }
        var pawn = context.OfType<Pawn>().FirstOrDefault();

        if (pawn?.IsAnimal ?? false)
        {
            iconPath = def!.iconPath + "_Animal";
        }

        if (hediff == HediffDefOf.WoundInfection)
        {
            iconPath = def!.iconPath + "_Organic";
        }

        if (hediff == HediffDefOf.DrugOverdose)
        {
            iconPath = def!.iconPath + "_Overdose";
        }
    }
}
