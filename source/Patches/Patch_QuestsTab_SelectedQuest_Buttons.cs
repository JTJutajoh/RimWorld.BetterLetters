#if !(v1_1 || v1_2) // This patch only works on RimWorld 1.3+
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using DarkLog;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterLetters.Patches;

/// <summary>
/// Modifies the behavior of the Quests tab and quests' associated letters.<br />
/// Primarily adds buttons next to the vanilla Dismiss button<br />
/// Also Changes the behavior of quest letters on the stack when the Dismiss button is clicked. 
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("QuestsTab_Buttons")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_QuestsTab_SelectedQuest_Buttons
{
    private static readonly FieldInfo? SelectedFieldInfo =
        typeof(MainTabWindow_Quests).GetField("selected", AccessTools.all);
    private static readonly MethodInfo? DoCharityIconMethodAnchor =
        typeof(MainTabWindow_Quests).GetMethod("DoCharityIcon", AccessTools.all);

    [HarmonyPatch(typeof(MainTabWindow_Quests), "DoSelectedQuestInfo")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    static IEnumerable<CodeInstruction> DoSelectedQuestInfo(IEnumerable<CodeInstruction> instructions)
    {
        if (DoCharityIconMethodAnchor is null)
        {
            LogPrefixed.Error("Cannot transpile DoSelectedQuestInfo, failed to get DoCharityIcon method");
        }

        var codes = new List<CodeInstruction>(instructions);

        // ReSharper disable once ForCanBeConvertedToForeach
        for (int i = 0; i < codes.Count; i++)
        {
            
            if (codes[i].Calls(DoCharityIconMethodAnchor))
            {
                yield return codes[i];

                // Add our buttons after the dismiss button is drawn
                // PATCH 1:
                // Pin button
                // Load the inner rect onto the stack
                yield return new CodeInstruction(OpCodes.Ldloc_3);
                // Load the currently selected quest from a field
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, SelectedFieldInfo);
                yield return CodeInstruction.Call(typeof(Patch_QuestsTab_SelectedQuest_Buttons), nameof(DoPinButton));

                // PATCH 2:
                // Snooze button
                // Load the inner rect onto the stack
                yield return new CodeInstruction(OpCodes.Ldloc_3);
                // Load the currently selected quest from a field
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, SelectedFieldInfo);
                yield return CodeInstruction.Call(typeof(Patch_QuestsTab_SelectedQuest_Buttons), nameof(DoSnoozeButton));

                continue;
            }

            yield return codes[i];
        }
    }

    /// <summary>
    /// Creates a pin button next to the Dismiss button.
    /// Similar to the vanilla DoDismissButton
    /// </summary>
    static void DoPinButton(Rect innerRect, Quest? quest)
    {
        if (quest is null)
        {
            return;
        }
        var rect = new Rect(innerRect.xMax - 64f - 6f, innerRect.y, 32f, 32f);

        var choiceLetter = quest.GetLetter();
        if (choiceLetter is null)
        {
            LogPrefixed.WarningOnce($"Couldn't find the associated letter for quest '{quest.name}'",
                quest.GetHashCode().ToString());
            return;
        }

        var pinned = choiceLetter.IsPinned();
        var tex = pinned ? LetterUtils.Icons.PinIcon : LetterUtils.Icons.PinOutline;
        if (Widgets.ButtonImage(rect, tex))
        {
            if (pinned)
            {
                choiceLetter.Unpin();
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
            else
            {
                choiceLetter.Pin();
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
        }

        if (Mouse.IsOver(rect))
        {
            var key = pinned ? "BetterLetters_UnPinQuestTooltip" : "BetterLetters_PinQuestTooltip";
            TooltipHandler.TipRegionByKey(rect, key);
        }
    }

    /// <summary>
    /// Creates a Snooze button next to the Dismiss button.
    /// Similar to the vanilla DoDismissButton
    /// </summary>
    static void DoSnoozeButton(Rect innerRect, Quest? quest)
    {
        if (quest is null)
        {
            return;
        }
        var rect = new Rect(innerRect.xMax - 96f - 6f, innerRect.y, 32f, 32f);

        var choiceLetter = quest.GetLetter();
        if (choiceLetter is null)
        {
            LogPrefixed.WarningOnce($"Couldn't find the associated letter for quest '{quest.name}'",
                quest.GetHashCode().ToString());
            return;
        }

        var snoozed = choiceLetter.IsSnoozed();
        var tex = snoozed ? LetterUtils.Icons.SnoozeIcon : LetterUtils.Icons.SnoozeOutline;
        if (Widgets.ButtonImage(rect, tex))
        {
            if (snoozed)
            {
                SnoozeManager.RemoveSnooze(choiceLetter);
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                snoozed = false;
            }
            else
            {
                void OnSnooze(SnoozeManager.Snooze? snooze)
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    snoozed = true;
                }

                var floatMenuOptions = new List<FloatMenuOption>
                {
                    LetterUtils.Snooze1HrFloatMenuOption(choiceLetter, OnSnooze),
                    LetterUtils.Snooze1DayFloatMenuOption(choiceLetter, OnSnooze),
                    LetterUtils.SnoozeDialogFloatMenuOption(choiceLetter, OnSnooze)
                };

                Find.WindowStack.Add(new FloatMenu(floatMenuOptions));
                SoundDefOf.FloatMenu_Open.PlayOneShotOnCamera();
            }
        }

        if (Mouse.IsOver(rect))
        {
            if (snoozed)
            {
                var snooze = SnoozeManager.Snoozes[choiceLetter];
                snooze.DoTipRegion(rect);
            }
            else
            {
                TooltipHandler.TipRegionByKey(rect, "BetterLetters_SnoozeQuestTooltip");
            }
        }
    }
    
#if !(v1_1 || v1_2 || v1_3 || v1_4)
    static readonly MethodInfo? DismissButtonClickedMethodAnchor = typeof(Widgets).
        GetMethod(
            name: nameof(Widgets.ButtonImage),
            types: new[] { typeof(Rect), typeof(Texture2D), typeof(bool), typeof(string) }
        );
#else
    static readonly MethodInfo? DismissButtonClickedMethodAnchor = typeof(Widgets).
        GetMethod(
            name: nameof(Widgets.ButtonImage),
            types: new Type[] { typeof(Rect), typeof(Texture2D), typeof(bool) }
        );
#endif
    //TODO: Look into conditionally patching/unpatching this patch instead of just checking settings at runtime
    /// <summary>
    /// Patches the vanilla button to dismiss quests to make it also remove them from the stack
    /// </summary>
    [HarmonyPatch(typeof(MainTabWindow_Quests), "DoDismissButton")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    static IEnumerable<CodeInstruction> DoDismissButton(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0; i < codes.Count; i++)
        {
            if (i > 2 && codes[i - 2].Calls(DismissButtonClickedMethodAnchor))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, SelectedFieldInfo);
                yield return CodeInstruction.CallClosure<Action<Quest?>>((quest) =>
                {
                    if (!Settings.DismissedQuestsDismissLetters) return;
                    if (quest?.dismissed ?? true)
                    {
                        return;
                    }
                    var letter = quest.GetLetter();
                    if (letter is null)
                    {
                        LogPrefixed.WarningOnce(
                            $"Couldn't find the associated letter for quest '{quest.name}'",
                            quest.GetHashCode().ToString());
                        return;
                    }

                    Find.LetterStack.RemoveLetter(letter);
                });
            }

            yield return codes[i];
        }
    }

    /// <summary>
    /// Prefix patch that modifies the position of the charity icon
    /// </summary>
    [HarmonyPatch(typeof(MainTabWindow_Quests), "DoCharityIcon")]
    [HarmonyPrefix]
    [UsedImplicitly]
    static void DoCharityIcon(ref Rect innerRect)
    {
        innerRect.x -= 40f;
    }
}
#endif