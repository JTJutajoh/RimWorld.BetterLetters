using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;

namespace BetterLetters.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("HistoryArchivableRow")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_Archivable_GetIcon_OverrideIcon
{
    [HarmonyPatch(typeof(Letter), "RimWorld.IArchivable.ArchivedIcon", MethodType.Getter)]
    [UsedImplicitly]
    static bool Prefix(IArchivable __instance, ref Texture __result)
    {
        if (__instance is not Letter letter) return true;

        if (letter.TryGetLetterIcon(out var icon) && icon is not null)
        {
            __result = icon;
            return false;
        }

        return true;
    }
}
