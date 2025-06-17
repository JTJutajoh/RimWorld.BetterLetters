using System;
using DarkLog;
using RimWorld;
using Verse;

namespace BetterLetters.Patches;

internal static class PlaySettingsGlobalControlCreateReminderPatch
{
    private static void DoPlaySettingsGlobalControls(WidgetRow row, bool worldView)
    {
        if (!Settings.DoCreateReminderPlaySetting) return;
        
        try
        {
            // if (worldView) return;

            var texture = LetterUtils.Icons.ReminderSmall;
            if (row.ButtonIcon(texture, "BetterLetters_PlaySettingsCreateReminder".Translate()))
            {
                Find.WindowStack.Add(new Dialog_Reminder());
            }
        }
        catch (Exception e)
        {
            LogPrefixed.Exception(e, "Play Settings Global Controls create reminder button");
        }
    }
}