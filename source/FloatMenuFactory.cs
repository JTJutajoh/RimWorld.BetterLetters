using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse.Sound;

namespace BetterLetters;

internal static class FloatMenuFactory
{
    /// <summary>
    /// Catch-all helper function to create float menus related to this mod.<br />
    /// Used partially to help with multi-version support since not all features are available in all versions.
    /// </summary>
    /// <param name="label">Text to display for the option</param>
    /// <param name="action">Callback to perform</param>
    /// <param name="priority">Order in the resulting float menu</param>
    /// <param name="iconTex">(Optional) Icon to add to this menu option (RW 1.6+)</param>
    /// <param name="iconColor">(Optional) Color tinting of icon to add to this menu option (RW 1.6+)</param>
    internal static FloatMenuOption MakeFloatMenuOption(
        string label,
        Action action,
        MenuOptionPriority priority = MenuOptionPriority.Default,
        Texture2D? iconTex = null,
        Color? iconColor = null
    )
    {
#if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5) // RW 1.6+ Created a new constructor for icons
        return new FloatMenuOption(
            label: label,
            action: action,
            priority: priority,
            iconTex: iconTex!,
            iconColor: iconColor ?? Color.white
        );
#else
        return new FloatMenuOption(
            label: label,
            action: action,
            priority: priority,
            itemIcon: iconTex!,
            iconColor: iconColor ?? Color.white
        );
#endif
    }

    internal static FloatMenuOption PinFloatMenuOption(Letter letter, Action? onPinned = null)
    {
        return MakeFloatMenuOption(
            "BetterLetters_Pin".Translate(),
            () =>
            {
                letter.Pin();
                onPinned?.Invoke();
            },
            (MenuOptionPriority)5,
            Icons.PinFloatMenu,
            Color.white
        );
    }

    /// <summary>
    /// Helper extension method to create and show a standard set of snooze float menu options for a given letter.
    /// </summary>
    /// <param name="letter">The letter to snooze</param>
    /// <param name="onClicked">(Optional) Extra callback called when a given option is clicked, with the created snooze passed along to it.</param>
    /// <param name="includeDialog">If the "Snooze for..." option to open the <see cref="Dialog_Snooze"/> will be added</param>
    /// <param name="includeRecent">If a list of recent snoozes should be added to the list</param>
    /// <param name="extraFloatMenuOptions"></param>
    internal static void ShowSnoozeFloatMenu(
        this Letter letter,
        Action<Snooze?>? onClicked = null,
        bool includeDialog = true,
        bool includeRecent = true,
        List<FloatMenuOption>? extraFloatMenuOptions = null
    )
    {
        var floatMenu = SnoozeFloatMenu(letter, onClicked, includeDialog, includeRecent, extraFloatMenuOptions);
        Find.WindowStack?.Add(floatMenu);
        SoundDefOf.FloatMenu_Open?.PlayOneShotOnCamera();
    }

    private static FloatMenu SnoozeFloatMenu(
        Letter letter,
        Action<Snooze?>? onClicked = null,
        bool includeDialog = true,
        bool includeRecent = true,
        List<FloatMenuOption>? extraFloatMenuOptions = null
    )
    {
        var floatMenuOptions = SnoozeFloatMenuOptions(letter, onClicked, includeDialog, includeRecent);
        if (extraFloatMenuOptions is not null)
            floatMenuOptions.AddRange(extraFloatMenuOptions);
        return new FloatMenu(floatMenuOptions);
    }

    internal static List<FloatMenuOption> SnoozeFloatMenuOptions(Letter letter, Action<Snooze?>? onClicked = null,
        bool includeDialog = true, bool includeRecent = true)
    {
        const float minRemaining1Hour = GenDate.TicksPerHour * 1.25f;
        const float minRemaining1Day = GenDate.TicksPerDay + GenDate.TicksPerHour;
        var floatMenuOptions = new List<FloatMenuOption>();

        var remainingTicks = letter.RemainingTicks();

        // Standard options added to every float menu
        if (remainingTicks == -1 || remainingTicks > minRemaining1Hour)
            floatMenuOptions.Add(Snooze1HrFloatMenuOption(letter, onClicked));
        if (remainingTicks == -1 || remainingTicks > minRemaining1Day)
            floatMenuOptions.Add(Snooze1DayFloatMenuOption(letter, onClicked));

        // Recents
        if (includeRecent)
            floatMenuOptions.AddRange(RecentSnoozeDurationsFloatMenuOptions(letter, onClicked));

        // Snooze For... dialog
        if (includeDialog)
            floatMenuOptions.Add(SnoozeDialogFloatMenuOption(letter, onClicked));

        return floatMenuOptions;
    }

    private static FloatMenuOption Snooze1HrFloatMenuOption(Letter letter,
        Action<Snooze?>? onClicked = null)
    {
        return MakeFloatMenuOption(
            "BetterLetters_SnoozeFor1Hour".Translate(),
            () =>
            {
                var snooze = WorldComponent_SnoozeManager.AddSnooze(letter, GenDate.TicksPerHour);
                onClicked?.Invoke(snooze);
            },
            (MenuOptionPriority)3,
            Icons.SnoozeFloatMenu,
            new Color(0.2f, 0.2f, 0.2f)
        );
    }

    private static FloatMenuOption Snooze1DayFloatMenuOption(Letter letter,
        Action<Snooze?>? onClicked = null)
    {
        return MakeFloatMenuOption(
            "BetterLetters_SnoozeFor1Day".Translate(),
            () =>
            {
                var snooze = WorldComponent_SnoozeManager.AddSnooze(letter, GenDate.TicksPerDay);
                onClicked?.Invoke(snooze);
            },
            (MenuOptionPriority)3,
            Icons.SnoozeFloatMenu,
            new Color(0.4f, 0.4f, 0.4f)
        );
    }

    private static FloatMenuOption SnoozeDialogFloatMenuOption(Letter letter,
        Action<Snooze?>? onSnoozed = null)
    {
        return MakeFloatMenuOption(
            "BetterLetters_SnoozeForFloatMenuOption".Translate(),
            () => { WorldComponent_SnoozeManager.ShowSnoozeDialog(letter, onSnoozed); },
            (MenuOptionPriority)1,
            Icons.SnoozeFloatMenu,
            Color.white
        );
    }

    private static List<FloatMenuOption> RecentSnoozeDurationsFloatMenuOptions(Letter letter,
        Action<Snooze?>? onClicked = null)
    {
        var floatMenuOptions = new List<FloatMenuOption>();

        var recentDurations = Settings.RecentSnoozeDurations.ListFullCopy();

        if (recentDurations is null)
            return floatMenuOptions;

        recentDurations.Reverse();

        foreach (var duration in recentDurations)
        {
            floatMenuOptions.Add(MakeFloatMenuOption(
                "BetterLetters_SnoozeForRecent".Translate(duration.ToStringTicksToPeriod()),
                () =>
                {
                    var snooze = WorldComponent_SnoozeManager.AddSnooze(letter, duration);
                    onClicked?.Invoke(snooze);
                },
                (MenuOptionPriority)2,
                Icons.SnoozeFloatMenu,
                new Color(0.4f, 0.5f, 0.6f)
            ));
        }

        return floatMenuOptions;
    }
}
