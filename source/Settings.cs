using System;
using System.Collections.Generic;
using BetterLetters.Patches;
using DarkLog;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterLetters;

internal class Settings : ModSettings
{
    private enum SettingsTab
    {
        Main,
        Pinning,
        Snoozing,
        Reminders
    }
    private SettingsTab _currentTab = SettingsTab.Main;
    private const float TabHeight = 32f;
    
    public enum PinTextureMode
    {
        Disabled = 0,
        Round = 1,
        Alt = 2
    }

    public static PinTextureMode PinTexture = PinTextureMode.Round;

    public static bool DisableRightClickPinnedLetters = false;
    public static bool DisableBounceIfPinned = true;
    public static bool DisableBounceAlways = false;
    public static bool DisableFlashIfPinned = true;
    public static bool DisableFlashAlways = false;
    public static int TextureInDialogSize = 56;
    public static int MaxNumSnoozes = 15;
    public static float MaxSnoozeDuration = 60f;
    public static bool SnoozePinned = true;
    public static bool DismissedQuestsDismissLetters = true;
    public static bool KeepQuestLettersOnStack = true;
    public static bool DoCreateReminderPlaySetting = true;

    public void DoWindowContents(Rect inRect)
    {
        var tabs = new List<TabRecord>
        {
            new TabRecord("BetterLetters_Settings_Tab_Main".Translate(), () => _currentTab = SettingsTab.Main, () => _currentTab == SettingsTab.Main),
            new TabRecord("BetterLetters_Settings_Tab_Pinning".Translate(), () => _currentTab = SettingsTab.Pinning, () => _currentTab == SettingsTab.Pinning),
            new TabRecord("BetterLetters_Settings_Tab_Snoozing".Translate(), () => _currentTab = SettingsTab.Snoozing, () => _currentTab == SettingsTab.Snoozing),
            new TabRecord("BetterLetters_Settings_Tab_Reminders".Translate(), () => _currentTab = SettingsTab.Reminders, () => _currentTab == SettingsTab.Reminders),
        };
#if v1_5 || v1_6
        TabDrawer.DrawTabsOverflow(inRect.TopPartPixels(TabHeight), tabs, 80f, 200f);
#elif v1_1 || v1_2 || v1_3 || v1_4
        TabDrawer.DrawTabs(inRect.TopPartPixels(TabHeight), tabs, 200f);
#endif
        Widgets.DrawLineHorizontal(inRect.xMin, inRect.yMin + TabHeight, inRect.width);
        
        var tabRect = inRect.BottomPartPixels(inRect.height - TabHeight - 32f);
        switch (_currentTab)
        {
            case SettingsTab.Main:
                try
                {
                    DoTabMain(tabRect);
                }
                catch (Exception e)
                {
                    LogPrefixed.Exception(e, "Error drawing main settings tab.", true);
                    _currentTab = SettingsTab.Pinning;
                }
                break;
            case SettingsTab.Pinning:
                try
                {
                    DoTabPinning(tabRect);
                }
                catch (Exception e)
                {
                    LogPrefixed.Exception(e, "Error drawing pin settings tab.", true);
                    _currentTab = SettingsTab.Main;
                }
                break;
            case SettingsTab.Reminders:
                try
                {
                    DoTabReminders(tabRect);
                }
                catch (Exception e)
                {
                    LogPrefixed.Exception(e, "Error drawing reminders settings tab.", true);
                    _currentTab = SettingsTab.Main;
                }
                break;
            case SettingsTab.Snoozing:
                try
                {
                    DoTabSnoozing(tabRect);
                }
                catch (Exception e)
                {
                    LogPrefixed.Exception(e, "Error drawing snooze settings tab.", true);
                    _currentTab = SettingsTab.Main;
                }
                break;
            default:
                _currentTab = SettingsTab.Main;
                break;
        }
    }

    private static void DoTabMain(Rect inRect)
    {
        var listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);
        
        listingStandard.CheckboxLabeled("BetterLetters_Settings_DismissedQuestsDismissLetters".Translate(),
            ref DismissedQuestsDismissLetters,
            "BetterLetters_Settings_DismissedQuestsDismissLetters_Desc".Translate());
        
