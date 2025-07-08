using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace BetterLetters;

internal static class FloatMenuOptionFactory
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
    public static FloatMenuOption MakeFloatMenuOption(
        string label,
        Action action,
        MenuOptionPriority priority = MenuOptionPriority.Default,
        Texture2D? iconTex = null,
        Color? iconColor = null
    )
    {
#if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5)
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

    //TODO: Refactor all the FloatMenus throughout the mod to use a generic static function here

    public static FloatMenuOption PinFloatMenuOption(Letter letter, Action? onPinned = null)
    {
        return MakeFloatMenuOption(
            "BetterLetters_Pin".Translate(),
            action: () =>
            {
                letter.Pin();
                onPinned?.Invoke();
            },
            iconTex: Icons.PinFloatMenu,
            iconColor: Color.white
        );
    }

    public static FloatMenuOption Snooze1HrFloatMenuOption(Letter letter,
        Action<Snooze?>? onClicked = null)
    {
        return MakeFloatMenuOption(
            "BetterLetters_SnoozeFor1Hour".Translate(),
            action: () =>
            {
                var snooze = WorldComponent_SnoozeManager.AddSnooze(letter, GenDate.TicksPerHour);
                onClicked?.Invoke(snooze);
            },
            iconTex: Icons.SnoozeFloatMenu,
            iconColor: new Color(0.2f, 0.2f, 0.2f)
        );
    }

    public static FloatMenuOption Snooze1DayFloatMenuOption(Letter letter,
        Action<Snooze?>? onClicked = null)
    {
        return MakeFloatMenuOption(
            "BetterLetters_SnoozeFor1Day".Translate(),
            action: () =>
            {
                var snooze = WorldComponent_SnoozeManager.AddSnooze(letter, GenDate.TicksPerDay);
                onClicked?.Invoke(snooze);
            },
            iconTex: Icons.SnoozeFloatMenu,
            iconColor: new Color(0.4f, 0.4f, 0.4f)
        );
    }

    public static FloatMenuOption SnoozeDialogFloatMenuOption(Letter letter,
        Action<Snooze?>? onSnoozed = null)
    {
        return MakeFloatMenuOption(
            "BetterLetters_SnoozeForFloatMenuOption".Translate(),
            action: () => { WorldComponent_SnoozeManager.ShowSnoozeDialog(letter, onSnoozed); },
            iconTex: Icons.SnoozeFloatMenu,
            iconColor: Color.white
        );
    }

    internal static List<FloatMenuOption> RecentSnoozeDurationsFloatMenuOptions(Letter letter,
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
                action: () =>
                {
                    var snooze = WorldComponent_SnoozeManager.AddSnooze(letter, duration);
                    onClicked?.Invoke(snooze);
                },
                iconTex: Icons.SnoozeFloatMenu,
                iconColor: new Color(0.4f, 0.5f, 0.6f)
            ));
        }

        return floatMenuOptions;
    }
}
