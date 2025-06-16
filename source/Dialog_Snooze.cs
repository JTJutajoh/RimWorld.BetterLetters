using System;
using DarkLog;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterLetters;

public class Dialog_Snooze : Window
{
    public override Vector2 InitialSize => new Vector2(600, 170);

    private static Dialog_Snooze? _instance;

    private Action<int> _onConfirmed;
    private float _durationDays = 0.5f;
    public int DurationTicks => Mathf.RoundToInt(_durationDays * (float)GenDate.TicksPerDay);
    private static float MaxDuration => Settings.MaxSnoozeDuration;

    public Dialog_Snooze(Action<int> onConfirmedAction)
    {
        _instance?.Close();
        _instance = this;
        _onConfirmed = onConfirmedAction;
        this.closeOnClickedOutside = true;
        this.absorbInputAroundWindow = true;
    }

    private string EndDate =>
        GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs + DurationTicks, QuestUtility.GetLocForDates());

    private string DurationString => DurationTicks.ToStringTicksToPeriodVerbose();

    public static void DoSnoozeOptions(Rect inRect, Rect labelsRect, ref float durationDays)
    {
        const float sliderWidthRatio = 0.6f;
        Widgets.HorizontalSlider(
            inRect.MiddlePart(sliderWidthRatio, 1.0f),
            ref durationDays,
            new FloatRange(0, MaxDuration),
            "BetterLetters_SliderLabel".Translate(durationDays.ToString("F1")),
            1f / 24f // Round to the hour
        );

        const float buttonWidthRatio = (1f - sliderWidthRatio) / 2 - 0.03f;
        const float buttonHeightIncDec = 30f;
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
        
        var durationTicks = Mathf.RoundToInt(durationDays * (float)GenDate.TicksPerDay);
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
        var mainRect = inRect.TopPartPixels(inRect.yMax - Window.CloseButSize.y - 4);

        var upperRect = mainRect.TopPart(0.7f);
        var labelsRect = mainRect.BottomPart(0.3f);
        DoSnoozeOptions(upperRect, labelsRect, ref _durationDays);


        var buttonsRect = inRect.BottomPartPixels(Window.CloseButSize.y + 10);

        if (Widgets.ButtonText(
                new Rect(0, buttonsRect.yMax - Window.CloseButSize.y, Window.CloseButSize.x, Window.CloseButSize.y),
                "BetterLetters_Cancel".Translate())
           )
        {
            this.Close(true);
        }
        else if (Widgets.ButtonText(
                     new Rect(buttonsRect.xMax - Window.CloseButSize.x, buttonsRect.yMax - Window.CloseButSize.y,
                         Window.CloseButSize.x, Window.CloseButSize.y),
                     "BetterLetters_Snooze".Translate())
                )
        {
            _onConfirmed(DurationTicks);
            this.Close(true);
        }
    }
}