        listingStandard.CheckboxLabeled("BetterLetters_Settings_KeepQuestLettersOnStack".Translate(),
            ref KeepQuestLettersOnStack,
            "BetterLetters_Settings_KeepQuestLettersOnStack_Desc".Translate());
        
        listingStandard.Gap(12f);
        
        listingStandard.CheckboxLabeled("BetterLetters_Settings_DisableBounceAlways".Translate(),
            ref DisableBounceAlways,
            "BetterLetters_Settings_DisableBounceAlways_Desc".Translate());
        if (!DisableBounceAlways)
        {
            listingStandard.CheckboxLabeled("BetterLetters_Settings_DisableBounceIfPinned".Translate(),
                ref DisableBounceIfPinned,
                "BetterLetters_Settings_DisableBounceIfPinned_Desc".Translate());
        }
        
        listingStandard.Gap(4f);
        
        listingStandard.CheckboxLabeled("BetterLetters_Settings_DisableFlashAlways".Translate(),
            ref DisableFlashAlways,
            "BetterLetters_Settings_DisableFlashAlways_Desc".Translate());
        if (!DisableFlashAlways)
        {
            listingStandard.CheckboxLabeled("BetterLetters_Settings_DisableFlashIfPinned".Translate(),
                ref DisableFlashIfPinned,
                "BetterLetters_Settings_DisableFlashIfPinned_Desc".Translate());
        }

        listingStandard.End();
    }

    private static void DoTabPinning(Rect inRect)
    {
        var listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);
        
        listingStandard.CheckboxLabeled("BetterLetters_Settings_DisableRightClickPinnedLetters".Translate(),
            ref DisableRightClickPinnedLetters,
            "BetterLetters_Settings_DisableRightClickPinnedLetters_Desc".Translate());

        listingStandard.Gap(8f);
        
        if (PinTexture != PinTextureMode.Disabled)
        {
            var labelRect = listingStandard.Label(
                "BetterLetters_Settings_TextureInDialogSize".Translate(TextureInDialogSize),
                tooltip: "BetterLetters_Settings_TextureInDialogSize_Desc".Translate(32));
            listingStandard.IntAdjuster(ref TextureInDialogSize, 8, 16);

            Texture2D? pinTex = null;
            if (PinTexture == PinTextureMode.Round)
            {
                pinTex = LetterUtils.Icons.PinRound;
            }
            else if (PinTexture == PinTextureMode.Alt)
            {
                pinTex = LetterUtils.Icons.PinAlt;
            }

            if (pinTex is not null)
            {
                var pinTexRect = new Rect(inRect.xMin + 96f + (TextureInDialogSize / 2f), labelRect.y + 24f,
                    TextureInDialogSize, TextureInDialogSize);
                GUI.DrawTexture(pinTexRect, pinTex);
                listingStandard.Gap(Mathf.Max(12f, TextureInDialogSize - 24f));
            }
        }
        
        listingStandard.Label("BetterLetters_Settings_PinTexture".Translate());
        if (listingStandard.RadioButton("BetterLetters_Settings_PinTexture_Disabled".Translate(),
                PinTexture == PinTextureMode.Disabled))
        {
            PinTexture = PinTextureMode.Disabled;
        }
        else if (listingStandard.RadioButton("BetterLetters_Settings_PinTexture_Round".Translate(),
                     PinTexture == PinTextureMode.Round))
        {
            PinTexture = PinTextureMode.Round;
        }
        else if (listingStandard.RadioButton("BetterLetters_Settings_PinTexture_Alt".Translate(),
                     PinTexture == PinTextureMode.Alt))
        {
            PinTexture = PinTextureMode.Alt;
        }
        
        listingStandard.End();
    }

    private static void DoTabSnoozing(Rect inRect)
    {
        var listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);
        
        MaxNumSnoozes = (int)listingStandard.SliderLabeled(
            "BetterLetters_Settings_MaxNumSnoozes".Translate(MaxNumSnoozes),
            MaxNumSnoozes, 1, 100, 0.5f, "BetterLetters_Settings_MaxNumSnoozes_Desc".Translate(15));

        MaxSnoozeDuration = (float)listingStandard.SliderLabeled(
            "BetterLetters_Settings_MaxSnoozeDuration".Translate(Dialog_Snooze.SliderMaxLabel),
            Mathf.RoundToInt(MaxSnoozeDuration), 1, 300, 0.5f,
            "BetterLetters_Settings_MaxSnoozeDuration_Desc".Translate(0));

        listingStandard.CheckboxLabeled("BetterLetters_Settings_SnoozePinned".Translate(), ref SnoozePinned,
            "BetterLetters_Settings_SnoozePinned_Desc".Translate());

        if (SnoozeManager.Instance is not null)
        {
            // If in-game, show a list of currently snoozed letters here
            DoSnoozesListing(listingStandard);
        }

        listingStandard.End();
    }

    private static void DoTabReminders(Rect inRect)
    {
        var listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);

        listingStandard.CheckboxLabeled("BetterLetters_Settings_DoCreateReminderPlaySetting".Translate(), ref DoCreateReminderPlaySetting, "BetterLetters_Settings_DoCreateReminderPlaySetting_Desc".Translate());

        listingStandard.End();
    }

    /// <summary>
    /// Draws a list of all snoozed letters in an ongoing game along with buttons to unsnooze them.<br />
    /// Meant as a last-resort way for users to clean up snoozes that they can't find through ingame means.
    /// </summary>
    private static void DoSnoozesListing(Listing_Standard listingStandard)
    {
        if (SnoozeManager.Instance == null)
            return;

        listingStandard.Indent();
        listingStandard.GapLine();
        listingStandard.Label(
            "BetterLetters_Settings_CurrentlySnoozed".Translate(SnoozeManager.NumSnoozes, SnoozeManager.MaxNumSnoozes));

        var snoozedLetters = SnoozeManager.Snoozes;
        if (snoozedLetters.Count == 0)
        {
            listingStandard.Label("BetterLetters_Settings_NoSnoozedLetters".Translate());
            return;
        }

        Letter? snoozeToRemove = null; // Doing it this way to avoid modifying the collection mid-loop
        Letter? snoozeToFire = null; // Doing it this way to avoid modifying the collection mid-loop
        foreach (var snooze in snoozedLetters)
        {
            var remainingTime = snooze.Value.RemainingTicks.ToStringTicksToPeriodVerbose();
            var rect = listingStandard.GetRect(30f, 0.9f);
            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.5f));
            var buttonRemoveText = "BetterLetters_Settings_Unsnooze".Translate();
            var buttonFireText = "BetterLetters_Settings_Fire".Translate();
            var buttonsRect = rect.RightPartPixels(Text.CalcSize(buttonRemoveText).x + Text.CalcSize(buttonFireText).x + 64f);
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
            Widgets.Label(rect.LeftPartPixels(rect.width - 64f), label);
        }

        if (snoozeToFire is not null)
        {
            Find.LetterStack.ReceiveLetter(SnoozeManager.Snoozes[snoozeToFire].Letter);
        }
        if (snoozeToRemove is not null)
        {
            SnoozeManager.RemoveSnooze(snoozeToRemove);
        }

