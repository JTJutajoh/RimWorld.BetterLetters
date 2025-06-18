#if !(v1_1 || v1_2 || v1_3) // This patch only works on RimWorld 1.4+
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
/// Large patch that modifies the appearance and behavior of each row in the Message History tab if the letter is
/// snoozed and/or a reminder.<br /><br />
/// Note that this patch only works on RimWorld version 1.4+.
/// I don't know why exactly but I don't care enough to investigate and fix it.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("HistoryArchivableRow")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_HistoryTab_DoArchivableRow
{
    static readonly MethodInfo? ButtonPatchMethodAnchor = typeof(Widgets).GetMethod(nameof(Widgets.ButtonInvisible)) ?? null;
    
    /// <summary>
    /// Transpiler that patches the display of letters in the History tab.
    /// <br />It:<br />
    /// 1. Replaces the pin icon with the snooze icon if the letter is snoozed<br />
    /// 2. Overrides the tooltip for the pin button<br />
    /// 3. Overrides the behavior of the pin button if snoozed<br />
    /// </summary>
    [HarmonyPatch(typeof(MainTabWindow_History), "DoArchivableRow")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    public static IEnumerable<CodeInstruction> DoArchivableRow(IEnumerable<CodeInstruction> instructions)
    {
        if (ButtonPatchMethodAnchor is null)
        {
            LogPrefixed.Error("Cannot transpile DoArchivableRow, failed to get Find.Archive getter");
            foreach (var codeInstruction in instructions) yield return codeInstruction;
            yield break;
        }
        var codes = new List<CodeInstruction>(instructions);

        Label? labelOfEndOfIfBlock = null;
        Label? labelOfEndOfElseBlock = null;
        bool hasPatchedClickBehavior = false;

        for (int i = 0; i < codes.Count; i++)
        {
            // PATCH 1:
            // Replace the pin icon with the snooze icon if the letter is snoozed...
            #region ReplaceIcon
            // Searching for when the Rect for the pin icon is created and stored into local variable 4
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 4) //IL_0124 in 1.6
            {
                // Store the label for the end of the if block we want to skip
                labelOfEndOfIfBlock = codes[i + 3].operand as System.Reflection.Emit.Label?; 
                // Search forward for the IL with the label we just found, so we can find the label of the end of the else block to skip both
                for (int i2 = i; i < codes.Count; i2++)
                {
                    if (labelOfEndOfIfBlock is not null && codes[i2].labels.Contains(labelOfEndOfIfBlock.Value))
                    {
                        // Store the label for the end of the else block we want to skip
                        labelOfEndOfElseBlock = codes[i2 - 1].operand as System.Reflection.Emit.Label?; 
                        break;
                    }
                    // Do NOT emit the IL here, this loop is just for searching.
                }

                // Once we've found the label for the end of the if/else block, inject our code and branch based on its result
                if (labelOfEndOfElseBlock is not null)
                {
                    yield return codes[i];
                    // Load the archivable (the letter) onto the stack
                    yield return new CodeInstruction(OpCodes.Ldarg_2); 
                    // Load the Rect for the pin icon onto the stack
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 4); 
                    
                    yield return CodeInstruction.CallClosure<Func<IArchivable, Rect, bool>>((archivable, rect) =>
                    {
                        if (archivable is not Letter letter || !letter.IsSnoozed()) return false;

                        GUI.color = Color.white;
                        if (Mouse.IsOver(rect))
                        {
                            GUI.color = new Color(1f, 1f, 1f, 0.5f);
                        }

                        GUI.DrawTexture(rect, letter.IsReminder() ? LetterUtils.Icons.ReminderFloatMenu : LetterUtils.Icons.SnoozeFloatMenu);
                        GUI.color = Color.white; // Probably redundant, but just in case

                        return true; // Causes the vanilla pin icon to be skipped
                    }); 
                    // If the function returned true, branch to skip past the vanilla logic for drawing the pin icon
                    yield return new CodeInstruction(OpCodes.Brtrue_S, labelOfEndOfElseBlock.Value);
                    
                    continue; // We already emitted the original IL before our new ones, so skip emitting it again.
                }
                else
                {
                    LogPrefixed.Error("Failed transpiling DoArchivableRow. Couldn't find 2nd IL Label.");
                }
            }
            #endregion ReplaceIcon

            // PATCH 2:
            // Override the tooltip for the pin button
            #region OverrideTooltip
            if (codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "PinArchivableTip") //IL_02AC in 1.6
            {
                // Rect was just loaded onto the stack with ldloc.2 (argument 1 for the patch method)
                
                // Load the archivable (the letter) onto the stack
                yield return new CodeInstruction(OpCodes.Ldarg_2); 
                // Load the original key string ("PinArchivableTip")
                yield return codes[i];
                // Load the NamedArgument passed to the tooltip translation (200)
                yield return codes[++i];
                // Call the replacement method with 4 arguments
                yield return CodeInstruction.CallClosure<Action<Rect, IArchivable, string, int>>((rect, archivable, key, arg) =>
                {
                    if (archivable is not Letter letter || !letter.IsSnoozed())
                    {
                        // Just call the original method with the original arguments
                        TooltipHandler.TipRegionByKey(rect, key, arg);
                    }
                    else
                    {
                        var snooze = SnoozeManager.Snoozes[letter];
                        snooze.DoTipRegion(rect);
                        // var remaining = snooze.RemainingTicks.ToStringTicksToPeriodVerbose();
                        // var end = GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs + snooze.Duration, QuestUtility.GetLocForDates());
                        // TooltipHandler.TipRegionByKey(rect, "BetterLetters_SnoozeArchiveTooltip", end, remaining);
                    }
                });
                
                // Skip over the implicit NamedArgument (we created it in the patch method)
                i++;
                // Skip over the TipByRegionKey call (we called it in the patch method)
                i++;
                
                continue;
            }
            #endregion OverrideTooltip

            // PATCH 3:
            // Override the behavior of the pin button if snoozed
            #region OverrideClickBehavior
            if (i > 4 && !hasPatchedClickBehavior && codes[i - 1].opcode == OpCodes.Brfalse_S && codes[i - 2].Calls(ButtonPatchMethodAnchor)) //IL_02D1 in 1.6
            {
                // Load the archivable (the letter) onto the stack
                yield return new CodeInstruction(OpCodes.Ldarg_2);

                yield return CodeInstruction.CallClosure<Func<IArchivable, bool>>((archivable) =>
                {
                    if (archivable is Letter letter && letter.IsSnoozed())
                    {
                        SnoozeManager.RemoveSnooze(letter);
                        SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                        return true; // Branch over the next ILs
                    }
                    return false; // Don't branch
                });
                // Search ahead for the label we want to branch to if the letter is snoozed when clicked
                for (int j = i; j < codes.Count; j++)
                {
                    if (codes[j].opcode == OpCodes.Br_S && codes[j].operand is Label label)
                    {
                        // Found the label, emit a branch to it
                        yield return new CodeInstruction(OpCodes.Brtrue_S, label);
                        break;
                    }
                }
                
                // Ensure this patch only happens once, since there are multiple calls to ButtonInvisible in the original method
                hasPatchedClickBehavior = true;
            }
            #endregion OverrideClickBehavior

            // Emitting the original IL instruction
            yield return codes[i];
        }
    }
}
#endif