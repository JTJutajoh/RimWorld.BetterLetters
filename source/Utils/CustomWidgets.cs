using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse.Sound;

namespace BetterLetters.Utils;

/// <summary>
///     Helper static class that contains methods to create common widgets I use in my mods.<br />
///     Many of these are implemented as extension methods, some of which are overloads for vanilla methods (such as ones
///     for <see cref="Listing_Standard" />).
/// </summary>
internal static class CustomWidgets
{
    private static string SliderMaxLabel => "BetterLetters_SliderRightLabel".Translate(
        Mathf.RoundToInt(MaxDuration * GenDate.TicksPerDay).ToStringTicksToPeriod());

    private static string SliderDurationLabel(float durationDays) =>
        "BetterLetters_SliderLabel".Translate(Mathf.RoundToInt(durationDays * GenDate.TicksPerDay)
            .ToStringTicksToPeriod());

    private static string _editBufferSnooze = "";

    internal static LetterUtils.TimeUnits SnoozeTimeUnit = LetterUtils.TimeUnits.Hours;

    private static void RefreshEditBuffer(int durationTicks)
    {
        if (_editBufferSnooze == "") return;
        _editBufferSnooze =
            durationTicks == 0 ? "" : ((int)Math.Floor(durationTicks / (float)SnoozeTimeUnit)).ToStringCached()!;
    }

    internal static void SnoozeSettings(float x,
        ref float y,
        float width,
        ref int durationTicks,
        float paddingLeft = 32f,
        float paddingRight = 32f,
        float paddingTop = 8f,
        int? maxDurationOverride = null,
        bool showEndDate = true
    )
    {
        const float minWidth = 400f;
        const float maxWidth = 600f;
        // When the chosen unit is Ticks, the buttons change to adjust by this amount.
        // Used the TPS of max 3x speed
        const int ticksMultiplier = 180;
        const float intEntryWidth = 240f;
        const float unitButtonWidth = 90f;
        const float buttonRowHeight = 32f;
        const float spacing = 8f;


        int years;
        int quadrums;
        int days;
        float hoursFloat;
        durationTicks.TicksToPeriod(out years, out quadrums, out days, out hoursFloat);
        int hours = (int)hoursFloat;

        RefreshEditBuffer(durationTicks);

        // Cap the widget width and center it within the supplied width
        if (width > maxWidth)
        {
            x += (width - maxWidth) / 2f;
            width = maxWidth;
        }

        width = Mathf.Max(width, minWidth);

        // Apply padding
        x += paddingLeft;
        width -= paddingLeft + paddingRight;
        y += paddingTop;

        // Duration label
        Text.Anchor = TextAnchor.UpperCenter;
        var durationString = durationTicks.ToStringTicksToPeriodVeryVerbose(Color.cyan);
        if (durationTicks <= Settings.SnoozeTickPeriod && showEndDate)
        {
            durationString = durationString + " " + "BetterLetters_MinimumDuration".Translate(durationTicks);
            TooltipHandler.TipRegionByKey(new Rect(x, y, width, 32f), "BetterLetters_MinimumDuration_Tooltip");
        }
        else if (durationTicks >= (maxDurationOverride ?? Settings.MaxSnoozeDuration))
        {
            durationString = durationString + " " + "BetterLetters_MaximumDuration".Translate();
            TooltipHandler.TipRegionByKey(new Rect(x, y, width, 32f), "BetterLetters_MaximumDuration_Tooltip");
        }

        Widgets.Label(x, ref y, width, "BetterLetters_SnoozeFor".Translate(durationString));
        Text.Anchor = TextAnchor.UpperLeft;

        // End date label
        if (showEndDate)
        {
            Text.Anchor = TextAnchor.UpperCenter;
            var endDateString =
                GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs + durationTicks, QuestUtility.GetLocForDates());
            Text.Font = GameFont.Tiny;
            GUI.color = ColorLibrary.Beige;
            Widgets.Label(x, ref y, width, "BetterLetters_SnoozeUntil".Translate(endDateString));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Create the rects for the buttons
        var centeredRect =
            new Rect(x, y, width, buttonRowHeight).MiddlePartPixels(unitButtonWidth + intEntryWidth + spacing,
                buttonRowHeight);
        var unitButtonRect = centeredRect.LeftPartPixels(unitButtonWidth);
        var intEntryRect = centeredRect.RightPartPixels(intEntryWidth);
        unitButtonRect.xMax -= spacing / 2;
        intEntryRect.xMin += spacing / 2;

        // Button to switch which unit is being controlled by the adjustment widget(s)
        if (Widgets.ButtonText(unitButtonRect, SnoozeTimeUnit.ToString()))
        {
            var floatMenuOptions = new List<FloatMenuOption>()
            {
                new("BetterLetters_Ticks".Translate(), () => SnoozeTimeUnit = LetterUtils.TimeUnits.Ticks),
                new("BetterLetters_Hours".Translate(), () => SnoozeTimeUnit = LetterUtils.TimeUnits.Hours),
                new("BetterLetters_Days".Translate(), () => SnoozeTimeUnit = LetterUtils.TimeUnits.Days),
                new("BetterLetters_Seasons".Translate(), () => SnoozeTimeUnit = LetterUtils.TimeUnits.Seasons),
                new("BetterLetters_Years".Translate(), () => SnoozeTimeUnit = LetterUtils.TimeUnits.Years),
                new("BetterLetters_Decades".Translate(), () => SnoozeTimeUnit = LetterUtils.TimeUnits.Decades),
            };
            Find.WindowStack?.Add(new FloatMenu(floatMenuOptions));
            SoundDefOf.FloatMenu_Open!.PlayOneShotOnCamera();
        }

        TooltipHandler.TipRegion(unitButtonRect, "BetterLetters_TimeUnitTooltip".Translate(SnoozeTimeUnit.ToString()));

        // Parse the text input field into the number of the currently chosen unit
        var numOfUnit = _editBufferSnooze.Length != 0 ? Math.Max(0, int.Parse(_editBufferSnooze)) : 0;
        var remainderTicks = durationTicks % (int)SnoozeTimeUnit;

        // Adjustment widget(s)
        Widgets.IntEntry(intEntryRect, ref numOfUnit, ref _editBufferSnooze,
            SnoozeTimeUnit == LetterUtils.TimeUnits.Ticks ? ticksMultiplier : 1);

        y += intEntryRect.height;

        // The resulting number of ticks is:
        // (X <unit> + Y <remainder>)
        // ex: (3 hours + 500 ticks) or (7 days + 38,477 ticks)
        // This way the user can adjust a unit without losing the smaller units that they've already set
        durationTicks = (int)Mathf.Clamp(numOfUnit * (int)SnoozeTimeUnit + remainderTicks, 0,
            maxDurationOverride ?? Settings.MaxSnoozeDuration);
    }


