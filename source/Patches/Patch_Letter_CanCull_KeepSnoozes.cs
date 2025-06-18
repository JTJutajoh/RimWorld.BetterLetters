using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace BetterLetters.Patches;

/// Simple patch that prevents snoozed letters from being culled from the archive the same way vanilla prevents
/// pinned letters from being culled.
[HarmonyPatch]
[HarmonyPatchCategory("Letter_CanCull_KeepSnoozes")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_Letter_CanCull_KeepSnoozes
{
    static IEnumerable<MethodBase> TargetMethods() => new[]
        { typeof(Letter).GetInterfaceProperty(typeof(IArchivable), "CanCullArchivedNow") };

    // [HarmonyPatch(typeof(Letter), "Rimworld.IArchivable.get_CanCullArchivedNow")]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void CanCullArchivedNow(ref bool __result, Letter __instance)
    {
        if (__instance.IsSnoozed())
        {
            __result = false;
        }
    }
}