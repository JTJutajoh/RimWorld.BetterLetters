using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse.Sound;

namespace BetterLetters;

public class Dialog_Reminder : Window
{
    private const int MaxTitleLength = 32;
    public override Vector2 InitialSize => new(500f, Mathf.Min(400f, UI.screenHeight));

    private Vector2 LetterIconSize => new Vector2(_letterDef.Icon?.width ?? 64f, _letterDef.Icon?.height ?? 64f) * 0.5f;

    public static Dialog_Reminder? Instance;

    private Vector2 _scrollPosition = Vector2.zero;

    private string _reminderTitle = "BetterLetters_Reminder".Translate();

    private string _reminderText = "";

    private bool _pinned;

    private LetterDef _letterDef = LetterUtils.ReminderLetterDef;

    private static readonly List<LetterDef> ValidLetterDefs = new()
    {
        LetterUtils.ReminderLetterDef,
        LetterDefOf.PositiveEvent,
        LetterDefOf.NeutralEvent,
        LetterDefOf.NegativeEvent,
        LetterDefOf.ThreatSmall,
        LetterDefOf.ThreatBig,
    };

    private int _durationTicks = GenDate.TicksPerDay;

    internal float DurationDays => _durationTicks.TicksToDays();

    private Thing? _selectedThing;

    /// <summary>
    /// Attempts to find and return the currently selected Thing in the game, if any.
    /// </summary>
    /// <returns>
    /// The first selected Thing if available, or null if no Thing is selected
    /// or if the AutoSelectThingForReminders setting is disabled.
    /// </returns>
    private static Thing? FindSelectedThing()
    {
        if (!Settings.AutoSelectThingForReminders)
        {
            return null;
        }

        if (Find.Selector?.NumSelected == 0) return null;
        if (Find.Selector?.SelectedPawns?.Count > 0)
        {
            return Find.Selector.SelectedPawns[0];
        }

        foreach (var o in Find.Selector?.SelectedObjectsListForReading ?? new List<object>())
        {
            if (o is Thing thing)
            {
                return thing;
            }
        }

        return null;
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
        var durationTicks = Instance._durationTicks;
        Instance.Close();
        Find.Targeter?.BeginTargeting(
#if v1_6
            TargetingParameters.ForThing()!,
#elif v1_1 || v1_2 || v1_3 || v1_4 || v1_5
            LegacySupport.ForThing(),
#endif
            targetInfo =>
            {
                if (targetInfo.HasThing)
                {
                    Find.WindowStack?.Add(new Dialog_Reminder(
                        targetInfo.Thing ?? selectedThing,
                        title,
                        text,
                        pinned,
                        letterDef,
                        durationTicks
                    ));
                }
            }
        );
    }

    /// <summary>
    /// Makes user-input text safe to be serialized into their save file, since otherwise &lt; and &gt; get interpreted as XML tags
    /// which causes a ton of errors when the save is loaded, effectively destroying the save file itself.
    /// </summary>
    private static string SanitizeText(string text)
    {
        text = System.Security.SecurityElement.Escape(text)!;
        return text;
    }

    public Dialog_Reminder(
        Thing? thing = null,
        string? title = null,
        string? text = null,
        bool pinned = true,
        LetterDef? letterDef = null,
        int durationTicks = -1
    )
    {
        _selectedThing = thing;

        Instance?.Close();
        Instance = this;

        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnAccept = false;
        closeOnClickedOutside = true;
        doCloseButton = false;
        doCloseX = true;
        soundAppear = SoundDefOf.CommsWindow_Open!;
        soundClose = SoundDefOf.CommsWindow_Close!;
        _selectedThing = thing ?? FindSelectedThing();
        _reminderTitle = title ?? _reminderTitle;
        _reminderText = text ?? _reminderText;
        _pinned = pinned;
        _letterDef = letterDef ?? _letterDef;
        _durationTicks = durationTicks >= 0 ? durationTicks : _durationTicks;

        CustomWidgets.SnoozeTimeUnit = LetterUtils.TimeUnits.Days;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var innerRect = inRect.AtZero();
        var curY = innerRect.yMin;

        // Draw the content
#if !(v1_1 || v1_2)
        Widgets.BeginGroup(innerRect);
#endif
        DoTitleRow(innerRect, ref curY);
        DoBodyTextEntry(innerRect, ref curY);
        curY += 3f;
        DoReminderSettings(innerRect, ref curY);
        curY += 4f;

        DoCloseButtons(innerRect);
#if !(v1_1 || v1_2)
        Widgets.EndGroup();
#endif
    }

