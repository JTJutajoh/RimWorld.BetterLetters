using System;
using BetterLetters.Patches;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterLetters;

internal class Settings : ModSettings
{
    public enum PinTextureMode
    {
        Disabled = 0,
        Round = 1,
        Alt = 2
    }

    public static PinTextureMode PinTexture = PinTextureMode.Round;

    public static bool DisableRightClickPinnedLetters = false;
    public static int TextureInDialogSize = 32;
    public static int MaxNumSnoozes = 15;
    public static float MaxSnoozeDuration = 60f;
    public static bool SnoozePinned = true;

    public void DoWindowContents(Rect inRect)
    {
        var listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);

        listingStandard.CheckboxLabeled("BetterLetters_Settings_DisableRightClickPinnedLetters".Translate(),
            ref DisableRightClickPinnedLetters,
            "BetterLetters_Settings_DisableRightClickPinnedLetters_Desc".Translate());
        
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

        if (PinTexture != PinTextureMode.Disabled)
        {
            listingStandard.Indent();

            var labelRect = listingStandard.Label(
                "BetterLetters_Settings_TextureInDialogSize".Translate(TextureInDialogSize),
                tooltip: "BetterLetters_Settings_TextureInDialogSize_Desc".Translate(32));
            listingStandard.IntAdjuster(ref TextureInDialogSize, 8, 16);

            Texture2D? pinTex = null;
            if (PinTexture == PinTextureMode.Round)
            {
                pinTex = DialogDrawNodePatch.PinTex;
            }
            else if (PinTexture == PinTextureMode.Alt)
            {
                pinTex = DialogDrawNodePatch.PinTex_Alt;
            }

            if (pinTex is not null)
            {
                var pinTexRect = new Rect(inRect.xMin + 96f + (TextureInDialogSize / 2f), labelRect.y + 24f,
                    TextureInDialogSize, TextureInDialogSize);
                GUI.DrawTexture(pinTexRect, pinTex);
                listingStandard.Gap(Mathf.Max(12f, TextureInDialogSize - 24f));
            }


            listingStandard.Outdent();
        }

        listingStandard.GapLine();
        listingStandard.Label("BetterLetters_Settings_SnoozeSettings".Translate());
        listingStandard.Gap();
        MaxNumSnoozes = (int)listingStandard.SliderLabeled(
            "BetterLetters_Settings_MaxNumSnoozes".Translate(MaxNumSnoozes),
            MaxNumSnoozes, 1, 100, 0.5f, "BetterLetters_Settings_MaxNumSnoozes_Desc".Translate(15));

        MaxSnoozeDuration = (float)listingStandard.SliderLabeled(
            "BetterLetters_Settings_MaxSnoozeDuration".Translate(MaxSnoozeDuration),
            Mathf.RoundToInt(MaxSnoozeDuration), 1, 240, 0.5f,
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

            Widgets.Label(rect.LeftPartPixels(rect.width - 64f),
                $"{snooze.Key?.Label ?? "null"} ({remainingTime})");
        }

        if (snoozeToFire is not null)
        {
            Find.LetterStack.ReceiveLetter(SnoozeManager.Snoozes[snoozeToFire].Letter);
        }
        if (snoozeToRemove is not null)
        {
            SnoozeManager.RemoveSnooze(snoozeToRemove);
        }

        listingStandard.Outdent();
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref PinTexture, "PinTexture", PinTextureMode.Round);
        Scribe_Values.Look(ref TextureInDialogSize, "TextureInDialogSize", 32);
        Scribe_Values.Look(ref MaxSnoozeDuration, "MaxSnoozeDuration", 60f);
        Scribe_Values.Look(ref MaxNumSnoozes, "MaxNumSnoozes", 15);
        Scribe_Values.Look(ref DisableRightClickPinnedLetters, "DisableRightClickPinnedLetters", false);
        Scribe_Values.Look(ref SnoozePinned, "SnoozePinned", true);

        base.ExposeData();
    }
}