using System;
using System.Collections.Generic;
using BetterLetters.Patches;
using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace BetterLetters;

internal class Settings : ModSettings
{
    private class SettingAttribute : Attribute
    {
    }

    internal enum PinTextureMode
    {
        Round = 1,
        Alt = 2
    }

    internal enum ButtonPlacement
    {
        TopLeft,
        TopMiddle,
        TopRight,
        BottomLeft,
        BottomMiddle,
        BottomRight
    }

    [Flags]
    internal enum LetterButtonsType
    {
        DiaOptions = 2,
        Icons = 4
    }

    internal enum QuestExpirationSounds
    {
        LetterArrive,
        BadUrgentSmall,
        BadUrgent,
        BadUrgentBig,
        RitualNegative,
    }

    // ReSharper disable RedundantDefaultMemberInitializer
    [Setting] internal static PinTextureMode PinTexture = PinTextureMode.Alt;
    [Setting] internal static ButtonPlacement LetterButtonsPosition = ButtonPlacement.BottomRight;

    [Setting] internal static LetterButtonsType LetterButtonsEnabledTypes =
        LetterButtonsType.Icons | LetterButtonsType.DiaOptions;

    internal static bool IconButtonsEnabled => LetterButtonsEnabledTypes.HasFlag(LetterButtonsType.Icons);
    internal static bool DiaOptionButtonsEnabled => LetterButtonsEnabledTypes.HasFlag(LetterButtonsType.DiaOptions);

    [Setting] internal static int MaxNumSnoozes = 30;
    [Setting] internal static int SnoozeTickPeriod = GenTicks.TicksPerRealSecond;

    [Setting] internal static bool DisableRightClickPinnedLetters = false;
    [Setting] internal static bool ReplaceLetterIconsInXML = true;
    [Setting] internal static bool DisableBounceIfPinned = true;
    [Setting] internal static bool DisableBounceAlways = false;
    [Setting] internal static bool DisableFlashIfPinned = true;
    [Setting] internal static bool DisableFlashAlways = false;
    [Setting] internal static int MaxSnoozeDuration = GenDate.TicksPerYear * 5;
    [Setting] internal static bool SnoozePinned = true;
    [Setting] internal static bool SnoozeOpen = false;
    [Setting] internal static bool DoCreateReminderPlaySetting = true;
    [Setting] internal static bool AutoSelectThingForReminders = true;

    [Setting] internal static bool DismissedQuestsDismissLetters = true;
    [Setting] internal static bool KeepQuestLettersOnStack = true;
    [Setting] internal static bool ChangeExpiredQuestLetters = true;
    [Setting] internal static QuestExpirationSounds QuestExpirationSound = QuestExpirationSounds.LetterArrive;
    // ReSharper restore RedundantDefaultMemberInitializer

    internal static Dictionary<string, object> DefaultSettings = new();

    // GUI Stuff
    internal enum SettingsTab
    {
        Main,
        Pinning,
        Snoozing,
        Cache
    }

    internal static SettingsTab CurrentTab = SettingsTab.Main;
    private const float TabHeight = 32f;

    public Settings()
    {
        HarvestSettingsDefaults(out DefaultSettings);
        Log.Trace("Default settings loaded");
    }


    private void HarvestSettingsDefaults(out Dictionary<string, object> settings, Type? owningType = null)
    {
        settings = new Dictionary<string, object>();
        if (owningType is null) owningType = GetType();

        var fields = AccessTools.GetDeclaredFields(owningType)!;
        Log.Trace($"Harvesting {fields.Count} settings for type '{owningType.FullName}'");
        foreach (var field in fields)
        {
            if (!field.IsStatic) continue;
            var fieldAttr = field.GetCustomAttributes(typeof(SettingAttribute), false);
            if (fieldAttr.Length == 0) continue;
            settings[field.Name] = field.GetValue(this);
            Log.Trace($"Harvested Setting: '{field.Name}' Default: {settings[field.Name]}");
        }
    }

