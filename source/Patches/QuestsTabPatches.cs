using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DarkLog;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterLetters.Patches;

public class QuestsTabPatches
{
    private static readonly FieldInfo? SelectedFieldInfo =
        typeof(MainTabWindow_Quests).GetField("selected", AccessTools.all);
    
    private static readonly MethodInfo? DoCharityIconMethodAnchor =
        typeof(MainTabWindow_Quests).GetMethod("DoCharityIcon", AccessTools.all);

    public static IEnumerable<CodeInstruction> DoSelectedQuestInfo(IEnumerable<CodeInstruction> instructions)
    {
        if (DoCharityIconMethodAnchor is null)
        {
            LogPrefixed.Error("Cannot transpile DoSelectedQuestInfo, failed to get DoCharityIcon method");
        }

        var codes = new List<CodeInstruction>(instructions);

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
                yield return CodeInstruction.Call(typeof(QuestsTabPatches), nameof(DoPinButton));

                // PATCH 2:
                // Snooze button
                // Load the inner rect onto the stack
                yield return new CodeInstruction(OpCodes.Ldloc_3);
                // Load the currently selected quest from a field
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, SelectedFieldInfo);
                yield return CodeInstruction.Call(typeof(QuestsTabPatches), nameof(DoSnoozeButton));

                continue;
            }

            yield return codes[i];
        }
    }

    public static void DoPinButton(Rect innerRect, Quest? quest)
    {
        if (quest is null)
        {
            return;
        }
        var rect = new Rect(innerRect.xMax - 64f - 6f, innerRect.y, 32f, 32f);

        var choiceLetter = GetLetterForQuest(quest);
        if (choiceLetter is null)
        {
            LogPrefixed.WarningOnce($"Couldn't find the associated letter for quest '{quest?.name ?? "null"}'",
                quest?.GetHashCode().ToString() ?? "null");
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
                //TODO: Un-dismiss it if it was dismissed
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
        }

        if (Mouse.IsOver(rect))
        {
            var key = pinned ? "BetterLetters_UnPinQuestTooltip" : "BetterLetters_PinQuestTooltip";
            TooltipHandler.TipRegionByKey(rect, key);
        }
    }

    public static void DoSnoozeButton(Rect innerRect, Quest? quest)
    {
        if (quest is null)
        {
            return;
        }
        var rect = new Rect(innerRect.xMax - 96f - 6f, innerRect.y, 32f, 32f);

        var choiceLetter = GetLetterForQuest(quest);
        if (choiceLetter is null)
        {
            LogPrefixed.WarningOnce($"Couldn't find the associated letter for quest '{quest?.name ?? "null"}'",
                quest?.GetHashCode().ToString() ?? "null");
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

    // This helper function will be called at least twice every frame and iterates over potentially hundreds of letters
    // to find a match, so it's important to cache the results.
    private static Dictionary<Quest, ChoiceLetter?> _questLetterCache = new();

    private static ChoiceLetter? GetLetterForQuest(Quest? quest)
    {
        if (quest is null)
        {
            LogPrefixed.Error("Tried to get letter for null quest");
            return null;
        }
        if (_questLetterCache.TryGetValue(quest, out var cachedLetter))
        {
            return cachedLetter;
        }

        foreach (var archivable in Find.Archive.ArchivablesListForReading)
        {
            if (archivable is ChoiceLetter letter)
            {
                if (letter.quest == quest)
                {
                    _questLetterCache[quest] = letter;
                    return letter;
                }
            }
        }

        // Cache null results too so we don't have to search for them again. A quest won't gain a letter if it didn't have one
        _questLetterCache[quest] = null;
        return null;
    }
    
#if !(v1_1 || v1_2 || v1_3 || v1_4)
    private static readonly MethodInfo? DismissButtonClickedMethodAnchor = typeof(Widgets).
        GetMethod(
            name: nameof(Widgets.ButtonImage),
            types: new Type[] { typeof(Rect), typeof(Texture2D), typeof(bool), typeof(string) }
        );
#else
    private static readonly MethodInfo? DismissButtonClickedMethodAnchor = typeof(Widgets).
        GetMethod(
            name: nameof(Widgets.ButtonImage),
            types: new Type[] { typeof(Rect), typeof(Texture2D), typeof(bool) }
        );
#endif
    /// <summary>
    /// Patches the vanilla button to dismiss quests to make it also remove them from the stack
    /// </summary>
    private static IEnumerable<CodeInstruction> DoDismissButton(IEnumerable<CodeInstruction> instructions)
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
                    var letter = GetLetterForQuest(quest);
                    if (letter is null)
                    {
                        LogPrefixed.WarningOnce(
                            $"Couldn't find the associated letter for quest '{quest?.name ?? "null"}'",
                            quest?.GetHashCode().ToString() ?? "null");
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
    public static void DoCharityIcon(ref Rect innerRect)
    {
        innerRect.x -= 40f;
    }
}