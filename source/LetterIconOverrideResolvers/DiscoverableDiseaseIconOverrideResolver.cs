using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;

namespace BetterLetters.LetterIconOverrideResolvers;

[UsedImplicitly]
internal class DiscoverableDiseaseIconOverrideResolver : LetterIconOverrideResolver
{
    private HediffDef? _hediff;
    private Pawn? _pawn;

    public override Texture2D Resolve(params object[] context)
    {
        if (def != null)
        {
            _hediff ??= context.OfType<HediffWithComps>().FirstOrDefault()?.def;
            _pawn ??= context.OfType<Pawn>().FirstOrDefault();
            return ContentFinder<Texture2D>.Get(ResolvedPath)!;
        }

        return base.Resolve();
    }

    /// <inheritdoc />
    public override string ResolvedPath
    {
        get
        {
            if (_pawn?.IsAnimal ?? false)
            {
                return def!.iconPath + "_Animal";
            }

            if (_hediff == HediffDefOf.WoundInfection)
            {
                return def!.iconPath + "_Organic";
            }

            if (_hediff == HediffDefOf.DrugOverdose)
            {
                return def!.iconPath + "_Overdose";
            }

            return base.ResolvedPath;
        }
    }

    public override void ExposeData()
    {
        Scribe_Defs.Look(ref _hediff, "hediff");
        Scribe_References.Look(ref _pawn, "pawn");
    }
}
