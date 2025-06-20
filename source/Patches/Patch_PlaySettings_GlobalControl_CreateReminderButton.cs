using System;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;

namespace BetterLetters.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("PlaySettings_CreateReminderButton")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_PlaySettings_GlobalControl_CreateReminderButton
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls")]
    [UsedImplicitly]
    private static void DoPlaySettingsGlobalControls(WidgetRow row, bool worldView)
    {
        if (!Settings.DoCreateReminderPlaySetting) return;

        try
        {
            var texture = LetterUtils.Icons.ReminderSmall;
            if (row.ButtonIcon(texture, "BetterLetters_PlaySettingsCreateReminder".Translate()))
            {
                Find.WindowStack?.Add(new Dialog_Reminder());
            }
        }
        catch (Exception e)
        {
            Log.Exception(e, "Play Settings Global Controls create reminder button");
        }
    }
}