    internal static string GetSettingLabel(string key, bool showValue = false)
    {
        if (!showValue) return $"BetterLetters_Settings_{key}".Translate();

        var value = AccessTools.Field(typeof(Settings), key)?.GetValue(null!)?.ToString() ?? null;
        if (value is null) return $"BetterLetters_Settings_{key}".Translate();

        return $"BetterLetters_Settings_{key}".Translate() + ": " + value;
    }

    internal static string GetSettingTooltip(string key)
    {
        var success = $"BetterLetters_Settings_{key}_Desc".TryTranslate(out var str);

        if (!success)
        {
            str = "";
            return str;
        }

        if (DefaultSettings.TryGetValue(key, out var setting))
            str += "BetterLetters_Settings_DefaultSuffix".Translate(setting!.ToString());

        return str;
    }

    internal void DoWindowContents(Rect inRect)
    {
        var tabs = new List<TabRecord>
        {
            new TabRecord("BetterLetters_Settings_Tab_Main".Translate(), () => CurrentTab = SettingsTab.Main,
                () => CurrentTab == SettingsTab.Main),
            new TabRecord("BetterLetters_Settings_Tab_Pinning".Translate(), () => CurrentTab = SettingsTab.Pinning,
                () => CurrentTab == SettingsTab.Pinning),
            new TabRecord("BetterLetters_Settings_Tab_Snoozing".Translate(), () => CurrentTab = SettingsTab.Snoozing,
                () => CurrentTab == SettingsTab.Snoozing),
        };
        if (Prefs.DevMode && WorldComponent_SnoozeManager.Instance is not null)
        {
            tabs.Add(new TabRecord("BetterLetters_Settings_Tab_Cache".Translate(),
                () => CurrentTab = SettingsTab.Cache, () => CurrentTab == SettingsTab.Cache));
        }
#if v1_5 || v1_6
        TabDrawer.DrawTabsOverflow(inRect.TopPartPixels(TabHeight), tabs, 80f, 200f);
#elif v1_1 || v1_2 || v1_3 || v1_4
        var tabsRect = inRect.TopPartPixels(TabHeight);
        tabsRect.y += TabHeight;
        TabDrawer.DrawTabs(tabsRect, tabs, 200f);
#endif
        Widgets.DrawLineHorizontal(inRect.xMin, inRect.yMin + TabHeight, inRect.width);

        var tabRect = inRect.BottomPartPixels(inRect.height - TabHeight - 32f);
        switch (CurrentTab)
        {
            case SettingsTab.Main:
                try
                {
                    DoTabMain(tabRect);
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error drawing main settings tab.", true);
                    CurrentTab = SettingsTab.Pinning;
                }

                break;
            case SettingsTab.Pinning:
                try
                {
                    DoTabPinning(tabRect);
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error drawing pin settings tab.", true);
                    CurrentTab = SettingsTab.Main;
                }

                break;

            case SettingsTab.Snoozing:
                try
                {
                    DoTabSnoozing(tabRect);
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error drawing snooze settings tab.", true);
                    CurrentTab = SettingsTab.Main;
                }

                break;
            case SettingsTab.Cache:
                try
                {
                    DoTabCache(tabRect);
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error drawing cache tab.", true);
                    CurrentTab = SettingsTab.Main;
                }

                break;
            default:
                CurrentTab = SettingsTab.Main;
                break;
        }
    }

    private static float? _miscSectionLastHeight = null;
    private static float? _questSectionLastHeight = null;

