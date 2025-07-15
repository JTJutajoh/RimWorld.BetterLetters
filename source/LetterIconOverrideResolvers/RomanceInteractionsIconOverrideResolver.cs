using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;

namespace BetterLetters.LetterIconOverrideResolvers;

[UsedImplicitly]
internal class RomanceInteractionsIconOverrideResolver : LetterIconOverrideResolver
{
    protected override void ResolvePath(out string? iconPath, params object[] context)
    {
        iconPath = def!.iconPath;

        var interactionDef = context.OfType<InteractionDef>().FirstOrFallback();

        if (interactionDef == null) return;

        var pawnInitiatorInteractionsTracker = context.OfType<Pawn_InteractionsTracker>().FirstOrFallback();
        if (pawnInitiatorInteractionsTracker is null) return;

        var pawnInitiator = AccessTools.Field(typeof(Pawn_InteractionsTracker), "pawn")
            ?.GetValue(pawnInitiatorInteractionsTracker) as Pawn;
        var pawnRecipient = context.OfType<Pawn>().FirstOrFallback();

        if (pawnInitiator is null || pawnRecipient is null) return;

        if (interactionDef == InteractionDefOf.MarriageProposal)
        {
            foreach (var relation in pawnInitiator.GetLoveRelations(false, true) ?? new List<DirectPawnRelation>())
            {
                if (relation.otherPawn != pawnRecipient) continue;
                if (relation.def == PawnRelationDefOf.Fiance)
                {
                    iconPath = "UI/Letters/LetterMarriageOn";
                    return;
                }
            }

            iconPath = "UI/Letters/LetterBreakup";
        }
        else if (interactionDef == InteractionDefOf.RomanceAttempt)
        {
            foreach (var relation in pawnInitiator.GetLoveRelations(false, true) ?? new List<DirectPawnRelation>())
            {
                if (relation.otherPawn != pawnRecipient) continue;
                if (relation.def != null && LovePartnerRelationUtility.IsLovePartnerRelation(relation.def))
                {
                    iconPath = "UI/Letters/LetterNewLovers";
                    return;
                }
            }

            iconPath = "UI/Letters/LetterBreakup";
        }
    }
}
