using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse.Sound;

namespace BetterLetters.Patches;

/// <summary>
/// When a <see cref="Quest"/> expires, it calls <see cref="Quest.CleanupQuestParts"/>, this patch piggybacks on that
/// to find and modify the <see cref="NewQuestLetter"/> corresponding to the quest to mark it as expired.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("ExpireQuestLetters")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_Quest_CleanupQuestParts_MarkLetter
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.CleanupQuestParts))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void FindAndMarkQuestLetter(Quest __instance)
    {
        if (!Settings.ChangeExpiredQuestLetters) return;
        // Only do the patch for quests that expired before being accepted.
        // Vanilla already handles failed timed quests after accepting
        if (__instance.State != QuestState.EndedOfferExpired || __instance.EverAccepted || __instance.hidden || __instance.GetQuestLetter() is not Letter letter) return;

        // Swap the def so it changes icon colors. This feels hacky but it doesn't seem to cause any problems
        letter.def = LetterDefOf.NeutralEvent!;
        // Add "Expired" to the label
        letter.Label = "BetterLetters_QuestExpiredLabel".Translate(__instance.name);

        // Only do the rest for letters that are still on the stack
        if (!Find.LetterStack!.LettersListForReading?.Contains(letter) ?? false) return;

        // Remove it so we can re-add it
        Find.LetterStack.RemoveLetter(letter);

        SoundDef? expireSound;
        switch (Settings.QuestExpirationSound)
        {
            case Settings.QuestExpirationSounds.LetterArrive:
                expireSound = LetterDefOf.NeutralEvent!.arriveSound;
                break;
            case Settings.QuestExpirationSounds.BadUrgentSmall:
                expireSound = LetterDefOf.NegativeEvent!.arriveSound;
                break;
            case Settings.QuestExpirationSounds.BadUrgent:
                expireSound = LetterDefOf.ThreatSmall!.arriveSound;
                break;
            case Settings.QuestExpirationSounds.BadUrgentBig:
                expireSound = LetterDefOf.ThreatBig!.arriveSound;
                break;
            case Settings.QuestExpirationSounds.RitualNegative:
                expireSound = LetterDefOf.RitualOutcomeNegative!.arriveSound;
                break;
            default:
                expireSound = null;
                break;
        }

        // The letter will already play its own arriveSound, so don't play it twice
        if (expireSound != letter.def.arriveSound)
            expireSound?.PlayOneShotOnCamera();

        // Re-add it so it gets attention
        Find.LetterStack.ReceiveLetter(letter);
    }
}
