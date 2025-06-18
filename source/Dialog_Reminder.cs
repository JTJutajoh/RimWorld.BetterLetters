using System.Collections.Generic;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse.Sound;

namespace BetterLetters;

public class Dialog_Reminder : Window
{
    public override Vector2 InitialSize => new(680f, Mathf.Min(440f, UI.screenHeight));

    public static Dialog_Reminder? Instance;

    private Vector2 _scrollPosition = Vector2.zero;

    private string _reminderTitle = "BetterLetters_Reminder".Translate();

    private string _reminderText = "";

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
    public int DurationTicks => Mathf.RoundToInt(_durationDays * GenDate.TicksPerDay);

    private Thing? _selectedThing;

    // Constructor
    public Dialog_Reminder(
        Thing? thing = null,
        string? title = null,
        string? text = null,
        bool pinned = true,
        LetterDef? letterDef = null,
        float durationDays = -1
    )
    {
        _selectedThing = thing;

        Instance?.Close();
        Instance = this;

        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnAccept = true;
        closeOnClickedOutside = true;
        closeOnClickedOutside = true;
        soundAppear = SoundDefOf.CommsWindow_Open;
        soundClose = SoundDefOf.CommsWindow_Close;
        _selectedThing = thing ?? FindSelectedThing();
        _reminderTitle = title ?? _reminderTitle;
        _reminderText = text ?? _reminderText;
        _pinned = pinned;
        _letterDef = letterDef ?? _letterDef;
        _durationDays = durationDays >= 0 ? durationDays : _durationDays;
    }

    private Thing? FindSelectedThing()
    {
        //TODO: Add setting to disable auto-selecting selected things for reminders
        if (Find.Selector.NumSelected == 0) return null;
        if (Find.Selector.SelectedPawns.Count > 0)
        {
            return Find.Selector.SelectedPawns[0];
        }

        foreach (var o in Find.Selector.SelectedObjectsListForReading)
        {
            if (o is Thing thing)
            {
                return thing;
            }
        }

        return null;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var innerRect = inRect.AtZero();
        var buttonsSize = new Vector2(100f, 32f);
        var snoozeSectionHeight = 160f;
        var pinSectionWidth = 40f;

        // Draw the content
#if !(v1_1 || v1_2)
        Widgets.BeginGroup(innerRect);
#endif

        // Title entry
        var titleTextRect = innerRect.TopPartPixels(32f);
        Widgets.Label(new Rect(titleTextRect.xMin, titleTextRect.yMin + 4f, Text.CalcSize("Title".Translate()).x, 32f),
            "Title".Translate());
        var titleTextEntryRect = titleTextRect.RightPart(0.9f);
        titleTextEntryRect.xMax -= pinSectionWidth;
        _reminderTitle = Widgets.TextField(titleTextEntryRect, _reminderTitle, 64, new Regex("^[^<>]*$"));
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
        var outerScrollRegionRect = new Rect(0f, titleTextRect.yMax + 30f, innerRect.width, 100f);
        var textEntryHeight = Mathf.Max(Text.CalcHeight(_reminderText, outerScrollRegionRect.width - 8f), 90f);
        var textEntryRect = new Rect(0f, 0f, outerScrollRegionRect.width - 14f, textEntryHeight);
        Widgets.BeginScrollView(outerScrollRegionRect, ref _scrollPosition, textEntryRect);
        _reminderText = Widgets.TextArea(textEntryRect, _reminderText);
        _reminderText = SanitizeText(_reminderText);
        Widgets.EndScrollView();

        // Selected thing button
        DoJumpToSelector(new Rect(inRect.xMin, outerScrollRegionRect.yMax + 4f, inRect.width, 64f));

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
                floatMenuOptions.Add(new FloatMenuOption(letterDef.defName, () => _letterDef = letterDef
#if v1_6
                    , iconTex: letterDef.Icon, iconColor: letterDef.color));
#elif v1_1 || v1_2 || v1_3 || v1_4 || v1_5
                )); // FloatMenuOption constructor only added an overload with iconTex in 1.6+, so just close the constructor here
#endif
            }


            Find.WindowStack.Add(new FloatMenu(floatMenuOptions));
            SoundDefOf.FloatMenu_Open.PlayOneShotOnCamera();
            Event.current.Use();
        }

        // Bottom buttons
        GUI.color = Color.white; // RW version < 1.4 Widgets.ButtonImage has a bug that fails to reset the GUI color
        if (Widgets.ButtonText(buttonsRect.LeftPartPixels(buttonsSize.x), "Cancel".Translate()))
        {
            Close();
        }
        else if (Widgets.ButtonText(buttonsRect.RightPartPixels(buttonsSize.x), "Confirm".Translate()))
        {
            LookTargets? lookTargets = null;
            if (_selectedThing is not null)
            {
                lookTargets = new LookTargets(_selectedThing);
            }

            LetterUtils.AddReminder(SanitizeText(_reminderTitle), SanitizeText(_reminderText), _letterDef,
                DurationTicks, _pinned, lookTargets);
            Close();
        }

