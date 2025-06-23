using System;
using RimWorld;
using UnityEngine;

namespace BetterLetters;

public class Dialog_Snooze : Window
{
    public override Vector2 InitialSize => new Vector2(440, 170);

    private static Dialog_Snooze? _instance;

    private readonly Action<int> _onConfirmed;
    private int _durationTicks = GenDate.TicksPerDay;

    internal int DurationTicks => _durationTicks;

    // Maximum duration in days set in settings
    private int? _maxDurationOverride;

    public Dialog_Snooze(Action<int> onConfirmedAction, int? maxDurationOverride = null)
    {
        _instance?.Close();
        _instance = this;
        _onConfirmed = onConfirmedAction;
        _maxDurationOverride = maxDurationOverride;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
        doCloseButton = false;
        doCloseX = true;

        CustomWidgets.SnoozeTimeUnit = LetterUtils.TimeUnits.Hours;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var buttonsSize = new Vector2(120f, 32f);
        var mainRect = inRect.TopPartPixels(inRect.yMax - buttonsSize.y - 4);

        var upperRect = mainRect.TopPart(0.7f);
        var labelsRect = mainRect.BottomPart(0.3f);

        var curY = upperRect.yMin;
        CustomWidgets.SnoozeSettings(upperRect.x, ref curY, upperRect.width, ref _durationTicks, maxDurationOverride: _maxDurationOverride);


        var buttonsRect = inRect.BottomPartPixels(buttonsSize.y + 10);

        if (Widgets.ButtonText(buttonsRect.MiddlePartPixels(buttonsSize.x, buttonsSize.y), "BetterLetters_Snooze".Translate()))
        {
            _onConfirmed(DurationTicks);
            Close();
        }
    }
}
