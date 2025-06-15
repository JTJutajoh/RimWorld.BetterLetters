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

    private string EndDate => GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs + DurationTicks, QuestUtility.GetLocForDates());
    private string DurationString => DurationTicks.ToStringTicksToPeriodVerbose();
    
    public override void DoWindowContents(Rect inRect)
    {
        var mainRect = inRect.TopPartPixels(inRect.yMax - Window.CloseButSize.y - 4);

        const float sliderWidthRatio = 0.6f;
        var upperRect = mainRect.TopPart(0.7f);
        Widgets.HorizontalSlider(
            upperRect.MiddlePart(sliderWidthRatio, 1.0f),
            ref _durationDays,
            new FloatRange(0, MaxDuration),
            "BetterLetters_SliderLabel".Translate(_durationDays.ToString("F1")),
            1f / 24f // Round to the hour
        );

        const float buttonWidthRatio = (1f - sliderWidthRatio) / 2 - 0.03f;
        const float buttonHeightIncDec = 30f;
        if (Widgets.ButtonTextSubtle(
                upperRect.MiddlePartPixels(upperRect.width, buttonHeightIncDec).LeftPart(buttonWidthRatio).LeftHalf(),
                "BetterLetters_DecDay".Translate()
            ))
        {
            _durationDays = Mathf.Max(_durationDays - 1, 0);
        }
        if (Widgets.ButtonTextSubtle(
                upperRect.MiddlePartPixels(upperRect.width, buttonHeightIncDec).LeftPart(buttonWidthRatio).RightHalf(),
                "BetterLetters_DecHour".Translate()
            ))
        {
            _durationDays = Mathf.Max(_durationDays - (1f / 24f), 0);
        }
        if (Widgets.ButtonTextSubtle(
                upperRect.MiddlePartPixels(upperRect.width, buttonHeightIncDec).RightPart(buttonWidthRatio).LeftHalf(),
                "BetterLetters_IncHour".Translate()
            ))
        {
            _durationDays = Mathf.Min( _durationDays + (1f / 24f), MaxDuration);
        }
        if (Widgets.ButtonTextSubtle(
                upperRect.MiddlePartPixels(upperRect.width, buttonHeightIncDec).RightPart(buttonWidthRatio).RightHalf(),
                "BetterLetters_IncDay".Translate()
            ))
        {
            _durationDays = Mathf.Min( _durationDays +  1, MaxDuration);
        }
        
        var labelsRect = mainRect.BottomPart(0.3f);
        Widgets.Label(labelsRect.LeftHalf(), "BetterLetters_SnoozeUntil".Translate(EndDate));
        var durRect = labelsRect.RightHalf();
        var durText = "BetterLetters_SnoozeFor".Translate(DurationString);
        durRect.xMin += durRect.width - Text.CalcSize(durText).x;
        Widgets.Label(durRect, durText);

        var buttonsRect = inRect.BottomPartPixels(Window.CloseButSize.y + 10);
        
        if (Widgets.ButtonText(
            new Rect(0, buttonsRect.yMax - Window.CloseButSize.y, Window.CloseButSize.x, Window.CloseButSize.y),
            "BetterLetters_Cancel".Translate())
        )
        {
            this.Close(true);
        }
        else if (Widgets.ButtonText(
            new Rect(buttonsRect.xMax - Window.CloseButSize.x, buttonsRect.yMax - Window.CloseButSize.y, Window.CloseButSize.x, Window.CloseButSize.y),
            "BetterLetters_Snooze".Translate())
        )
        {
            _onConfirmed(DurationTicks);
            this.Close(true);
        }
    }
}