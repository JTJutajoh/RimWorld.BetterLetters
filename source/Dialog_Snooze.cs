using System;
using RimWorld;
using UnityEngine;

namespace BetterLetters;

public class Dialog_Snooze : Window
{
    public override Vector2 InitialSize => new Vector2(400, 170);

    private static Dialog_Snooze? _instance;

    private readonly Action<int> _onConfirmed;
    private int _durationTicks = GenDate.TicksPerDay;

    internal int DurationTicks => _durationTicks;

    // Maximum duration in days set in settings

    public Dialog_Snooze(Action<int> onConfirmedAction)
    {
        _instance?.Close();
        _instance = this;
        _onConfirmed = onConfirmedAction;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
        doCloseButton = false;

        CustomWidgets.SnoozeTimeUnit = LetterUtils.TimeUnits.Hours;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var buttonsSize = new Vector2(120f, 32f);
        var mainRect = inRect.TopPartPixels(inRect.yMax - buttonsSize.y - 4);

        var upperRect = mainRect.TopPart(0.7f);
        var labelsRect = mainRect.BottomPart(0.3f);

        var curY = upperRect.yMin;
        CustomWidgets.SnoozeSettings(upperRect.x, ref curY, upperRect.width, ref _durationTicks);


        var buttonsRect = inRect.BottomPartPixels(buttonsSize.y + 10);

        if (Widgets.ButtonText(buttonsRect.RightPartPixels(buttonsSize.x), "BetterLetters_Snooze".Translate()))
        {
            _onConfirmed(DurationTicks);
            Close();
        }

        if (Widgets.ButtonText(buttonsRect.LeftPartPixels(buttonsSize.x), "Cancel".Translate()))
        {
            Close();
        }
    }
}