    private static void DoTabMain(Rect inRect)
    {
        var listing = new Listing_Standard();
        var innerRect = inRect.MiddlePart(0.85f, 1f);
        listing.Begin(innerRect);

        listing.ColumnWidth = innerRect.width / 2.05f;

        var miscSection = listing.BeginSection(_miscSectionLastHeight ?? 9999f)!;

        miscSection.SectionHeader("BetterLetters_Settings_Section_Misc");

        miscSection.CheckboxLabeled(GetSettingLabel("DisableBounceAlways"), ref DisableBounceAlways,
            GetSettingTooltip("DisableBounceAlways"), 36f, 0.90f);
        if (!DisableBounceAlways)
        {
            miscSection.Indent();
            miscSection.CheckboxLabeled(GetSettingLabel("DisableBounceIfPinned"), ref DisableBounceIfPinned,
                GetSettingTooltip("DisableBounceIfPinned"), 28f, 0.87f);
            miscSection.Outdent();
        }

        miscSection.Gap(4f);

        miscSection.CheckboxLabeled(GetSettingLabel("DisableFlashAlways"), ref DisableFlashAlways,
            GetSettingTooltip("DisableFlashAlways"), 36f, 0.90f);
        if (!DisableFlashAlways)
        {
            miscSection.Indent();
            miscSection.CheckboxLabeled(GetSettingLabel("DisableFlashIfPinned"), ref DisableFlashIfPinned,
                GetSettingTooltip("DisableFlashIfPinned"), 28f, 0.87f);
            miscSection.Outdent();
        }

        _miscSectionLastHeight = miscSection.MaxColumnHeightSeen;
        listing.EndSection(miscSection);

        listing.NewColumn();

        var questSection = listing.BeginSection(_questSectionLastHeight ?? 9999f)!;

        questSection.SectionHeader("BetterLetters_Settings_Section_Quests");

        questSection.CheckboxLabeled(GetSettingLabel("DismissedQuestsDismissLetters"),
            ref DismissedQuestsDismissLetters,
            GetSettingTooltip("DismissedQuestsDismissLetters"), 36f, 0.90f);

        questSection.CheckboxLabeled(GetSettingLabel("KeepQuestLettersOnStack"), ref KeepQuestLettersOnStack,
            GetSettingTooltip("KeepQuestLettersOnStack"), 36f, 0.90f);

        questSection.CheckboxLabeled(GetSettingLabel("ChangeExpiredQuestLetters"), ref ChangeExpiredQuestLetters,
            GetSettingTooltip("ChangeExpiredQuestLetters"), 36f, 0.90f);

        var expirationSoundLabelRect = questSection.Label(GetSettingLabel("QuestExpirationSound"));
        var extraHeight = 0f;
        foreach (QuestExpirationSounds soundOption in Enum.GetValues(typeof(QuestExpirationSounds)))
        {
            var disabled = !ChangeExpiredQuestLetters;
            if (questSection.RadioButton(
                    $"BetterLetters_Settings_QuestExpirationSound_{soundOption.ToString()}".Translate(),
                    soundOption == QuestExpirationSound, 0f, tabInRight: 0.6f, null!, null, disabled))
            {
                QuestExpirationSound = soundOption;
            }

            extraHeight += 32f;
        }

        TooltipHandler.TipRegion(
            expirationSoundLabelRect with { height = expirationSoundLabelRect.height + extraHeight },
            "BetterLetters_Settings_QuestExpirationSound_Desc".Translate());

        _questSectionLastHeight = questSection.MaxColumnHeightSeen;
        listing.EndSection(questSection);

        listing.End();
    }

    private static void DoTabQuests(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect.MiddlePart(0.75f, 1f));


