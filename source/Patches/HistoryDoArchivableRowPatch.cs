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

internal class HistoryDoArchivableRowPatch
{
    private static readonly MethodInfo? ButtonPatchMethodAnchor = typeof(Widgets).GetMethod(nameof(Widgets.ButtonInvisible)) ?? null;
    
    /// <summary>
    /// Transpiler that patches the display of letters in the History tab. It does multiple things:<br />
    /// 1. Replaces the pin icon with the snooze icon if the letter is snoozed<br />
    /// 2. Overrides the tooltip for the pin button<br />
    /// 3. Overrides the behavior of the pin button if snoozed<br />
    /// </summary>
    public static IEnumerable<CodeInstruction> DoArchivableRow(IEnumerable<CodeInstruction> instructions)
    {
        if (ButtonPatchMethodAnchor is null)
        {
            LogPrefixed.Error("Cannot transpile DoArchivableRow, failed to get Find.Archive getter");
        }
        var codes = new List<CodeInstruction>(instructions);

        Label? ifBlockEndLabel = null;
        Label? elseBlockEndLabel = null;
        bool patchedClickBehavior = false;

        for (int i = 0; i < codes.Count; i++)
        {
            // PATCH 1:
            // Replace the pin icon with the snooze icon if the letter is snoozed...
            // Searching for when the Rect for the pin icon is created and stored into local variable 4
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 4) //IL_0124 in 1.6
            {
                // Store the label for the end of the if block we want to skip
                ifBlockEndLabel = codes[i + 3].operand as System.Reflection.Emit.Label?; 
                // Search forward for the IL with the label we just found, so we can find the label of the end of the else block to skip both
                for (int i2 = i; i < codes.Count; i2++)
                {
                    if (ifBlockEndLabel is not null && codes[i2].labels.Contains(ifBlockEndLabel.Value))
                    {
                        // Store the label for the end of the else block we want to skip
                        elseBlockEndLabel = codes[i2 - 1].operand as System.Reflection.Emit.Label?; 
                        break;
                    }
                    // Do NOT emit the IL here, this loop is just for searching.
                }

                // Once we've found the label for the end of the if/else block, inject our code and branch based on its result
                if (elseBlockEndLabel is not null)
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

                        GUI.DrawTexture(rect, letter.IsReminder() ? LetterUtils.Icons.ReminderIcon : LetterUtils.Icons.SnoozeFloatMenuIcon);
                        GUI.color = Color.white; // Probably redundant, but just in case

                        return true; // Causes the vanilla pin icon to be skipped
                    }); 
                    // If the function returned true, branch to skip past the vanilla logic for drawing the pin icon
                    yield return new CodeInstruction(OpCodes.Brtrue_S, elseBlockEndLabel.Value);
                    
                    continue; // We already emitted the original IL before our new ones, so skip emitting it again.
                }
                else
                {
                    LogPrefixed.Error("Failed transpiling DoArchivableRow. Couldn't find 2nd IL Label.");
                }
            }

            // PATCH 2:
            // Override the tooltip for the pin button
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

            // PATCH 3:
            // Override the behavior of the pin button if snoozed
            if (i > 4 && !patchedClickBehavior && codes[i - 1].opcode == OpCodes.Brfalse_S && codes[i - 2].Calls(ButtonPatchMethodAnchor)) //IL_02D1 in 1.6
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
                patchedClickBehavior = true;
            }

            // Emitting the original IL instruction
            yield return codes[i];
        }
    }
}