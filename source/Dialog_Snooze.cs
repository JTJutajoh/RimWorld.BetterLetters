using System;
using RimWorld;
using UnityEngine;

namespace BetterLetters;

public class Dialog_Snooze : Window
{
    public override Vector2 InitialSize => new Vector2(600, 170);

    private static Dialog_Snooze? _instance;

    private Action<int> _onConfirmed;
    private float _durationDays = 0.5f;

    public int DurationTicks => Mathf.RoundToInt(_durationDays * GenDate.TicksPerDay);

    // Maximum duration in days set in settings
    private static float MaxDuration => Settings.MaxSnoozeDuration;

    public Dialog_Snooze(Action<int> onConfirmedAction)
    {
        _instance?.Close();
        _instance = this;
        _onConfirmed = onConfirmedAction;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
    }

    public static string SliderMaxLabel => "BetterLetters_SliderRightLabel".Translate(
        Mathf.RoundToInt(MaxDuration * GenDate.TicksPerDay).ToStringTicksToPeriod());

    public static string SliderDurationLabel(float durationDays) =>
        "BetterLetters_SliderLabel".Translate(Mathf.RoundToInt(durationDays * GenDate.TicksPerDay).ToStringTicksToPeriod());

    public static void DoSnoozeOptions(Rect inRect, Rect labelsRect, ref float durationDays)
    {
        //TODO: Fix snooze options layout
        const float sliderWidthRatio = 0.6f;
        var sliderRect = inRect.MiddlePart(sliderWidthRatio, 0.8f);
        sliderRect = sliderRect.TopPartPixels(20f);
// For some reason RW 1.4 made this obsolete, but not anymore in 1.5+. Just ignore the warning.
#pragma warning disable CS0612 // Type or member is obsolete
        durationDays = Widgets.HorizontalSlider(
            sliderRect,
            durationDays,
            0,
            MaxDuration,
            label: SliderDurationLabel(durationDays),
            leftAlignedLabel: "BetterLetters_Immediately".Translate(),
            rightAlignedLabel: SliderMaxLabel,
            roundTo: 1f / 24f // Round to the hour
        );
#pragma warning restore CS0612 // Type or member is obsolete

        var maxLabelRect = inRect.MiddlePart(sliderWidthRatio, 1.0f).RightPartPixels(72f).TopPartPixels(40f);
        maxLabelRect.y += 16f;
        TooltipHandler.TipRegionByKey(maxLabelRect, "BetterLetters_MaxDurationTooltip", Settings.MaxSnoozeDuration);


        //TODO: Redo inc/dec buttons
        const float buttonWidthRatio = (1f - sliderWidthRatio) / 2 - 0.03f;
        const float buttonHeightIncDec = 30f;

        //TODO: Add a text entry field to directly set the duration
        // Widgets.TextFieldNumeric();

        if (Widgets.ButtonTextSubtle(
                inRect.MiddlePartPixels(inRect.width, buttonHeightIncDec).LeftPart(buttonWidthRatio).LeftHalf(),
                "BetterLetters_DecDay".Translate()
            ))
        {
            durationDays = Mathf.Max(durationDays - 1, 0);
        }

        if (Widgets.ButtonTextSubtle(
                inRect.MiddlePartPixels(inRect.width, buttonHeightIncDec).LeftPart(buttonWidthRatio).RightHalf(),
                "BetterLetters_DecHour".Translate()
            ))
        {
            durationDays = Mathf.Max(durationDays - (1f / 24f), 0);
        }

        if (Widgets.ButtonTextSubtle(
                inRect.MiddlePartPixels(inRect.width, buttonHeightIncDec).RightPart(buttonWidthRatio).LeftHalf(),
                "BetterLetters_IncHour".Translate()
            ))
        {
            durationDays = Mathf.Min(durationDays + (1f / 24f), MaxDuration);
        }

        if (Widgets.ButtonTextSubtle(
                inRect.MiddlePartPixels(inRect.width, buttonHeightIncDec).RightPart(buttonWidthRatio).RightHalf(),
                "BetterLetters_IncDay".Translate()
            ))
        {
            durationDays = Mathf.Min(durationDays + 1, MaxDuration);
        }

        var durationTicks = Mathf.RoundToInt(durationDays * GenDate.TicksPerDay);
        var endDate =
            GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs + durationTicks, QuestUtility.GetLocForDates());
        Widgets.Label(labelsRect.LeftHalf(), "BetterLetters_SnoozeUntil".Translate(endDate));
        var durRect = labelsRect.RightHalf();
        var durText = "BetterLetters_SnoozeFor".Translate(durationTicks.ToStringTicksToPeriodVerbose());
        durRect.xMin += durRect.width - Text.CalcSize(durText).x;
        Widgets.Label(durRect, durText);
    }

    public override void DoWindowContents(Rect inRect)
    {
        var buttonSize = new Vector2(120f, 40f);
        var mainRect = inRect.TopPartPixels(inRect.yMax - buttonSize.y - 4);

        var upperRect = mainRect.TopPart(0.7f);
        var labelsRect = mainRect.BottomPart(0.3f);
        DoSnoozeOptions(upperRect, labelsRect, ref _durationDays);


        var buttonsRect = inRect.BottomPartPixels(buttonSize.y + 10);

        if (Widgets.ButtonText(
                new Rect(0, buttonsRect.yMax - buttonSize.y, buttonSize.x, buttonSize.y),
                "BetterLetters_Cancel".Translate())
           )
        {
            Close();
        }
        else if (Widgets.ButtonText(
                     new Rect(buttonsRect.xMax - buttonSize.x, buttonsRect.yMax - buttonSize.y,
                         buttonSize.x, buttonSize.y),
                     "BetterLetters_Snooze".Translate())
                )
        {
            _onConfirmed(DurationTicks);
            Close();
        }
    }
}