    private void DoTitleRow(Rect innerRect, ref float curY)
    {
        var titleRowRect = new Rect(innerRect.xMin + 30f, curY, innerRect.width - 60f, 32f);
        var titleTextEntryRect = titleRowRect.RightPart(0.9f);
        var pinButtonSize = titleRowRect.height;
        titleTextEntryRect.xMax -= pinButtonSize + 8f;
        var pinIconRect = new Rect(titleRowRect.xMax - pinButtonSize, curY, pinButtonSize, pinButtonSize);

        Widgets.Label(new Rect(titleRowRect.xMin, titleRowRect.yMin + 4f, Text.CalcSize("Title".Translate()).x, 32f),
            "Title".Translate());
        _reminderTitle =
            // ReSharper disable once RedundantArgumentDefaultValue
            Widgets.TextField(titleTextEntryRect, _reminderTitle, MaxTitleLength, null!)
            ?? _reminderTitle;
        _reminderTitle = SanitizeText(_reminderTitle);

        // Pin button
        Widgets.Checkbox(pinIconRect.xMin, pinIconRect.yMin, ref _pinned, pinButtonSize,
            texChecked: Icons.PinIcon, texUnchecked: Icons.PinOutline);
        TooltipHandler.TipRegionByKey(pinIconRect, "BetterLetters_PinReminder");

        curY += titleRowRect.height + 4f;
    }

    private void DoBodyTextEntry(Rect innerRect, ref float curY)
    {
        const float paddingLeft = 16f;
        innerRect.xMin += paddingLeft;

        curY += 4f;

        Text.Font = GameFont.Tiny;
        Widgets.Label(innerRect.xMin, ref curY, innerRect.width, "BetterLetters_ReminderTextLabel".Translate());
        Text.Font = GameFont.Small;

        var reminderTextRect = new Rect(innerRect.xMin, curY, innerRect.width, 80f);

        var reminderTextOuterRect = reminderTextRect;
        var reminderTextViewRect = reminderTextRect;
        Widgets.AdjustRectsForScrollView(reminderTextRect, ref reminderTextOuterRect, ref reminderTextViewRect);
        var reminderTextHeight = _reminderText.Split('\n').ToList().Count * 32f;
        reminderTextViewRect.height =
            Mathf.Max(reminderTextViewRect.height, reminderTextHeight) - 12f;

        Widgets.BeginScrollView(reminderTextOuterRect, ref _scrollPosition, reminderTextViewRect);
        _reminderText = Widgets.TextArea(reminderTextViewRect, _reminderText) ?? _reminderText;
        Widgets.EndScrollView();

        curY += reminderTextRect.height + 4f;
    }

    private void DoReminderSettings(Rect innerRect, ref float curY)
    {
        const float snoozeSettingsWidthPct = 0.85f;
        const float extraSettingsRowHeight = 32f;
        var snoozeSettingsWidth = innerRect.width * snoozeSettingsWidthPct;
        var snoozeSettingsLeftMargin = ((innerRect.width - snoozeSettingsWidth) / 2f);

        CustomWidgets.SnoozeSettings(
            x: innerRect.xMin + (innerRect.xMin + snoozeSettingsLeftMargin),
            y: ref curY,
            width: snoozeSettingsWidth,
            durationTicks: ref _durationTicks);

        curY += 8f;

        var letterDefSelectorRect = new Rect(innerRect.xMin + snoozeSettingsLeftMargin, curY, snoozeSettingsWidth,
            extraSettingsRowHeight).MiddlePart(0.8f, 1.0f);
        DoLetterDefSelector(letterDefSelectorRect);
        curY += extraSettingsRowHeight;

        curY += 4f;

        var jumpToSelectorRect = new Rect(innerRect.xMin + snoozeSettingsLeftMargin, curY, snoozeSettingsWidth,
            extraSettingsRowHeight).MiddlePart(0.8f, 1.0f);
        DoJumpToSelector(jumpToSelectorRect);
        curY += extraSettingsRowHeight;
    }