        listing.End();
    }

    private static float? _lastLeftSectionHeight = null;
    private static float? _lastRightSectionHeight = null;

    private static void DoTabPinning(Rect inRect)
    {
        var listing = new Listing_Standard();
        var lsRect = inRect.MiddlePart(0.85f, 1f);
        listing.Begin(lsRect);
        var leftColWidth = lsRect.width / 2.05f;
        var rightColWidth = lsRect.width / 2.05f;
        listing.ColumnWidth = leftColWidth;

        var leftSection = listing.BeginSection(_lastLeftSectionHeight ?? 9999f)!;

        listing.CheckboxLabeled(GetSettingLabel("DisableRightClickPinnedLetters"),
            ref DisableRightClickPinnedLetters,
            GetSettingTooltip("DisableRightClickPinnedLetters"), 36f);

        leftSection.Gap(8f);

        var buttonSize = Patch_Dialog_NodeTree_DoWindowContents_AddPinSnoozeButtons.ButtonSize;
        var labelRect = leftSection.Label(GetSettingLabel("PinTexture"));
        var tabIn = buttonSize + 4f;
        var tabInRight = leftColWidth * 0.2f;
        if (leftSection.RadioButton("BetterLetters_Settings_PinTexture_Round".Translate(),
                PinTexture == PinTextureMode.Round, tabIn, tabInRight, null!, null, false))
        {
            PinTexture = PinTextureMode.Round;
        }
        else if (leftSection.RadioButton("BetterLetters_Settings_PinTexture_Alt".Translate(),
                     PinTexture == PinTextureMode.Alt, tabIn, tabInRight, null!, null, false))
        {
            PinTexture = PinTextureMode.Alt;
        }

        var pinTexRect = labelRect with { y = labelRect.yMax + 2f, width = buttonSize, height = buttonSize };
        GUI.DrawTexture(pinTexRect, Icons.PinRound);
        pinTexRect.y += 24f;
        GUI.DrawTexture(pinTexRect, Icons.PinAlt);

        _lastLeftSectionHeight = leftSection.MaxColumnHeightSeen;
        listing.EndSection(leftSection);

        // RIGHT COLUMN
        listing.NewColumn();

        var rightSection = listing.BeginSection(_lastRightSectionHeight ?? 9999f)!;

        var letterButtonTypesRect = rightSection.Label(GetSettingLabel("LetterButtonTypes"));

        var buttonTypeDiaOptions = LetterButtonsEnabledTypes.HasFlag(LetterButtonsType.DiaOptions);
#if !(v1_1 || v1_2 || v1_3)
        rightSection.CheckboxLabeled("BetterLetters_Settings_LetterButtonTypes_DiaOptions".Translate(),
            ref buttonTypeDiaOptions, tabIn);
#else
        rightSection.CheckboxLabeled("BetterLetters_Settings_LetterButtonTypes_DiaOptions".Translate(),
            ref buttonTypeDiaOptions);
#endif
        if (buttonTypeDiaOptions)
            LetterButtonsEnabledTypes |= LetterButtonsType.DiaOptions;
        else
            LetterButtonsEnabledTypes &= ~LetterButtonsType.DiaOptions;

        var buttonTypeIcons = LetterButtonsEnabledTypes.HasFlag(LetterButtonsType.Icons);
#if !(v1_1 || v1_2 || v1_3)
        rightSection.CheckboxLabeled("BetterLetters_Settings_LetterButtonTypes_Icons".Translate(),
            ref buttonTypeIcons, tabIn);
#else
        rightSection.CheckboxLabeled("BetterLetters_Settings_LetterButtonTypes_Icons".Translate(),
            ref buttonTypeIcons);
#endif
        if (buttonTypeIcons)
            LetterButtonsEnabledTypes |= LetterButtonsType.Icons;
        else
            LetterButtonsEnabledTypes &= ~LetterButtonsType.Icons;

        TooltipHandler.TipRegion(
            letterButtonTypesRect with { height = letterButtonTypesRect.height + 64f },
            GetSettingTooltip("LetterButtonTypes"));

        rightSection.GapLine(8f);

        var letterButtonsPositionRect = rightSection.Label(GetSettingLabel("LetterButtonsPosition"));

        var extraHeight = 0f;
        foreach (ButtonPlacement placement in Enum.GetValues(typeof(ButtonPlacement)))
        {
            var disabled = !LetterButtonsEnabledTypes.HasFlag(LetterButtonsType.Icons);
            if (rightSection.RadioButton(GetSettingLabel(placement.ToString()),
                    placement == LetterButtonsPosition, 0f, tabInRight: 0.6f, null!, null, disabled))
            {
                LetterButtonsPosition = placement;
                if (Patch_Dialog_NodeTree_DoWindowContents_AddPinSnoozeButtons.CurrentLetter is not null)
                {
                    Patch_Dialog_NodeTree_DoWindowContents_AddPinSnoozeButtons.CalcButtonPositions();
                }
            }

            extraHeight += 32f;
        }

        TooltipHandler.TipRegion(
            letterButtonsPositionRect with { height = letterButtonsPositionRect.height + extraHeight },
            GetSettingTooltip("LetterButtonsPosition"));

        _lastRightSectionHeight = rightSection.MaxColumnHeightSeen;
        listing.EndSection(rightSection);

        listing.End();
    }

    private static string _editBufferMaxNumSnoozes = MaxNumSnoozes.ToString();
    private static Vector2 _scrollPositionSnoozeTab = Vector2.zero;
    private static float? _lastSnoozeTabHeight = null;
    private static float? _lastSnoozeSectionHeight = null;
    private static float? _lastRemindersSectionHeight = null;

    private static void DoTabSnoozing(Rect inRect)
    {
        // Calculate the height of the tab's contents for the scrollbar rects
        var viewRectSnoozeTab = new Rect(inRect);
        var outerRectSnoozeTab = new Rect(inRect);
#if !(v1_1 || v1_2 || v1_3 || v1_4)
        Widgets.AdjustRectsForScrollView(inRect, ref outerRectSnoozeTab, ref viewRectSnoozeTab);
#else
        LegacySupport.AdjustRectsForScrollView(inRect, ref outerRectSnoozeTab, ref viewRectSnoozeTab);
#endif
        viewRectSnoozeTab.height = _lastSnoozeTabHeight ?? inRect.height * 1.5f;

        Widgets.BeginScrollView(outerRectSnoozeTab, ref _scrollPositionSnoozeTab, viewRectSnoozeTab);
        var listing = new Listing_Standard();
        var innerRect = viewRectSnoozeTab.MiddlePart(0.85f, 1f);
        listing.Begin(innerRect);
        var yTop = listing.GetRect(0f, 1f).yMin;

        listing.ColumnWidth = innerRect.width / 2.05f;

        // Snoozing section
        var snoozeSection = listing.BeginSection(_lastSnoozeSectionHeight ?? 9999f)!;
        snoozeSection.SectionHeader("BetterLetters_Settings_Section_Snoozing");

        snoozeSection.CheckboxLabeled(GetSettingLabel("SnoozePinned"), ref SnoozePinned,
            GetSettingTooltip("SnoozePinned"), 28f, 0.9f);

        snoozeSection.CheckboxLabeled(GetSettingLabel("SnoozeOpen"), ref SnoozeOpen,
            null!, 28f, 0.9f);

        snoozeSection.GapLine(42f);

        snoozeSection.Label(GetSettingLabel("MaxSnoozeDuration", true));
        var snoozesRect = snoozeSection.GetRect(0f, 1f);
        var curY = snoozesRect.yMin;
        CustomWidgets.SnoozeSettings(snoozesRect.xMin, ref curY, snoozesRect.width, ref MaxSnoozeDuration, 0f, 0f, 0f,
            maxDurationOverride: GenDate.TicksPerYear * 100, showEndDate: false);
        MaxSnoozeDuration = Mathf.Clamp(MaxSnoozeDuration, GenDate.TicksPerHour, GenDate.TicksPerYear * 100);
        snoozeSection.GetRect((curY - snoozesRect.yMin) + 4f);
        var defaultMaxSnoozeDuration = DefaultSettings["MaxSnoozeDuration"] as int? ?? GenDate.TicksPerYear * 5;
#if !(v1_1 || v1_2 || v1_3)
        if (snoozeSection.ButtonText("Default".Translate() + "(" + defaultMaxSnoozeDuration.ToStringTicksToPeriodVague() + ")"))
#else
        if (snoozeSection.ButtonText("Default".Translate()))
#endif
        {
            MaxSnoozeDuration = defaultMaxSnoozeDuration;
        }

        snoozeSection.GapLine(42f);

        snoozeSection.IntSetting(ref MaxNumSnoozes, "MaxNumSnoozes", ref _editBufferMaxNumSnoozes, min: 1,
            max: 200);

        snoozeSection.GapLine(42f);
        var periodLabelRect = snoozeSection.GetRect(1f, 0.5f);
        curY = periodLabelRect.yMin;
#if !(v1_1 || v1_2)
        Widgets.Label(periodLabelRect.xMin, ref curY, periodLabelRect.width, GetSettingLabel("SnoozeTickPeriod", true));
#else
        LegacySupport.Label(periodLabelRect.xMin, ref curY, periodLabelRect.width, "BetterLetters_ReminderTextLabel".Translate());
#endif
        snoozeSection.GetRect((curY - periodLabelRect.yMin) + 4f);

        snoozeSection.GapLine();

        var periodRect = snoozeSection.GetRect(1f, 1f);
        CustomWidgets.SnoozeSettings(periodRect.xMin, ref curY, periodRect.width, ref SnoozeTickPeriod, 0f, 0f, 0f,
            maxDurationOverride: GenDate.TicksPerHour, showEndDate: false);
        SnoozeTickPeriod = Mathf.Max(SnoozeTickPeriod, GenTicks.TicksPerRealSecond); // Minimum
        snoozeSection.GetRect(curY - periodRect.yMin);
        var defaultSnoozeTickPeriod = DefaultSettings["SnoozeTickPeriod"] as int? ?? GenTicks.TicksPerRealSecond;
#if !(v1_1 || v1_2 || v1_3)
        if (snoozeSection.ButtonText("Default".Translate() + "(" + defaultSnoozeTickPeriod.ToStringTicksToPeriodVague() + ")"))
#else
        if (snoozeSection.ButtonText("Default".Translate()))
#endif
        {
            SnoozeTickPeriod = defaultSnoozeTickPeriod;
        }

        snoozeSection.SubLabel(GetSettingTooltip("SnoozeTickPeriod"), 0.9f);

        _lastSnoozeSectionHeight = snoozeSection.MaxColumnHeightSeen;
        listing.EndSection(snoozeSection);

        listing.NewColumn();

        var remindersSection = listing.BeginSection(_lastRemindersSectionHeight ?? 9999f)!;
        remindersSection.SectionHeader("BetterLetters_Settings_Section_Reminders");

        remindersSection.CheckboxLabeled(GetSettingLabel("DoCreateReminderPlaySetting"),
            ref DoCreateReminderPlaySetting, GetSettingTooltip("DoCreateReminderPlaySetting"), 32f, 0.9f);

        remindersSection.CheckboxLabeled(GetSettingLabel("AutoSelectThingForReminders"),
            ref AutoSelectThingForReminders, GetSettingTooltip("AutoSelectThingForReminders"), 32f, 0.9f);

        _lastRemindersSectionHeight = remindersSection.MaxColumnHeightSeen;
        listing.EndSection(remindersSection);

        _lastSnoozeTabHeight = _lastSnoozeSectionHeight + _lastRemindersSectionHeight + 16f;
        listing.End();
        Widgets.EndScrollView();
    }

    private static void DoTabCache(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect.MiddlePart(0.75f, 1f));

        // If in-game, show a list of currently snoozed letters here
        DoSnoozesListing(listing);

        listing.End();
    }

    /// <summary>
    /// Draws a list of all snoozed letters in an ongoing game along with buttons to unsnooze them.<br />
    /// Meant as a last-resort way for users to clean up snoozes that they can't find through ingame means.
    /// </summary>
    private static void DoSnoozesListing(Listing_Standard listing)
    {
        if (WorldComponent_SnoozeManager.Instance == null)
            return;

        listing.GapLine();
        listing.Label(
            "BetterLetters_Settings_CurrentlySnoozed".Translate(WorldComponent_SnoozeManager.NumSnoozes,
                WorldComponent_SnoozeManager.MaxNumSnoozes));

        var snoozedLetters = WorldComponent_SnoozeManager.Snoozes;
        if (snoozedLetters.Count == 0)
        {
            listing.Label("BetterLetters_Settings_NoSnoozedLetters".Translate());
            return;
        }

        var section = listing.BeginSection(300f)!;
        Letter? snoozeToRemove = null; // Doing it this way to avoid modifying the collection mid-loop
        Letter? snoozeToFire = null; // Doing it this way to avoid modifying the collection mid-loop
        foreach (var snooze in snoozedLetters)
        {
            var remainingTime = snooze.Value?.RemainingTicks.ToStringTicksToPeriodVerbose();
            var rect = section.GetRect(34f, 0.9f);
            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.5f));
            rect = rect.ContractedBy(4f);
            var buttonRemoveText = "BetterLetters_Settings_Unsnooze".Translate();
            var buttonFireText = "BetterLetters_Settings_Fire".Translate();
            var buttonsRect =
                rect.RightPartPixels(Text.CalcSize(buttonRemoveText).x + Text.CalcSize(buttonFireText).x + 64f);
            if (Widgets.ButtonTextSubtle(buttonsRect.RightHalf(), buttonRemoveText))
            {
                snoozeToRemove = snooze.Key;
            }

            if (snooze.Key is not null && Widgets.ButtonTextSubtle(buttonsRect.LeftHalf(), buttonFireText))
            {
                snoozeToFire = snooze.Key;
                snoozeToRemove = snooze.Key;
            }