#if !(v1_1)
        listingStandard.Outdent();
#endif
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref PinTexture, "PinTexture", PinTextureMode.Round);
        Scribe_Values.Look(ref TextureInDialogSize, "TextureInDialogSize", 56);
        Scribe_Values.Look(ref MaxSnoozeDuration, "MaxSnoozeDuration", 60f);
        Scribe_Values.Look(ref MaxNumSnoozes, "MaxNumSnoozes", 15);
        Scribe_Values.Look(ref DisableRightClickPinnedLetters, "DisableRightClickPinnedLetters", false);
        Scribe_Values.Look(ref DisableBounceIfPinned, "DisableBounceIfPinned", true);
        Scribe_Values.Look(ref DisableBounceAlways, "DisableBounceAlways", false);
        Scribe_Values.Look(ref DisableFlashIfPinned, "DisableFlashIfPinned", true);
        Scribe_Values.Look(ref DisableFlashAlways, "DisableFlashAlways", false);
        Scribe_Values.Look(ref SnoozePinned, "SnoozePinned", true);
        Scribe_Values.Look(ref DismissedQuestsDismissLetters, "DismissedQuestsDismissLetters", true);
        Scribe_Values.Look(ref KeepQuestLettersOnStack, "KeepQuestLettersOnStack", true);
        Scribe_Values.Look(ref DoCreateReminderPlaySetting, "DoCreateReminderPlaySetting", true);

        base.ExposeData();
    }
}