using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;

namespace BetterLetters.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("RaidLetter_AddDetails")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_IncidentWorkerRaid_GetLetterLabel_AddDetails
{
    private static readonly List<PawnsArrivalModeDef> DropPodArrivalModes = new()
    {
        PawnsArrivalModeDefOf.CenterDrop,
        PawnsArrivalModeDefOf.EdgeDrop,
#if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5)
        PawnsArrivalModeDefOf.RandomDrop,
        PawnsArrivalModeDefOf.SpecificDropDebug
#endif
    };

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "GetLetterLabel")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void AddToRaidLabel(IncidentWorker_RaidEnemy __instance,
        IncidentParms parms,
        ref string __result)
    {
        if (!Settings.ModifyLetterText) return;

        // Add a prefix if the raid is a drop pod raid
        if (Settings.RaidAddDropPod && DropPodArrivalModes.Contains(parms.raidArrivalMode))
        {
            __result = "BetterLetters_Raid_DropPodPrefix".Translate() + " " + __result;
        }

        if (parms.raidStrategy?.defName is null) return;

        List<string> postfixes = new();
        // Add a postfix with info about the type of raid
        if (Settings.RaidAddRaidStrategy && parms.raidStrategy.defName.Contains("Immediate"))
        {
            postfixes.Add("BetterLetters_Raid_Immediate".Translate());
        }

        if (Settings.RaidAddRaidStrategy && parms.raidStrategy.defName.Contains("Breaching"))
        {
            postfixes.Add("BetterLetters_Raid_Breaching".Translate());
        }

        if (Settings.RaidAddRaidStrategy && parms.raidStrategy.defName.Contains("Sappers"))
        {
            postfixes.Add("BetterLetters_Raid_Sappers".Translate());
        }

        if (Settings.RaidAddRaidStrategy && parms.raidStrategy.defName == "StageThenAttack")
        {
            postfixes.Add("BetterLetters_Raid_Delayed".Translate());
        }

        if (Settings.RaidAddRaidStrategy && parms.raidStrategy.defName == "EmergeFromWater")
        {
            postfixes.Add("BetterLetters_Raid_Emerge".Translate());
        }

        if (Settings.RaidAddPawnCount)
        {
            var pawns = parms.pawnCount;
            postfixes.Add(pawns != 1
                ? "BetterLetters_Raid_PawnsCount".Translate(pawns)
                : "BetterLetters_Raid_1Pawn".Translate());
        }

        if (Settings.RaidAddGroupCount && parms.PawnGroupCount > 1)
        {
            postfixes.Add("BetterLetters_Raid_GroupsCount".Translate(parms.PawnGroupCount));
        }

        if (postfixes.Count > 0)
        {
            __result += " (" + string.Join(", ", postfixes) + ")";
        }
    }
}