    /// <summary>
    ///     Extension method for <see cref="Listing_Standard" />.<br />
    ///     Creates a nicely formatted set of widgets to adjust and set an integer value corresponding to a field in
    ///     <see cref="Settings" />. The supplied setting name must exactly match the field name.
    /// </summary>
    /// <param name="listingStandard">Listing standard instance</param>
    /// <param name="value">The int value to be adjusted by this widget.</param>
    /// <param name="settingName">The name of the field on <see cref="Settings" /> that this widget modifies.</param>
    /// <param name="editBuffer">The string buffer used to temporarily store and edit the value.</param>
    /// <param name="label">Optional label for the block. If null, no label will be shown.</param>
    /// <param name="multiplier">A multiplier value applied during input adjustment.</param>
    /// <param name="min">The minimum allowed value for the integer.</param>
    /// <param name="max">The maximum allowed value for the integer.</param>
    /// <param name="doDefaultButton">Optionally, disable the "Default" button.</param>
    internal static void IntSetting(this Listing_Standard listingStandard,
        ref int value,
        string settingName,
        ref string editBuffer,
        string? label = null,
        int multiplier = 1,
        int min = 0,
        int max = 999999,
        bool doDefaultButton = true)
    {
        if (label != null) listingStandard.Label(label);
        var labelRect = listingStandard.Label(Settings.GetSettingLabel(settingName,
            true));
        var tooltip = Settings.GetSettingTooltip(settingName);
        if (tooltip != "")
            TooltipHandler.TipRegion(
                new Rect(labelRect.xMin,
                    labelRect.yMin,
                    labelRect.width,
                    labelRect.height + 30f),
                tooltip);

        IntEntry(listingStandard,
            ref value,
            ref editBuffer,
            (int)Settings.DefaultSettings[settingName],
            multiplier,
            min,
            max);
    }