    private void DoLetterDefSelector(Rect inRect)
    {
        var buttonRect = inRect.RightPart(0.6f);
        var iconWidthScaled = LetterIconSize.x * (32f / LetterIconSize.y);
        var iconRect =
            new Rect(buttonRect.xMin - iconWidthScaled, buttonRect.yMin, iconWidthScaled, 32f);

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(inRect.LeftPart(0.3f), (string)"BetterLetters_LetterTypeLabel".Translate());
        Text.Anchor = TextAnchor.UpperLeft;

        GUI.color = _letterDef.color;
        var clicked = Widgets.ButtonText(buttonRect, _letterDef.defName!.Truncate(buttonRect.width - 4f)!);

        Widgets.DrawTextureFitted(iconRect, _letterDef.Icon!, scale: 0.6f);
        GUI.color = Color.white;

        if (!clicked)
            return;

#if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5)
        var floatMenuOptions = ValidLetterDefs.Select(letterDef => new FloatMenuOption(label: letterDef.defName!,
            () => _letterDef = letterDef, iconTex: letterDef.Icon!, iconColor: letterDef.color)).ToList();
#else
        var floatMenuOptions = ValidLetterDefs.Select(letterDef => new FloatMenuOption(label: letterDef.defName!,
            () => _letterDef = letterDef)).ToList();
#endif

        Find.WindowStack?.Add(new FloatMenu(floatMenuOptions));
        SoundDefOf.FloatMenu_Open?.PlayOneShotOnCamera();
        Event.current?.Use();
    }

    private void DoJumpToSelector(Rect inRect)
    {
        var thingLabel = _selectedThing?.LabelShortCap ?? "BetterLetters_NothingSelected".Translate();
        var buttonRect = inRect.RightPart(0.6f);
        var iconWidthScaled = LetterIconSize.x * (32f / LetterIconSize.y);
        var iconRect = new Rect(buttonRect.xMin - iconWidthScaled, buttonRect.yMin, iconWidthScaled, 32f);

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(inRect.LeftPart(0.3f), "BetterLetters_JumpToColon".Translate());
        Text.Anchor = TextAnchor.UpperLeft;

        TooltipHandler.TipRegionByKey(buttonRect, "BetterLetters_JumpToColonTooltip");

        var clicked =
            Widgets.ButtonText(buttonRect, thingLabel.Truncate(buttonRect.width - 4f)!);

#if v1_6
        if (_selectedThing is not null)
        {
            Widgets.ThingIcon(iconRect, _selectedThing, scale: 0.6f);
        }
#else
        if (_selectedThing is not null)
        {
            Widgets.ThingIcon(iconRect, _selectedThing);
        }
#endif
        if (clicked)
        {
            var floatMenuOptions = new List<FloatMenuOption>
            {
                new FloatMenuOption("BetterLetters_NothingSelected".Translate(), () => { _selectedThing = null; },
                    MenuOptionPriority.AttackEnemy),
                new FloatMenuOption("BetterLetters_SelectSomething".Translate(), DoSelectThing
#if v1_6
                    ,
                    iconTex: TexButton.Plus!,
                    iconColor: Color.white, priority: MenuOptionPriority.AttackEnemy
#endif
                )
            };

            var addedSelectedThing = false;
            foreach (var o in Find.Selector?.SelectedObjectsListForReading ?? new List<object>())
            {
                var priority = MenuOptionPriority.Default;
                if (o is not Thing thing) continue;
                if (thing is Pawn) priority = MenuOptionPriority.High;
                floatMenuOptions.Add(
                    new FloatMenuOption(thing.LabelCap ?? string.Empty, () => _selectedThing = thing,
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
                floatMenuOptions.Add(new FloatMenuOption(_selectedThing.LabelCap ?? string.Empty, null!,
#if v1_4 || v1_5 || v1_6
                    _selectedThing, Color.white,
#endif
                    priority: MenuOptionPriority.InitiateSocial));
            }

            Find.WindowStack?.Add(new FloatMenu(floatMenuOptions));
            SoundDefOf.FloatMenu_Open?.PlayOneShotOnCamera();
            Event.current?.Use();
        }
    }

    private void DoCloseButtons(Rect innerRect)
    {
        var buttonsSize = new Vector2(120f, 32f);
        var buttonsRect = innerRect.BottomPartPixels(buttonsSize.y)
            .MiddlePartPixels(buttonsSize.x * 3.2f, buttonsSize.y);

        // Bottom buttons
        GUI.color = Color.white; // RW version < 1.4 Widgets.ButtonImage has a bug that fails to reset the GUI color
        if (Widgets.ButtonText(buttonsRect.MiddlePartPixels(buttonsSize.x, buttonsSize.y), "Confirm".Translate()))
        {
            LookTargets? lookTargets = null;
            if (_selectedThing is not null)
            {
                lookTargets = new LookTargets(_selectedThing);
            }

            LetterUtils.AddReminder(SanitizeText(_reminderTitle), SanitizeText(_reminderText), _letterDef,
                _durationTicks, _pinned, lookTargets);
            Close();
        }
    }
}
