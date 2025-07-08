using System;
using RimWorld;
using UnityEngine;

namespace BetterLetters;

public class Dialog_Snooze : Window
{
    public override Vector2 InitialSize => new Vector2(440, 340);

    private static Dialog_Snooze? _instance;

    private readonly Action<int, bool, bool> _onConfirmed;
    private int _durationTicks = GenDate.TicksPerDay;

    internal int DurationTicks => _durationTicks;

    // Maximum duration in days set in settings
    private readonly int? _maxDurationOverride;
    private bool _openWhenFinished;
    private bool _pinWhenFinished;

    public Dialog_Snooze(Action<int, bool, bool> onConfirmedAction, int? maxDurationOverride = null)
    {
        _instance?.Close();
        _instance = this;
        _openWhenFinished = Settings.SnoozeOpen;
        _pinWhenFinished = Settings.SnoozePinned;
        _onConfirmed = onConfirmedAction;
        _maxDurationOverride = maxDurationOverride;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
        doCloseButton = false;
        doCloseX = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var buttonsSize = new Vector2(120f, 32f);
        var mainRect = inRect.TopPartPixels(inRect.yMax - buttonsSize.y - 4);
        mainRect.yMin += 8f;

        var upperRect = mainRect.TopPart(0.85f);
        var labelsRect = mainRect.BottomPart(0.15f);

        var curY = upperRect.yMin;
        CustomWidgets.TimeEntry(upperRect, ref _durationTicks, _maxDurationOverride);
        curY += upperRect.height + 4f;

        var checkboxLabelString = "BetterLetters_OpenWhenFinished".Translate();
        var checkboxLabelSize = Text.CalcSize(checkboxLabelString);
        checkboxLabelSize.x += 32f;
        Widgets.CheckboxLabeled(
            new Rect(labelsRect.xMin + (labelsRect.width - checkboxLabelSize.x) / 2f, curY, checkboxLabelSize.x, 32f),
            "BetterLetters_OpenWhenFinished".Translate(),
            ref _openWhenFinished, placeCheckboxNearText: true);
        // Pin button
        var pinRect = new Rect(inRect.xMax - 32f, inRect.yMax - 32f, 32f, 32f);
        Widgets.Checkbox(pinRect.x, pinRect.y, ref _pinWhenFinished, pinRect.width, texChecked: Icons.PinIcon, texUnchecked: Icons.PinOutline);
        TooltipHandler.TipRegionByKey(pinRect, "BetterLetters_PinReminder");

        var buttonsRect = inRect.BottomPartPixels(buttonsSize.y + 10);

        if (Widgets.ButtonText(buttonsRect.MiddlePartPixels(buttonsSize.x, buttonsSize.y),
                "BetterLetters_Snooze".Translate()))
        {
            _onConfirmed(DurationTicks, _pinWhenFinished, _openWhenFinished);
            Close();
        }
    }
}