    /// <summary>
    ///     Extension method for <see cref="Listing_Standard" />.<br />
    ///     Wrapper for vanilla <see cref="Listing_Standard.IntAdjuster" /> that includes a button to reset to a specified
    ///     default value.<br />
    ///     Ensures integer value is clamped within the specified range and supports editing through a buffer.<br />
    ///     Acts as an overload for vanilla <see cref="Listing_Standard.IntEntry" />.
    /// </summary>
    /// <param name="listingStandard">The Listing_Standard instance for extending functionality.</param>
    /// <param name="value">The integer value to be modified.</param>
    /// <param name="defaultValue">The default value to reset to when the default button is clicked.</param>
    /// <param name="editBuffer">The string buffer used to temporarily store and edit the value.</param>
    /// <param name="multiplier">A multiplier value applied during input adjustment.</param>
    /// <param name="min">The minimum allowed value for the integer.</param>
    /// <param name="max">The maximum allowed value for the integer.</param>
    internal static void IntEntry(this Listing_Standard listingStandard,
        ref int value,
        ref string editBuffer,
        int defaultValue,
        int multiplier = 1,
        int min = 0,
        int max = 999999)
    {
#if !(v1_2 || v1_3 || v1_4 || v1_5) // RW 1.6 fixed a bug with IntEntry that forced the value to be a positive number
        listingStandard.IntEntry(ref value, ref editBuffer, multiplier, min);
#else
        listingStandard.IntEntryWithNegative(ref value, ref editBuffer, multiplier, min);
#endif
        listingStandard.IntSetter(ref value, defaultValue, "Default".Translate());

        value = Mathf.Clamp(value, min, max);
    }

    private static float MaxDuration => Settings.MaxSnoozeDuration;

    internal static void SnoozeIconButton(Letter letter, Rect rect)
    {
        var snoozed = letter.IsSnoozed();
        var tex = snoozed ? Icons.SnoozeIcon : Icons.SnoozeOutline;
        if (Widgets.ButtonImage(rect, tex))
        {
            if (snoozed)
            {
                WorldComponent_SnoozeManager.RemoveSnooze(letter);
                SoundDefOf.Tick_Low!.PlayOneShotOnCamera();
                snoozed = false;
            }
            else
            {
                void OnSnooze(WorldComponent_SnoozeManager.Snooze? snooze)
                {
                    SoundDefOf.Tick_High!.PlayOneShotOnCamera();
                    snoozed = true;
                }

                var floatMenuOptions = new List<FloatMenuOption>
                {
                    LetterUtils.Snooze1HrFloatMenuOption(letter, OnSnooze),
                    LetterUtils.Snooze1DayFloatMenuOption(letter, OnSnooze),
                    LetterUtils.SnoozeDialogFloatMenuOption(letter, OnSnooze)
                };

                Find.WindowStack?.Add(new FloatMenu(floatMenuOptions));
                SoundDefOf.FloatMenu_Open!.PlayOneShotOnCamera();
            }
        }

        if (Mouse.IsOver(rect))
        {
            if (snoozed)
            {
                WorldComponent_SnoozeManager.Snoozes[letter]?.DoTipRegion(rect);
            }
            else
            {
                TooltipHandler.TipRegionByKey(rect, "BetterLetters_SnoozeQuestTooltip");
            }
        }
    }

    internal static void PinIconButton(Letter letter, Rect rect)
    {
        var pinned = letter.IsPinned();
        var tex = pinned ? Icons.PinIcon : Icons.PinOutline;
        if (Widgets.ButtonImage(rect, tex))
        {
            if (pinned)
            {
                letter.Unpin();
                SoundDefOf.Tick_Low!.PlayOneShotOnCamera();
            }
            else
            {
                letter.Pin();
                SoundDefOf.Tick_High!.PlayOneShotOnCamera();
            }
        }

        if (Mouse.IsOver(rect))
        {
            var key = pinned ? "BetterLetters_UnPinQuestTooltip" : "BetterLetters_PinQuestTooltip";
            TooltipHandler.TipRegionByKey(rect, key);
        }
    }

    internal static void GearIconButton(Letter _, Rect rect)
    {
#if (v1_1 || v1_2 || v1_3)
        // Mod settings window changed in RW 1.4+ and it's not really possible to open a specific one easily in 1.1-1.3
        return;
#else
        if (!Mouse.IsOver(rect.ExpandedBy(4f))) return;

        var tex = Icons.Gear;
        if (Widgets.ButtonImage(rect, tex))
        {
            Settings.CurrentTab = Settings.SettingsTab.Pinning;
            Find.WindowStack!.Add(new Dialog_ModSettings(BetterLettersMod.Instance!));
        }

        if (Mouse.IsOver(rect))
        {
            TooltipHandler.TipRegionByKey(rect, "BetterLetters_OpenSettingsTooltip");
        }
#endif
    }
}