#if !(v1_1 || v1_2)
        Widgets.EndGroup();
#endif
    }

    private void DoJumpToSelector(Rect inRect)
    {
        //TODO: Improve appearance/placement of the thing selector
        var thingRect = new Rect(inRect.xMin, inRect.yMin + 4f, inRect.width, 32f);
        var label = "BetterLetters_JumpToColon".Translate();
        var labelRect = thingRect.LeftPartPixels(Text.CalcSize(label).x);
        labelRect.y += 2f;
        Widgets.Label(labelRect, label);
        var thingLabel = _selectedThing?.LabelShortCap ?? "BetterLetters_NothingSelected".Translate();
        var thingLabelSize = Text.CalcSize(thingLabel);
        thingLabelSize.x += 32f + 8f; // Add some room for the icon
        var thingButtonRect = new Rect(labelRect.xMax + 8f, labelRect.yMin - 4f,
            Mathf.Max(thingLabelSize.x, 100f), 32f);
        var thingButtonClicked = Widgets.ButtonTextSubtle(thingButtonRect, thingLabel, textLeftMargin: 32f);
        var thingIconRect = new Rect(thingButtonRect.xMin + 2f, thingButtonRect.yMin + 2f, 32f, 32f);
#if v1_6
        if (_selectedThing is not null)
        {
            Widgets.ThingIcon(thingIconRect, _selectedThing, scale: 0.6f);
        }
#else
        if (_selectedThing is not null)
        {
            Widgets.ThingIcon(thingIconRect, _selectedThing);
        }
#endif
        if (thingButtonClicked)
        {
            var floatMenuOptions = new List<FloatMenuOption>
            {
                new FloatMenuOption("BetterLetters_NothingSelected".Translate(), () => { _selectedThing = null; },
                    MenuOptionPriority.AttackEnemy),
                new FloatMenuOption("BetterLetters_SelectSomething".Translate(), DoSelectThing
#if v1_6
                    ,
                    iconTex: TexButton.Plus,
                    iconColor: Color.white, priority: MenuOptionPriority.AttackEnemy
#endif
                )
            };

            var addedSelectedThing = false;
            foreach (var o in Find.Selector.SelectedObjectsListForReading)
            {
                var priority = MenuOptionPriority.Default;
                if (o is not Thing thing) continue;
                if (thing is Pawn) priority = MenuOptionPriority.High;
                floatMenuOptions.Add(
                    new FloatMenuOption(thing.LabelCap, () => _selectedThing = thing,
#if v1_4 || v1_5 || v1_6
                        thing, Color.white,
#endif
                        priority
                    )
                );
                if (thing == _selectedThing) addedSelectedThing = true;
            }

            if (_selectedThing != null && !addedSelectedThing)
            {
                floatMenuOptions.Add(new FloatMenuOption(_selectedThing.LabelCap, null,
#if v1_4 || v1_5 || v1_6
                    _selectedThing, Color.white,
#endif
                    priority: MenuOptionPriority.InitiateSocial));
            }

            Find.WindowStack.Add(new FloatMenu(floatMenuOptions));
            SoundDefOf.FloatMenu_Open.PlayOneShotOnCamera();
            Event.current.Use();
        }
    }

    private static void DoSelectThing()
    {
        if (Instance is null)
        {
            Log.Warning("Can't select thing, dialog instance was null");
            return;
        }

        var selectedThing = Instance._selectedThing;
        var title = Instance._reminderTitle;
        var text = Instance._reminderText;
        var pinned = Instance._pinned;
        var letterDef = Instance._letterDef;
        var durationDays = Instance._durationDays;
        Instance.Close();
        Find.Targeter.BeginTargeting(
#if v1_6
            TargetingParameters.ForThing(),
#elif v1_1 || v1_2 || v1_3 || v1_4 || v1_5
            LegacySupport.ForThing(),
#endif
            targetInfo =>
            {
                if (targetInfo.HasThing)
                {
                    Find.WindowStack.Add(new Dialog_Reminder(
                        targetInfo.Thing ?? selectedThing,
                        title,
                        text,
                        pinned,
                        letterDef,
                        durationDays
                    ));
                }
            }
        );
    }

    private static string SanitizeText(string text)
    {
        // Remove all instances of < and > from the string to avoid XML serialization issues
        text = text.Replace("<", "");
        text = text.Replace(">", "");
        return text;
    }
}