using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterLetters;

public class Dialog_Reminder : Window
{
    public override Vector2 InitialSize => new Vector2(680f, (float)Mathf.Min(400f, UI.screenHeight));

    private Vector2 _scrollPosition = Vector2.zero;

    private string _reminderTitle = "BetterLetters_Reminder".Translate();

    private string _reminderText = "";
    private bool HasReminderText => _reminderText.Length > 0;

    private bool _pinned = true;

    private LetterDef _letterDef = LetterDefOf.PositiveEvent;

    private static readonly List<LetterDef> ValidLetterDefs = new()
    {
        LetterDefOf.PositiveEvent,
        LetterDefOf.NeutralEvent,
        LetterDefOf.NegativeEvent,
        LetterDefOf.ThreatSmall,
        LetterDefOf.ThreatBig,
    };

    private float _durationDays = 1f;
    public int DurationTicks => Mathf.RoundToInt(_durationDays * (float)GenDate.TicksPerDay);

    // Constructor
    public Dialog_Reminder() : base(null)
    {
        this.forcePause = true;
        this.absorbInputAroundWindow = true;
        this.closeOnAccept = true;
        this.closeOnClickedOutside = true;
        this.closeOnClickedOutside = true;
        this.soundAppear = SoundDefOf.CommsWindow_Open;
        this.soundClose = SoundDefOf.CommsWindow_Close;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var innerRect = inRect.AtZero();
        var buttonsSize = new Vector2(100f, 32f);
        var snoozeSectionHeight = 160f;
        var pinSectionWidth = 40f;

        // Draw the content
        Widgets.BeginGroup(innerRect);

        // Title entry
        var titleTextRect = innerRect.TopPartPixels(32f);
        Widgets.Label(titleTextRect.LeftPart(0.12f), "Title".Translate());
        var titleTextEntryRect = titleTextRect.RightPart(0.9f);
        titleTextEntryRect.xMax -= pinSectionWidth;
        _reminderTitle = Widgets.TextField(titleTextEntryRect, _reminderTitle, 64);
        _reminderTitle = SanitizeText(_reminderTitle);

        Widgets.Label(new Rect(0f, titleTextRect.yMax + 8f, innerRect.width, 32f),
            "BetterLetters_ReminderTextLabel".Translate());

        // Pin button
        var pinRect = titleTextRect.RightPartPixels(pinSectionWidth);
        var pinButtonSize = 32f;
        var pinIconRect = new Rect(pinRect.xMax - pinButtonSize, pinRect.yMin, pinButtonSize, pinButtonSize);
        Widgets.Checkbox(pinIconRect.xMin, pinIconRect.yMin, ref _pinned, pinButtonSize,
            texChecked: LetterUtils.Icons.PinIcon, texUnchecked: LetterUtils.Icons.PinOutline);
        TooltipHandler.TipRegionByKey(pinIconRect, "BetterLetters_PinReminder");


        // Text entry
        var outerScrollRegionRect = new Rect(0f, titleTextRect.yMax + 30f, innerRect.width,
            innerRect.height - snoozeSectionHeight - 20f - (titleTextRect.yMax + 30f));
        var textEntryHeight = Mathf.Max(Text.CalcHeight(_reminderText, outerScrollRegionRect.width - 8f), 120f);
        var textEntryRect = new Rect(0f, 0f, outerScrollRegionRect.width - 10f, textEntryHeight);
        Widgets.BeginScrollView(outerScrollRegionRect, ref this._scrollPosition, textEntryRect, true);
        _reminderText = Widgets.TextArea(textEntryRect, _reminderText, false);
        _reminderText = SanitizeText(_reminderText);
        Widgets.EndScrollView();

        // Snooze settings
        var snoozeRect = innerRect.BottomPartPixels(snoozeSectionHeight);
        snoozeRect.yMax -= buttonsSize.y + 20f;
        snoozeRect.yMin -= buttonsSize.y;
        var snoozeControlsRect = snoozeRect.TopPart(0.7f);
        var labelsRect = snoozeRect.BottomPart(0.2f);
        Dialog_Snooze.DoSnoozeOptions(snoozeControlsRect, labelsRect, ref _durationDays);

        // Bottom Row
        var buttonsRect = innerRect.BottomPartPixels(buttonsSize.y);

        // Letter type selection
        var letterIconSize = new Vector2(_letterDef.Icon.width, _letterDef.Icon.height) * 0.5f;
        var letterTypeRect = buttonsRect.MiddlePartPixels(buttonsSize.x * 1.3f, letterIconSize.y);
        var letterIconRect = new Rect(letterTypeRect.xMax - letterIconSize.x,
            letterTypeRect.center.y - (letterIconSize.y / 2) - 4f, letterIconSize.x, letterIconSize.y);
        Widgets.Label(letterTypeRect.LeftPartPixels(letterTypeRect.width - letterIconSize.x),
            "BetterLetters_LetterTypeLabel".Translate());
        if (Widgets.ButtonImage(letterIconRect, _letterDef.Icon, _letterDef.color))
        {
            var floatMenuOptions = new List<FloatMenuOption>();

            foreach (var letterDef in ValidLetterDefs)
            {
                floatMenuOptions.Add(new FloatMenuOption(letterDef.defName, () => _letterDef = letterDef,
                    iconTex: letterDef.Icon, iconColor: letterDef.color));
                ;
            }

            Find.WindowStack.Add(new FloatMenu(floatMenuOptions));
            SoundDefOf.FloatMenu_Open.PlayOneShotOnCamera();
            Event.current.Use();
        }

        // Bottom buttons
        if (Widgets.ButtonText(buttonsRect.LeftPartPixels(buttonsSize.x), "Cancel".Translate()))
        {
            this.Close(true);
        }
        else if (Widgets.ButtonText(buttonsRect.RightPartPixels(buttonsSize.x), "Confirm".Translate()))
        {
            LetterUtils.AddReminder(SanitizeText(_reminderTitle), SanitizeText(_reminderText), _letterDef, DurationTicks, _pinned);
            this.Close(true);
        }

        Widgets.EndGroup();
    }

    private static string SanitizeText(string text)
    {
        // Remove all instances of < and > from the string to avoid XML serialization issues
        text = text.Replace("<", "");
        text = text.Replace(">", "");
        return text;
    }
}