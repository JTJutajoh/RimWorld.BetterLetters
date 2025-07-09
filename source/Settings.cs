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
        None,
        LetterArrive,
        BadUrgentSmall,
        BadUrgent,
        BadUrgentBig,
        RitualNegative,
    }

    // ReSharper disable RedundantDefaultMemberInitializer
    [Setting] internal static PinTextureMode PinTexture = PinTextureMode.Alt;
    [Setting] internal static ButtonPlacement LetterButtonsPosition = ButtonPlacement.BottomRight;

    [Setting] internal static LetterButtonsType LetterButtonsEnabledTypes = LetterButtonsType.Icons;

    internal static bool IconButtonsEnabled => LetterButtonsEnabledTypes.HasFlag(LetterButtonsType.Icons);
    internal static bool DiaOptionButtonsEnabled => LetterButtonsEnabledTypes.HasFlag(LetterButtonsType.DiaOptions);

    [Setting] internal static int MaxNumSnoozes = 30;
    [Setting] internal static int SnoozeTickPeriod = GenTicks.TicksPerRealSecond;

    [Setting] internal static bool DisableRightClickPinnedLetters = false;
    [Setting] internal static bool ReplaceLetterIconsInLetterStack = true;
    [Setting] internal static bool ReplaceLetterIconsInXML = true;
    [Setting] internal static bool ModifyLetterText = true;
    [Setting] internal static bool RaidAddDropPod = true;
    [Setting] internal static bool RaidAddRaidStrategy = true;
    [Setting] internal static bool RaidAddPawnCount = true;
    [Setting] internal static bool RaidAddGroupCount = true;
    [Setting] internal static bool AddBulkDismissButton = true;
    [Setting] internal static bool DisableBounceIfPinned = true;
    [Setting] internal static bool DisableBounceAlways = false;
    [Setting] internal static bool DisableFlashIfPinned = true;
    [Setting] internal static bool DisableFlashAlways = false;
    [Setting] internal static int MaxSnoozeDuration = GenDate.TicksPerYear * 2;
    [Setting] internal static int MinSnoozeDuration = GenDate.TicksPerHour / 4;
    [Setting] internal static bool SnoozePinned = true;
    [Setting] internal static bool SnoozeOpen = false;
    [Setting] internal static bool RemindersPinned = true;
    [Setting] internal static bool RemindersOpen = false;
    [Setting] internal static bool DoCreateReminderPlaySetting = true;
    [Setting] internal static bool AutoSelectThingForReminders = true;
    [Setting] internal static bool OffsetLetterLabels = true;
    [Setting] internal static float LetterLabelsOffsetAmount = 0f;

    [Setting] internal static bool AddQuestExpirationSnoozeOptions = true;

    [Setting] internal static bool DismissedQuestsDismissLetters = true;
    [Setting] internal static bool KeepQuestLettersOnStack = true;
    [Setting] internal static bool ChangeExpiredQuestLetters = true;
    [Setting] internal static QuestExpirationSounds QuestExpirationSound = QuestExpirationSounds.LetterArrive;

    [Setting] internal static List<string> EnabledPatchCategories = new()
    {
        "Letter_RemoveLetter_KeepOnStack",
        "Letter_RemoveLetter_KeepOnStack_QuestLetter",
        "Letter_OpenLetter_AddDiaOptions",
        "Dialog_AddIcons",
        "ArchivePin_AddBackToStack",
        "Letter_CanDismissWithRightClick_BlockIfPinned",
        "Letter_DrawInLetterStack",
        "Letter_CanCull_KeepSnoozes",
        "LetterStack_SortPinned",
        "PlaySettings_CreateReminderButton",
        "HistoryFiltersAndButtons",
        "ExpireQuestLetters",
        "BundleLetters",
        "HistoryArchivableRow",
        "QuestsTab_Buttons",
        "RaidLetter_AddDetails",
        "LetterStack_AddButtons",
        "LetterIconCaching",
    };

    [Setting] internal static List<string> DisabledPatchCategories = new();

    [Setting] internal static int RecentSnoozesMax = 10;
    [Setting] internal static int DurationSimilarityThreshold = GenDate.TicksPerHour / 2;
    [Setting] internal static List<int> RecentSnoozeDurations = new();
    // ReSharper restore RedundantDefaultMemberInitializer

    internal static Dictionary<string, object> DefaultSettings = new();

    // GUI Stuff
    internal enum SettingsTab
    {
        Main,
        Patches,
        Cache
    }

    internal static SettingsTab CurrentTab = SettingsTab.Main;
    private const float TabHeight = 32f;

    public Settings()
    {
        HarvestSettingsDefaults(out DefaultSettings);
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

        Log.Trace("Default settings loaded");
    }

    internal static string GetSettingLabel(string key, bool showValue = false, string unitString = "")
    {
        if (!showValue) return $"BetterLetters_Settings_{key}".Translate();

        var value = AccessTools.Field(typeof(Settings), key)?.GetValue(null!)?.ToString() ?? null;
        if (value is null) return $"BetterLetters_Settings_{key}".Translate();

        return $"BetterLetters_Settings_{key}".Translate() + ": " + value + unitString;
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

    internal static void TryCacheSnoozeDuration(Snooze snooze)
    {
        if (Mathf.Abs(GenDate.TicksPerDay - snooze.Duration) < DurationSimilarityThreshold)
            return;
        if (Mathf.Abs(GenDate.TicksPerHour - snooze.Duration) < DurationSimilarityThreshold)
            return;

        for (var index = 0; index < RecentSnoozeDurations.Count; index++)
        {
            // Look for any similar durations and adjust them to this new duration if they already exist
            if (Mathf.Abs(snooze.Duration - RecentSnoozeDurations[index]) < DurationSimilarityThreshold)
            {
                RecentSnoozeDurations[index] = snooze.Duration;
                RecentSnoozeDurations.RemoveAt(index);
                break;
            }
        }

        // If no similar duration was found, add a new one
        RecentSnoozeDurations.Add(snooze.Duration);
        if (RecentSnoozeDurations.Count > RecentSnoozesMax)
        {
            RecentSnoozeDurations.PopFront();
        }

        BetterLettersMod.Instance!.WriteSettings();
    }

    internal void DoWindowContents(Rect inRect)
    {
        var tabs = new List<TabRecord>
        {
            new TabRecord("BetterLetters_Settings_Tab_Main".Translate(), () => CurrentTab = SettingsTab.Main,
                () => CurrentTab == SettingsTab.Main),
            new TabRecord("BetterLetters_Settings_Tab_Patches".Translate(), () => CurrentTab = SettingsTab.Patches,
                () => CurrentTab == SettingsTab.Patches),
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

        var tabRect = inRect.BottomPartPixels(inRect.height - TabHeight);
        Widgets.DrawMenuSection(tabRect);
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
                }

                break;

            case SettingsTab.Patches:
                try
                {
                    DoTabPatches(tabRect);
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error drawing patches settings tab.", true);
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

    private static Vector2 _scrollPositionMainTab = Vector2.zero;
    private static float? _lastMainTabHeight;

    private static void DoTabMain(Rect inRect)
    {
        var viewRect = new Rect(inRect);
        var outerRect = new Rect(inRect);
#if !(v1_1 || v1_2 || v1_3 || v1_4)
        Widgets.AdjustRectsForScrollView(inRect, ref outerRect, ref viewRect);
#else
        LegacySupport.AdjustRectsForScrollView(inRect, ref outerRect, ref viewRect);
#endif
        viewRect.height = _lastMainTabHeight ?? inRect.height * 1.5f;

        Widgets.BeginScrollView(outerRect, ref _scrollPositionMainTab, viewRect);

        var listing = new Listing_Standard();
        var innerRect = viewRect.MiddlePart(0.95f, 1f);
        listing.Begin(innerRect);

        listing.ColumnWidth = innerRect.width / 2.05f;

        DoLetterStackSection(listing);

        DoBaseLettersSection(listing);

        DoDialogButtonsSection(listing);

        listing.NewColumn();

        DoTimeSection(listing);

        DoSnoozingSection(listing);
        DoRemindersSection(listing);

        DoQuestSection(listing);

        _lastMainTabHeight = listing.MaxColumnHeightSeen;
        listing.End();
        Widgets.EndScrollView();
    }

    private static float? _questSectionLastHeight;

    private static void DoQuestSection(Listing_Standard listing)
    {
        var section = listing.BeginSection(_questSectionLastHeight ?? 9999f)!;

        section.SectionHeader("BetterLetters_Settings_Section_Quests");

        section.CheckboxLabeled(GetSettingLabel("DismissedQuestsDismissLetters"),
            ref DismissedQuestsDismissLetters,
            GetSettingTooltip("DismissedQuestsDismissLetters"), 36f);

        section.CheckboxLabeled(GetSettingLabel("KeepQuestLettersOnStack"), ref KeepQuestLettersOnStack,
            GetSettingTooltip("KeepQuestLettersOnStack"), 36f);

        section.CheckboxLabeled(GetSettingLabel("ChangeExpiredQuestLetters"), ref ChangeExpiredQuestLetters,
            GetSettingTooltip("ChangeExpiredQuestLetters"), 36f);

        section.CheckboxLabeled(GetSettingLabel("AddQuestExpirationSnoozeOptions"), ref AddQuestExpirationSnoozeOptions,
            GetSettingTooltip("AddQuestExpirationSnoozeOptions"), 36f);

        var expirationSoundLabelRect = section.Label(GetSettingLabel("QuestExpirationSound"));
        var extraHeight = 0f;
        foreach (QuestExpirationSounds soundOption in Enum.GetValues(typeof(QuestExpirationSounds)))
        {
            var disabled = !ChangeExpiredQuestLetters;
            if (section.RadioButton(
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

        _questSectionLastHeight = section.MaxColumnHeightSeen;
        listing.EndSection(section);

        listing.Gap(24f);
    }

    private static float? _lastDialogButtonsSectionHeight;

    private static void DoDialogButtonsSection(Listing_Standard listing)
    {
        var section = listing.BeginSection(_lastDialogButtonsSectionHeight ?? 9999f)!;
        section.SectionHeader("BetterLetters_Settings_Section_DialogButtons");

        var letterButtonTypesRect = section.Label(GetSettingLabel("LetterButtonTypes"));

        var buttonTypeDiaOptions = LetterButtonsEnabledTypes.HasFlag(LetterButtonsType.DiaOptions);
#if !(v1_1 || v1_2 || v1_3)
        section.CheckboxLabeled("BetterLetters_Settings_LetterButtonTypes_DiaOptions".Translate(),
            ref buttonTypeDiaOptions);
#else
        section.CheckboxLabeled("BetterLetters_Settings_LetterButtonTypes_DiaOptions".Translate(),
            ref buttonTypeDiaOptions);
#endif
        if (buttonTypeDiaOptions)
            LetterButtonsEnabledTypes |= LetterButtonsType.DiaOptions;
        else
            LetterButtonsEnabledTypes &= ~LetterButtonsType.DiaOptions;

        var buttonTypeIcons = LetterButtonsEnabledTypes.HasFlag(LetterButtonsType.Icons);
#if !(v1_1 || v1_2 || v1_3)
        section.CheckboxLabeled("BetterLetters_Settings_LetterButtonTypes_Icons".Translate(),
            ref buttonTypeIcons);
#else
        section.CheckboxLabeled("BetterLetters_Settings_LetterButtonTypes_Icons".Translate(),
            ref buttonTypeIcons);
#endif
        if (buttonTypeIcons)
            LetterButtonsEnabledTypes |= LetterButtonsType.Icons;
        else
            LetterButtonsEnabledTypes &= ~LetterButtonsType.Icons;

        TooltipHandler.TipRegion(
            letterButtonTypesRect with { height = letterButtonTypesRect.height + 64f },
            GetSettingTooltip("LetterButtonTypes"));

        section.GapLine(8f);

        var letterButtonsPositionRect = section.Label(GetSettingLabel("LetterButtonsPosition"));

        var extraHeight = 0f;
        foreach (ButtonPlacement placement in Enum.GetValues(typeof(ButtonPlacement)))
        {
            var disabled = !LetterButtonsEnabledTypes.HasFlag(LetterButtonsType.Icons);
            if (section.RadioButton(GetSettingLabel(placement.ToString()),
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

        _lastDialogButtonsSectionHeight = section.MaxColumnHeightSeen;
        listing.EndSection(section);

        listing.Gap(24f);
    }

    private static float? _lastPinningSectionHeight;

    private static void DoLetterStackSection(Listing_Standard listing)
    {
        var section = listing.BeginSection(_lastPinningSectionHeight ?? 9999f)!;

        section.SectionHeader("BetterLetters_Settings_Section_LetterStack");

        section.CheckboxLabeled(GetSettingLabel("DisableRightClickPinnedLetters"),
            ref DisableRightClickPinnedLetters,
            GetSettingTooltip("DisableRightClickPinnedLetters"), 36f);

        section.CheckboxLabeled(GetSettingLabel("AddBulkDismissButton"), ref AddBulkDismissButton,
            GetSettingTooltip("AddBulkDismissButton"), 36f);

        section.CheckboxLabeled(GetSettingLabel("OffsetLetterLabels"), ref OffsetLetterLabels,
            GetSettingTooltip("OffsetLetterLabels"), 36f);

        if (!OffsetLetterLabels)
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
        var tempOffsetAmount = section.SliderLabeled(GetSettingLabel("LetterLabelsOffsetAmount", true, "px"),
            LetterLabelsOffsetAmount, -52f, 52f, 0.7f, GetSettingTooltip("LetterLabelsOffsetAmount"));
        if (OffsetLetterLabels)
            LetterLabelsOffsetAmount = Mathf.RoundToInt(tempOffsetAmount);
        GUI.color = Color.white;

        section.GapLine();

        section.Label("BetterLetters_Settings_ReplaceLetterIconsInLetterStack".Translate());
        if (section.RadioButton("BetterLetters_Settings_ReplaceLetterIconsInLetterStack_Enabled".Translate(),
                ReplaceLetterIconsInLetterStack, 0f, GetSettingTooltip("ReplaceLetterIconsInLetterStack")))
            ReplaceLetterIconsInLetterStack = true;
        if (section.RadioButton("BetterLetters_Settings_ReplaceLetterIconsInLetterStack_Disabled".Translate(),
                !ReplaceLetterIconsInLetterStack, 0f, GetSettingTooltip("ReplaceLetterIconsInLetterStack")))
            ReplaceLetterIconsInLetterStack = false;

        section.GapLine();

        if (section.RadioButton(GetSettingLabel("DisableBounceIfPinned"),
                (!DisableBounceAlways && DisableBounceIfPinned), 0f,
                GetSettingTooltip("DisableBounceIfPinned")))
        {
            DisableBounceAlways = false;
            DisableBounceIfPinned = true;
        }

        if (section.RadioButton(GetSettingLabel("DisableBounceAlways"),
                (!DisableBounceIfPinned && DisableBounceAlways), 0f,
                GetSettingTooltip("DisableBounceAlways")))
        {
            DisableBounceAlways = true;
            DisableBounceIfPinned = false;
        }

        section.Gap(8f);

        if (section.RadioButton(GetSettingLabel("DisableBounceNever"),
                (!DisableBounceIfPinned && !DisableBounceAlways), 0f,
                GetSettingTooltip("DisableBounceNever")))
        {
            DisableBounceAlways = false;
            DisableBounceIfPinned = false;
        }

        section.Gap(20f);

        if (section.RadioButton(GetSettingLabel("DisableFlashIfPinned"),
                (!DisableFlashAlways && DisableFlashIfPinned), 0f,
                GetSettingTooltip("DisableFlashIfPinned")))
        {
            DisableFlashAlways = false;
            DisableFlashIfPinned = true;
        }

        if (section.RadioButton(GetSettingLabel("DisableFlashAlways"),
                (!DisableFlashIfPinned && DisableFlashAlways), 0f,
                GetSettingTooltip("DisableFlashAlways")))
        {
            DisableFlashAlways = true;
            DisableFlashIfPinned = false;
        }

        section.Gap(8f);

        if (section.RadioButton(GetSettingLabel("DisableFlashNever"),
                (!DisableFlashIfPinned && !DisableFlashAlways), 0f,
                GetSettingTooltip("DisableFlashNever")))
        {
            DisableFlashAlways = false;
            DisableFlashIfPinned = false;
        }

        _lastPinningSectionHeight = section.MaxColumnHeightSeen;
        listing.EndSection(section);

        listing.Gap(24f);
    }

    private static float? _lastBaseLettersSectionHeight;

    private static void DoBaseLettersSection(Listing_Standard listing)
    {
        var section = listing.BeginSection(_lastBaseLettersSectionHeight ?? 9999f)!;
        section.SectionHeader("BetterLetters_Settings_Section_BaseLetters");

        section.Label("BetterLetters_Settings_ReplaceLetterIconsInXML".Translate());
        if (section.RadioButton("BetterLetters_Settings_ReplaceLetterIconsInXML_Enabled".Translate(),
                ReplaceLetterIconsInXML, 0f, GetSettingTooltip("ReplaceLetterIconsInXML")))
            ReplaceLetterIconsInXML = true;
        if (section.RadioButton("BetterLetters_Settings_ReplaceLetterIconsInXML_Disabled".Translate(),
                !ReplaceLetterIconsInXML, 0f, GetSettingTooltip("ReplaceLetterIconsInXML")))
            ReplaceLetterIconsInXML = false;
        section.SubLabel("BetterLetters_Settings_RequiresRestart".Translate(), 1f);

        section.GapLine();

        section.Label("BetterLetters_Settings_ModifyLetterText".Translate());
        if (section.RadioButton("BetterLetters_Settings_ModifyLetterText_Enabled".Translate(),
                ModifyLetterText, 0f, GetSettingTooltip("ModifyLetterText")))
            ModifyLetterText = true;
        if (section.RadioButton("BetterLetters_Settings_ModifyLetterText_Disabled".Translate(),
                !ModifyLetterText, 0f, GetSettingTooltip("ModifyLetterText")))
            ModifyLetterText = false;

        section.Gap();

        section.Indent(36f);
        section.CheckboxLabeled(GetSettingLabel("RaidAddDropPod"), ref RaidAddDropPod, !ModifyLetterText,
            GetSettingTooltip("RaidAddDropPod"), labelPct: 0.8f);
        section.CheckboxLabeled(GetSettingLabel("RaidAddRaidStrategy"), ref RaidAddRaidStrategy, !ModifyLetterText,
            GetSettingTooltip("RaidAddRaidStrategy"), labelPct: 0.8f);
        section.CheckboxLabeled(GetSettingLabel("RaidAddPawnCount"), ref RaidAddPawnCount, !ModifyLetterText,
            GetSettingTooltip("RaidAddPawnCount"), labelPct: 0.8f);
        section.CheckboxLabeled(GetSettingLabel("RaidAddGroupCount"), ref RaidAddGroupCount, !ModifyLetterText,
            GetSettingTooltip("RaidAddGroupCount"), labelPct: 0.8f);
        section.Outdent(36f);

        _lastBaseLettersSectionHeight = section.MaxColumnHeightSeen;
        listing.EndSection(section);

        listing.Gap(24f);
    }

    private static string _editBufferMaxNumSnoozes = MaxNumSnoozes.ToString();

    private static float? _lastRemindersSectionHeight;

    private static void DoRemindersSection(Listing_Standard listing)
    {
        var section = listing.BeginSection(_lastRemindersSectionHeight ?? 9999f)!;
        section.SectionHeader("BetterLetters_Settings_Section_Reminders");

        section.CheckboxLabeled(GetSettingLabel("DoCreateReminderPlaySetting"),
            ref DoCreateReminderPlaySetting, GetSettingTooltip("DoCreateReminderPlaySetting"), 32f);

        section.CheckboxLabeled(GetSettingLabel("RemindersPinned"), ref RemindersPinned, null!, 28f);

        section.CheckboxLabeled(GetSettingLabel("RemindersOpen"), ref RemindersOpen, null!, 28f);

        section.CheckboxLabeled(GetSettingLabel("AutoSelectThingForReminders"),
            ref AutoSelectThingForReminders, GetSettingTooltip("AutoSelectThingForReminders"), 32f);

        _lastRemindersSectionHeight = section.MaxColumnHeightSeen;
        listing.EndSection(section);

        listing.Gap(24f);
    }

    private static float? _lastSnoozeSectionHeight;
    private static string _editBufferRecentSnoozesMax = "";
    private static string _editBufferDuratinSimilarityThreshold = "";

    private static void DoSnoozingSection(Listing_Standard listing)
    {
        var section = listing.BeginSection(_lastSnoozeSectionHeight ?? 9999f)!;
        section.SectionHeader("BetterLetters_Settings_Section_Snoozing");

        section.CheckboxLabeled(GetSettingLabel("SnoozePinned"), ref SnoozePinned,
            GetSettingTooltip("SnoozePinned"), 28f, 0.9f);

        section.CheckboxLabeled(GetSettingLabel("SnoozeOpen"), ref SnoozeOpen,
            null!, 28f, 0.9f);

        section.IntSetting(ref MaxNumSnoozes, "MaxNumSnoozes", ref _editBufferMaxNumSnoozes, min: 1, max: 200);

        section.GapLine();

        _editBufferRecentSnoozesMax = RecentSnoozesMax.ToString();
        section.IntSetting(ref RecentSnoozesMax, "RecentSnoozesMax", ref _editBufferRecentSnoozesMax, min: 1, max: 20);

        section.Gap();

        _editBufferDuratinSimilarityThreshold = DurationSimilarityThreshold.ToString();
        section.IntSetting(ref DurationSimilarityThreshold, "DurationSimilarityThreshold",
            ref _editBufferDuratinSimilarityThreshold, min: 1, max: GenDate.TicksPerHour);
        section.Label(DurationSimilarityThreshold.ToStringTicksToPeriodVerbose()!);

        section.Gap();

        if (section.ButtonText(
                "BetterLetters_Settings_ClearRecentSnoozeDurations".Translate(RecentSnoozeDurations.Count)))
        {
            RecentSnoozeDurations.Clear();
        }

        _lastSnoozeSectionHeight = section.MaxColumnHeightSeen;
        listing.EndSection(section);

        listing.Gap(24f);
    }

    private static float? _lastTimeSectionHeight;

    private static void DoTimeSection(Listing_Standard listing)
    {
        var section = listing.BeginSection(_lastTimeSectionHeight ?? 9999f)!;
        section.SectionHeader("BetterLetters_Settings_Section_Time");

        // section.SliderSetting(ref MaxSnoozeDuration, "MaxSnoozeDuration");

        SnoozeTickPeriod = Mathf.RoundToInt(section.SliderLabeled(GetSettingLabel("SnoozeTickPeriod", true, " ticks"),
            SnoozeTickPeriod, GenDate.TicksPerHour / 4f, GenDate.TicksPerHour * 4f, 0.7f,
            GetSettingTooltip("SnoozeTickPeriod")));

        section.Label("BetterLetters_Settings_SnoozeDurationRange".Translate());

        var minSnoozeDurationHours = (float)Math.Max(MinSnoozeDuration, SnoozeTickPeriod) / GenDate.TicksPerHour;
        minSnoozeDurationHours = section.SliderLabeled(
            "BetterLetters_Settings_MinSnoozeDuration".Translate(MinSnoozeDuration.ToStringTicksToPeriod()),
            minSnoozeDurationHours,
            (float)SnoozeTickPeriod / GenDate.TicksPerHour, 24f,
            tooltip: "BetterLetters_Settings_MinSnoozeDuration_Desc".Translate());

        minSnoozeDurationHours = Mathf.Round(minSnoozeDurationHours * 2f) / 2f;
        MinSnoozeDuration = Mathf.RoundToInt(minSnoozeDurationHours * GenDate.TicksPerHour);

        var maxSnoozeDurationHours = (float)MaxSnoozeDuration / GenDate.TicksPerDay;
        maxSnoozeDurationHours = section.SliderLabeled(
            "BetterLetters_Settings_MaxSnoozeDuration".Translate(MaxSnoozeDuration.ToStringTicksToPeriod()),
            maxSnoozeDurationHours,
            (float)MinSnoozeDuration / GenDate.TicksPerHour, 60 * 5,
            tooltip: "BetterLetters_Settings_MaxSnoozeDuration_Desc".Translate());

        maxSnoozeDurationHours = Mathf.Round(maxSnoozeDurationHours * 2f) / 2f;
        MaxSnoozeDuration = Mathf.RoundToInt(maxSnoozeDurationHours * GenDate.TicksPerDay);

        if (section.ButtonText("Default".Translate()))
        {
            SnoozeTickPeriod = (int)DefaultSettings["SnoozeTickPeriod"];
            MinSnoozeDuration = (int)DefaultSettings["MinSnoozeDuration"];
            MaxSnoozeDuration = (int)DefaultSettings["MaxSnoozeDuration"];
        }

        _lastTimeSectionHeight = section.MaxColumnHeightSeen;
        listing.EndSection(section);

        listing.Gap(24f);
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

    private static Vector2 _scrollPositionPatchesTab = Vector2.zero;
    private static float? _lastPatchesTabHeight;

    private static void DoTabPatches(Rect inRect)
    {
        var viewRect = new Rect(inRect);
        var outerRect = new Rect(inRect);
#if !(v1_1 || v1_2 || v1_3 || v1_4)
        Widgets.AdjustRectsForScrollView(inRect, ref outerRect, ref viewRect);
#else
        LegacySupport.AdjustRectsForScrollView(inRect, ref outerRect, ref viewRect);
#endif
        viewRect.height = _lastPatchesTabHeight ?? inRect.height * 1.5f;

        Widgets.BeginScrollView(outerRect, ref _scrollPositionPatchesTab, viewRect);

        var listing = new Listing_Standard();
        var innerRect = viewRect.MiddlePart(0.9f, 1f);
        listing.Begin(innerRect);

        listing.Label("BetterLetters_Settings_Patches_Label".Translate());
        listing.SubLabel("BetterLetters_Settings_Patches_Note".Translate(), 1f);
        listing.Gap(4f);
        GUI.color = ColorLibrary.RedReadable;
        var applyButtonWidthPct = 0.8f;
        TooltipHandler.TipRegionByKey(
            new Rect(innerRect.xMin, listing.CurHeight, innerRect.width * applyButtonWidthPct, 40f),
            "BetterLetters_Settings_RefreshPatches_Desc");
        if (listing.ButtonText("BetterLetters_Settings_RefreshPatches".Translate(), null!, 0.3f))
        {
            BetterLettersMod.Instance?.GetSettings<Settings>()?.Write();
            PatchManager.RepatchAll();
        }

        listing.SubLabel("BetterLetters_Settings_ProbablyRequiresRestart".Translate(), applyButtonWidthPct);
        GUI.color = Color.white;

        listing.GapLine();
        listing.Label("BetterLetters_Settings_Patches_Enabled".Translate());
        foreach (var patch in EnabledPatchCategories)
        {
            DisabledPatchCategories.Remove(patch);
            var enabled = true;
            listing.CheckboxLabeled($"BetterLetters_Settings_PatchCategory_{patch}".Translate(), ref enabled, 80f);
            if (!enabled)
            {
                DisabledPatchCategories.Add(patch);
            }
        }

        listing.GapLine();
        listing.Label("BetterLetters_Settings_Patches_Disabled".Translate());
        foreach (var patch in DisabledPatchCategories)
        {
            EnabledPatchCategories.Remove(patch);
            var enabled = false;
            listing.CheckboxLabeled($"BetterLetters_Settings_PatchCategory_{patch}".Translate(), ref enabled, 80f);
            if (enabled)
            {
                EnabledPatchCategories.Add(patch);
            }
        }

        EnabledPatchCategories.Sort();
        DisabledPatchCategories.Sort();

        _lastPatchesTabHeight = listing.MaxColumnHeightSeen;
        listing.End();
        Widgets.EndScrollView();
    }

    public override void ExposeData()
    {
        string? settingsVersion = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            settingsVersion = BetterLettersMod.ModVersion.Major + "." + BetterLettersMod.ModVersion.Minor;
            Scribe_Values.Look(ref settingsVersion, "SettingsVersion", forceSave: true);
        }
        else if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Scribe_Values.Look(ref settingsVersion, "SettingsVersion", forceSave: true);
        }

        Scribe_Values.Look(ref PinTexture, "PinTexture",
            (PinTextureMode)DefaultSettings[nameof(PinTexture)]);

        Scribe_Values.Look(ref LetterButtonsPosition, "LetterButtonsPosition",
            (ButtonPlacement)DefaultSettings[nameof(LetterButtonsPosition)]);

        Scribe_Values.Look(ref LetterButtonsEnabledTypes, "LetterButtonsEnabledTypes",
            (LetterButtonsType)DefaultSettings[nameof(LetterButtonsEnabledTypes)]);

        Scribe_Values.Look(ref MaxSnoozeDuration, "MaxSnoozeDuration",
            (int)DefaultSettings[nameof(MaxSnoozeDuration)]);

        Scribe_Values.Look(ref ReplaceLetterIconsInLetterStack, "ReplaceLetterIconsInLetterStack",
            (bool)DefaultSettings[nameof(ReplaceLetterIconsInLetterStack)]);

        Scribe_Values.Look(ref ReplaceLetterIconsInXML, "ReplaceLetterIconsInXML",
            (bool)DefaultSettings[nameof(ReplaceLetterIconsInXML)]);

        Scribe_Values.Look(ref ModifyLetterText, "ModifyLetterText",
            (bool)DefaultSettings[nameof(ModifyLetterText)]);

        Scribe_Values.Look(ref RaidAddDropPod, "RaidAddDropPod",
            (bool)DefaultSettings[nameof(RaidAddDropPod)]);

        Scribe_Values.Look(ref RaidAddRaidStrategy, "RaidAddRaidStrategy",
            (bool)DefaultSettings[nameof(RaidAddRaidStrategy)]);

        Scribe_Values.Look(ref RaidAddPawnCount, "RaidAddPawnCount",
            (bool)DefaultSettings[nameof(RaidAddPawnCount)]);

        Scribe_Values.Look(ref RaidAddGroupCount, "RaidAddGroupCount",
            (bool)DefaultSettings[nameof(RaidAddGroupCount)]);

        Scribe_Values.Look(ref AddBulkDismissButton, "AddBulkDismissButton",
            (bool)DefaultSettings[nameof(AddBulkDismissButton)]);

        Scribe_Values.Look(ref MinSnoozeDuration, "MinSnoozeDuration",
            (int)DefaultSettings[nameof(MinSnoozeDuration)]);

        Scribe_Values.Look(ref MaxNumSnoozes, "MaxNumSnoozes",
            (int)DefaultSettings[nameof(MaxNumSnoozes)]);

        Scribe_Values.Look(ref SnoozeTickPeriod, "SnoozeTickPeriod",
            (int)DefaultSettings[nameof(SnoozeTickPeriod)]);

        Scribe_Values.Look(ref DisableRightClickPinnedLetters, "DisableRightClickPinnedLetters",
            (bool)DefaultSettings[nameof(DisableRightClickPinnedLetters)]);

        Scribe_Values.Look(ref DisableBounceIfPinned, "DisableBounceIfPinned",
            (bool)DefaultSettings[nameof(DisableBounceIfPinned)]);

        Scribe_Values.Look(ref DisableBounceAlways, "DisableBounceAlways",
            (bool)DefaultSettings[nameof(DisableBounceAlways)]);

        Scribe_Values.Look(ref DisableFlashIfPinned, "DisableFlashIfPinned",
            (bool)DefaultSettings[nameof(DisableFlashIfPinned)]);

        Scribe_Values.Look(ref DisableFlashAlways, "DisableFlashAlways",
            (bool)DefaultSettings[nameof(DisableFlashAlways)]);

        Scribe_Values.Look(ref SnoozePinned, "SnoozePinned",
            (bool)DefaultSettings[nameof(SnoozePinned)]);

        Scribe_Values.Look(ref SnoozeOpen, "SnoozeOpen",
            (bool)DefaultSettings[nameof(SnoozeOpen)]);

        Scribe_Values.Look(ref RemindersPinned, "RemindersPinned",
            (bool)DefaultSettings[nameof(RemindersPinned)]);

        Scribe_Values.Look(ref RemindersOpen, "RemindersOpen",
            (bool)DefaultSettings[nameof(RemindersOpen)]);

        Scribe_Values.Look(ref DoCreateReminderPlaySetting, "DoCreateReminderPlaySetting",
            (bool)DefaultSettings[nameof(DoCreateReminderPlaySetting)]);

        Scribe_Values.Look(ref AutoSelectThingForReminders, "AutoSelectThingForReminders",
            (bool)DefaultSettings[nameof(AutoSelectThingForReminders)]);

        Scribe_Values.Look(ref AddQuestExpirationSnoozeOptions, "AddQuestExpirationSnoozeOptions",
            (bool)DefaultSettings[nameof(AddQuestExpirationSnoozeOptions)]);


        Scribe_Values.Look(ref QuestExpirationSound, "QuestExpirationSound",
            (QuestExpirationSounds)DefaultSettings[nameof(QuestExpirationSound)]);

        Scribe_Values.Look(ref DismissedQuestsDismissLetters, "DismissedQuestsDismissLetters",
            (bool)DefaultSettings[nameof(DismissedQuestsDismissLetters)]);

        Scribe_Values.Look(ref KeepQuestLettersOnStack, "KeepQuestLettersOnStack",
            (bool)DefaultSettings[nameof(KeepQuestLettersOnStack)]);

        Scribe_Values.Look(ref ChangeExpiredQuestLetters, "ChangeExpiredQuestLetters",
            (bool)DefaultSettings[nameof(ChangeExpiredQuestLetters)]);

        Scribe_Values.Look(ref OffsetLetterLabels, "OffsetLetterLabels",
            (bool)DefaultSettings[nameof(OffsetLetterLabels)]);

        Scribe_Values.Look(ref LetterLabelsOffsetAmount, "LetterLabelsOffsetAmount",
            (float)DefaultSettings[nameof(LetterLabelsOffsetAmount)]);

        Scribe_Values.Look(ref RecentSnoozesMax, "RecentSnoozesMax",
            (int)DefaultSettings[nameof(RecentSnoozesMax)]);

        Scribe_Values.Look(ref DurationSimilarityThreshold, "DurationSimilarityThreshold",
            (int)DefaultSettings[nameof(DurationSimilarityThreshold)]);

        Scribe_Collections.Look(ref RecentSnoozeDurations, "RecentSnoozeDurations", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            RecentSnoozeDurations ??= new List<int>();
        }

        base.ExposeData();
    }
}