#if v1_4 || v1_5 || v1_6
            var label = $"{snooze.Key?.Label ?? "null"} ({remainingTime})";
#elif v1_1 || v1_2 || v1_3
            var label = $"{snooze.Key?.label} ({remainingTime})";
#endif
            Widgets.Label(rect.LeftPartPixels(rect.width - 64f).MiddlePart(0.9f, 1f), label);
        }

        if (snoozeToFire is not null)
        {
            if (WorldComponent_SnoozeManager.Snoozes[snoozeToFire]?.Letter is { } letter)
                Find.LetterStack?.ReceiveLetter(letter);
        }

        if (snoozeToRemove is not null)
        {
            WorldComponent_SnoozeManager.RemoveSnooze(snoozeToRemove);
        }

        listing.EndSection(section);
    }

    public override void ExposeData()
    {
        // ReSharper disable RedundantArgumentDefaultValue
        Scribe_Values.Look(ref PinTexture, "PinTexture", PinTextureMode.Alt);
        Scribe_Values.Look(ref LetterButtonsPosition, "LetterButtonsPosition", ButtonPlacement.BottomRight);
        Scribe_Values.Look(ref LetterButtonsEnabledTypes, "LetterButtonsEnabledTypes",
            LetterButtonsType.Icons & LetterButtonsType.DiaOptions);
        Scribe_Values.Look(ref MaxSnoozeDuration, "MaxSnoozeDuration", GenDate.TicksPerYear * 5);
        Scribe_Values.Look(ref MaxNumSnoozes, "MaxNumSnoozes", 30);
        Scribe_Values.Look(ref SnoozeTickPeriod, "SnoozeTickPeriod", GenTicks.TickLongInterval);
        Scribe_Values.Look(ref DisableRightClickPinnedLetters, "DisableRightClickPinnedLetters", false);
        Scribe_Values.Look(ref DisableBounceIfPinned, "DisableBounceIfPinned", true);
        Scribe_Values.Look(ref DisableBounceAlways, "DisableBounceAlways", false);
        Scribe_Values.Look(ref DisableFlashIfPinned, "DisableFlashIfPinned", true);
        Scribe_Values.Look(ref DisableFlashAlways, "DisableFlashAlways", false);
        Scribe_Values.Look(ref SnoozePinned, "SnoozePinned", true);
        Scribe_Values.Look(ref SnoozeOpen, "SnoozeOpen", false);
        Scribe_Values.Look(ref DoCreateReminderPlaySetting, "DoCreateReminderPlaySetting", true);
        Scribe_Values.Look(ref AutoSelectThingForReminders, "AutoSelectThingForReminders", true);

        Scribe_Values.Look(ref QuestExpirationSound, "QuestExpirationSound", QuestExpirationSounds.LetterArrive);
        Scribe_Values.Look(ref DismissedQuestsDismissLetters, "DismissedQuestsDismissLetters", true);
        Scribe_Values.Look(ref KeepQuestLettersOnStack, "KeepQuestLettersOnStack", true);
        Scribe_Values.Look(ref ChangeExpiredQuestLetters, "ChangeExpiredQuestLetters", true);
        // ReSharper restore RedundantArgumentDefaultValue

        base.ExposeData();
    }
}
