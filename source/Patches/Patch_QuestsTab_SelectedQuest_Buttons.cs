using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;

namespace BetterLetters.Patches;

/// <summary>
/// Modifies the behavior of the Quests tab and quests' associated letters.<br />
/// Primarily adds buttons next to the vanilla Dismiss button<br />
/// Also Changes the behavior of quest letters on the stack when the Dismiss button is clicked.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("QuestsTab_Buttons")]
[HarmonyPatchCondition(unsupportedVersion: RWVersion.v1_0 | RWVersion.v1_1 | RWVersion.v1_2,
    unsupportedString: "Pin/snooze buttons in the quest tab will not be available.")]
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
        if (SelectedFieldInfo == null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(SelectedFieldInfo)} method for {nameof(Patch_QuestsTab_SelectedQuest_Buttons)}.{MethodBase.GetCurrentMethod()} patch");

        if (DoCharityIconMethodAnchor == null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(DoCharityIconMethodAnchor)} method for {nameof(Patch_QuestsTab_SelectedQuest_Buttons)}.{MethodBase.GetCurrentMethod()} patch");

        var codes = new List<CodeInstruction>(instructions);

        // ReSharper disable once ForCanBeConvertedToForeach
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i]!.Calls(DoCharityIconMethodAnchor))
            {
                yield return codes[i]!;

                // Add our buttons after the dismiss button is drawn
                // PATCH 1:
                // Pin button
                // Load the inner rect onto the stack
                yield return new CodeInstruction(OpCodes.Ldloc_3);
                // Load the currently selected quest from a field
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, SelectedFieldInfo);
                yield return CodeInstruction.Call(typeof(Patch_QuestsTab_SelectedQuest_Buttons), nameof(DoPinButton))!;

                // PATCH 2:
                // Snooze button
                // Load the inner rect onto the stack
                yield return new CodeInstruction(OpCodes.Ldloc_3);
                // Load the currently selected quest from a field
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, SelectedFieldInfo);
                yield return CodeInstruction.Call(typeof(Patch_QuestsTab_SelectedQuest_Buttons),
                    nameof(DoSnoozeButton))!;

                continue;
            }

            yield return codes[i]!;
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

        var choiceLetter = quest.GetQuestLetter();
        if (choiceLetter is null)
        {
            Log.WarningOnce($"Couldn't find the associated letter for quest '{quest.name}'",
                quest.GetHashCode().ToString());
            return;
        }

        CustomWidgets.PinIconButton(choiceLetter, rect);
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


        var choiceLetter = quest.GetQuestLetter();
        if (choiceLetter is null)
        {
            Log.WarningOnce($"Couldn't find the associated letter for quest '{quest.name}'",
                quest.GetHashCode().ToString());
            return;
        }

        //BUG: This isn't including quests that start active
        var rect = new Rect(innerRect.xMax - 96f - 6f, innerRect.y, 32f, 32f);
        var extraFloatMenuOptions = new List<FloatMenuOption>();
        if (quest.GetTicksUntilExpiry() > GenDate.TicksPerHour)
        {
            // Snooze until 1 hr before expiration
            extraFloatMenuOptions.Add(new FloatMenuOption(
                "BetterLetters_Quest_SnoozeUntil1HrBeforeExpiration".Translate(),
                () => { choiceLetter.Snooze(quest.GetTicksUntilExpiry() - GenDate.TicksPerHour); }
#if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5)
                , Icons.SnoozeFloatMenu, ColorLibrary.Gold
#endif
            ));
        }

        if (quest.GetTicksUntilExpiry() > GenDate.TicksPerDay)
        {
            // Snooze until 1 day before expiration
            extraFloatMenuOptions.Add(new FloatMenuOption(
                "BetterLetters_Quest_SnoozeUntil1DayBeforeExpiration".Translate(),
                () => { choiceLetter.Snooze(quest.GetTicksUntilExpiry() - GenDate.TicksPerDay); }
#if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5)
                , Icons.SnoozeFloatMenu, ColorLibrary.Gold
#endif
            ));
        }

        CustomWidgets.SnoozeIconButton(choiceLetter, rect, extraFloatMenuOptions);
    }

#if !(v1_1 || v1_2 || v1_3 || v1_4)
    static readonly MethodInfo? DismissButtonClickedMethodAnchor = typeof(Widgets).GetMethod(
        name: nameof(Widgets.ButtonImage),
        types: new[] { typeof(Rect), typeof(Texture2D), typeof(bool), typeof(string) }
    );
#else
    static readonly MethodInfo? DismissButtonClickedMethodAnchor = typeof(Widgets).GetMethod(
        name: nameof(Widgets.ButtonImage),
        types: new Type[] { typeof(Rect), typeof(Texture2D), typeof(bool) }
    );
#endif
    /// <summary>
    /// Patches the vanilla button to dismiss quests to make it also remove them from the stack
    /// </summary>
    [HarmonyPatch(typeof(MainTabWindow_Quests), "DoDismissButton")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    static IEnumerable<CodeInstruction> DoDismissButton(IEnumerable<CodeInstruction> instructions)
    {
        if (DismissButtonClickedMethodAnchor == null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(DismissButtonClickedMethodAnchor)} method for {nameof(Patch_QuestsTab_SelectedQuest_Buttons)}.{MethodBase.GetCurrentMethod()} patch");
        if (SelectedFieldInfo == null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(SelectedFieldInfo)} method for {nameof(Patch_QuestsTab_SelectedQuest_Buttons)}.{MethodBase.GetCurrentMethod()} patch");

        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0; i < codes.Count; i++)
        {
            if (i > 2 && codes[i - 2]!.Calls(DismissButtonClickedMethodAnchor))
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

                    var letter = quest.GetQuestLetter();
                    if (letter is null)
                    {
                        Log.WarningOnce(
                            $"Couldn't find the associated letter for quest '{quest.name}'",
                            quest.GetHashCode().ToString());
                        return;
                    }

                    Find.LetterStack?.RemoveLetter(letter);
                })!;
            }

            yield return codes[i]!;
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
