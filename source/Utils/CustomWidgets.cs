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
    internal static void TimeEntryColumn(
        Rect inRect,
        ref float curX,
        string label,
        ref int value,
        ref string editBuffer
    )
    {
        const float textFieldHeight = 32f;
        const float buttonHeight = 24f;
        const float buttonWidth = 42f;
        var columnWidth = inRect.width / 5f - 4f;

        curX += 2f;

        var columnRect = new Rect(curX, inRect.yMin, columnWidth, inRect.height);

        var textFieldRect = columnRect.MiddlePartPixels(columnWidth, textFieldHeight);
        textFieldRect = textFieldRect.MiddlePartPixels(buttonWidth, textFieldRect.height);
        var incButtonRect = columnRect with { yMin = textFieldRect.yMin - 2f - buttonHeight, height = buttonHeight };
        incButtonRect = incButtonRect.MiddlePartPixels(buttonWidth, incButtonRect.height);
        var labelRect = columnRect with { yMin = incButtonRect.yMin - 2f - buttonHeight, height = buttonHeight };
        var decButtonRect = columnRect with { yMin = textFieldRect.yMax + 2f, height = buttonHeight };
        decButtonRect = decButtonRect.MiddlePartPixels(buttonWidth, decButtonRect.height);
        var clearButtonRect = columnRect with { yMin = decButtonRect.yMax + 2f, height = buttonHeight };
        clearButtonRect = clearButtonRect.MiddlePartPixels(buttonWidth, clearButtonRect.height);

        // Label
        Text.Anchor = TextAnchor.LowerCenter;
        Widgets.Label(labelRect, label);
        Text.Anchor = TextAnchor.UpperLeft;

        if (Widgets.ButtonText(incButtonRect, "+"))
        {
            value++;
        }

        if (Widgets.ButtonText(decButtonRect, "-"))
        {
            value--;
        }

        GUI.color = ColorLibrary.RedReadable;
        if (Widgets.ButtonText(clearButtonRect, "X"))
        {
            value = 0;
        }

        GUI.color = Color.white;

        editBuffer = value.ToStringCached() ?? value.ToString();
        Widgets.TextFieldNumeric(textFieldRect, ref value, ref editBuffer);
        if (int.TryParse(editBuffer!, out var editBufferAsInt))
        {
            value = editBufferAsInt;
        }

        curX += columnWidth + 2f;
    }

    private static string _editBufferTicks = "";
    private static string _editBufferHours = "";
    private static string _editBufferDays = "";
    private static string _editBufferQuadrums = "";
    private static string _editBufferYears = "";

    internal static void TimeEntry(
        Rect inRect,
        ref int durationTicks,
        int? maxDurationOverride = null,
        int? minDurationOverride = null
    )
    {
        const float inputsWidth = 64f * 5;
        const float overallWidth = inputsWidth + 20f;
        Widgets.DrawMenuSection(inRect.MiddlePartPixels(overallWidth, inRect.height));
        inRect = inRect.ContractedBy(10f, 4f);

        durationTicks.TicksToPeriod(out var years, out var quadrums, out var days, out var hoursFloat);
        var hours = (int)hoursFloat;
        var remainderTicks = durationTicks % GenDate.TicksPerHour;

        var labelHeight = Text.LineHeightOf(GameFont.Small);
        var labelRect = inRect with
        {
            yMin = inRect.yMax - labelHeight * 2 - 4f, height = labelHeight * 2
        };
        var inputRect = inRect.MiddlePartPixels(inputsWidth, 142f);
        inputRect.y -= labelHeight * 2 / 2f;

        var curX = inputRect.xMin;


        // Label column
        var endDateString =
            GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs + durationTicks, QuestUtility.GetLocForDates());
        var durationString = durationTicks.ToStringTicksToPeriodVeryVerbose();
        if (durationTicks <= Settings.MinSnoozeDuration)
        {
            durationString = durationString + " " + "BetterLetters_MinimumDuration".Translate(durationTicks);
            TooltipHandler.TipRegionByKey(labelRect, "BetterLetters_MinimumDuration_Tooltip");
        }
        else if (durationTicks >= (maxDurationOverride ?? Settings.MaxSnoozeDuration))
        {
            durationString = durationString + " " + "BetterLetters_MaximumDuration".Translate();
            TooltipHandler.TipRegionByKey(labelRect, "BetterLetters_MaximumDuration_Tooltip");
        }

        Text.Anchor = TextAnchor.LowerCenter;
        Widgets.Label(labelRect.TopHalf(), "BetterLetters_SnoozeUntil".Translate(endDateString));
        GUI.color = ColorLibrary.Beige;
        Text.Font = GameFont.Tiny;
        Widgets.Label(labelRect.BottomHalf(), "BetterLetters_SnoozeFor".Translate(durationString));
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        // Ticks column
        TimeEntryColumn(inputRect, ref curX, "BetterLetters_Ticks".Translate(), ref remainderTicks,
            ref _editBufferTicks);
        // Hours column
        TimeEntryColumn(inputRect, ref curX, "BetterLetters_Hours".Translate(), ref hours, ref _editBufferHours);
        // Days column
        TimeEntryColumn(inputRect, ref curX, "BetterLetters_Days".Translate(), ref days, ref _editBufferDays);
        // Quadrums column
        TimeEntryColumn(inputRect, ref curX, "BetterLetters_Seasons".Translate(), ref quadrums,
            ref _editBufferQuadrums);
        // Years column
        TimeEntryColumn(inputRect, ref curX, "BetterLetters_Years".Translate(), ref years, ref _editBufferYears);

        durationTicks = Mathf.Clamp(LetterUtils.TicksFromPeriod(remainderTicks, hours, days, quadrums, years),
            minDurationOverride ?? Settings.MinSnoozeDuration, maxDurationOverride ?? Settings.MaxSnoozeDuration);
    }

    [Obsolete] private static string _editBufferSnooze = "null";

    [Obsolete] internal static LetterUtils.TimeUnits SnoozeTimeUnit = LetterUtils.TimeUnits.Hours;

    [Obsolete]
    private static void RefreshEditBuffer(int durationTicks)
    {
        if (_editBufferSnooze == "") return;
        _editBufferSnooze =
            durationTicks == 0 ? "" : ((int)Math.Floor(durationTicks / (float)SnoozeTimeUnit)).ToStringCached()!;
    }

    [Obsolete] private static float? _lastSnoozeSettingsHeight;

    [Obsolete]
    internal static void SnoozeSettings(float x,
        ref float y,
        float width,
        ref int durationTicks,
        float paddingLeft = 32f,
        float paddingRight = 32f,
        float paddingTop = 8f,
        float paddingBottom = 8f,
        int? maxDurationOverride = null,
        int? minDurationOverride = null,
        bool showEndDate = true,
        bool doSectionBackground = true
    )
    {
        const float minWidth = 340f;
        const float maxWidth = 600f;
        // When the chosen unit is Ticks, the buttons change to adjust by this amount.
        // Used the TPS of max 3x speed
        const int ticksMultiplier = 180;
        const float intEntryWidth = 240f;
        const float unitButtonWidth = 90f;
        const float buttonRowHeight = 32f;
        const float spacing = 8f;

        var startY = y;

        float hoursFloat;
        durationTicks.TicksToPeriod(out _, out _, out _, out hoursFloat);
        var hours = (int)hoursFloat;

        RefreshEditBuffer(durationTicks);

        // Cap the widget width and center it within the supplied width
        if (width > maxWidth)
        {
            x += (width - maxWidth) / 2f;
            width = maxWidth;
        }

        width = Mathf.Max(width, minWidth);

        if (doSectionBackground && _lastSnoozeSettingsHeight != null)
            Widgets.DrawMenuSection(new Rect(x, y, width, _lastSnoozeSettingsHeight.Value));

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

#if !(v1_1 || v1_2)
        Widgets.Label(x, ref y, width, "BetterLetters_SnoozeFor".Translate(durationString));
#else
        LegacySupport.Label(x, ref y, width, "BetterLetters_ReminderTextLabel".Translate());
#endif
        Text.Anchor = TextAnchor.UpperLeft;

        // End date label
        if (showEndDate)
        {
            Text.Anchor = TextAnchor.UpperCenter;
            var endDateString =
                GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs + durationTicks, QuestUtility.GetLocForDates());
            Text.Font = GameFont.Tiny;
            GUI.color = ColorLibrary.Beige;
#if !(v1_1 || v1_2)
            Widgets.Label(x, ref y, width, "BetterLetters_SnoozeUntil".Translate(endDateString));
#else
            LegacySupport.Label(x, ref y, width, "BetterLetters_ReminderTextLabel".Translate());
#endif
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
        y += paddingBottom;
        _lastSnoozeSettingsHeight = y - startY;

        // The resulting number of ticks is:
        // (X <unit> + Y <remainder>)
        // ex: (3 hours + 500 ticks) or (7 days + 38,477 ticks)
        // This way the user can adjust a unit without losing the smaller units that they've already set
        durationTicks = Mathf.Clamp(numOfUnit * (int)SnoozeTimeUnit + remainderTicks,
            minDurationOverride ?? Settings.SnoozeTickPeriod,
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

    internal static void SnoozeIconButton(Letter letter, Rect rect)
    {
        SnoozeIconButton(letter, rect, null);
    }

    internal static void SnoozeIconButton(Letter letter, Rect rect, List<FloatMenuOption>? extraFloatMenuOptions)
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
                letter.ShowSnoozeFloatMenu(snooze =>
                {
                    SoundDefOf.Tick_High!.PlayOneShotOnCamera();
                    snoozed = true;
                }, extraFloatMenuOptions: extraFloatMenuOptions);
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
            Settings.CurrentTab = Settings.SettingsTab.Main;
            Find.WindowStack!.Add(new Dialog_ModSettings(BetterLettersMod.Instance!));
        }

        if (Mouse.IsOver(rect))
        {
            TooltipHandler.TipRegionByKey(rect, "BetterLetters_OpenSettingsTooltip");
        }
#endif
    }

    internal static void CheckboxLabeled(
        // ReSharper disable once InconsistentNaming
        this Listing_Standard _this,
        string label,
        ref bool checkOn,
        bool disabled,
        string? tooltip = null,
        float height = 0.0f,
        float labelPct = 1f)
    {
        Rect rect = _this.GetRect(height != 0.0 ? height : Text.CalcHeight(label, _this.ColumnWidth * labelPct),
            labelPct);
        rect.width = Math.Min(rect.width + 24f, _this.ColumnWidth);
        Rect? boundingRectCached = _this.BoundingRectCached;
        if (boundingRectCached.HasValue)
        {
            ref Rect local = ref rect;
            boundingRectCached = _this.BoundingRectCached!;
            Rect other = boundingRectCached.Value;
            if (!local.Overlaps(other))
                goto label_7;
        }

        if (!tooltip!.NullOrEmpty())
        {
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);
            TooltipHandler.TipRegion(rect, (TipSignal)tooltip);
        }

        Widgets.CheckboxLabeled(rect, label, ref checkOn, disabled: disabled);
        label_7:
        _this.Gap(_this.verticalSpacing);
    }

    internal static void SectionHeader(this Listing_Standard snoozeSection, string sectionKey)
    {
        Text.Anchor = TextAnchor.UpperCenter;
        snoozeSection.Label(sectionKey.Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        snoozeSection.GapLine(8f);
    }

    internal static void DrawSeparatorLine(float x, ref float curY, float width)
    {
        GUI.color = Widgets.SeparatorLineColor;
        Widgets.DrawLineHorizontal(x, curY, width);
        GUI.color = Color.white;
        curY += 3f;
    }
}
