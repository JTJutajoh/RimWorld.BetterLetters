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

namespace BetterLetters.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("HistoryFiltersAndButtons")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_HistoryTab_FilteringButtons
{
    static bool showRemindersFilter = true;
    static bool showSnoozesFilter = true;

    /// Patch that adds buttons to the messages tab to create a new reminder and to filter snoozes and reminders.
    [HarmonyPatch(typeof(MainTabWindow_History), "DoMessagesPage")]
    [HarmonyPrefix]
    [UsedImplicitly]
    static void AddButtonsToMessagesPage(ref Rect rect)
    {
        const float rowOffsetX = 400f;
        var rowRect = new Rect(rect.x + rowOffsetX, rect.y + 10f, rect.width - rowOffsetX - 14f, 30f);

        var label = "BetterLetters_NewReminderButton".Translate();
        var labelSize = Text.CalcSize(label) * 1.4f;
        labelSize.x = Mathf.Min(labelSize.x, 400f);
        labelSize.y = Mathf.Min(labelSize.y, 80f);
        var buttonRect = rowRect.RightPartPixels(labelSize.x);
        if (Widgets.ButtonText(buttonRect, label))
        {
            var reminderDialog = new Dialog_Reminder();
            Find.WindowStack.Add(reminderDialog);
        }

#if !(v1_1 || v1_2 || v1_3) // Only works on RimWorld 1.4+
        // Draw the checkboxes for filtering letters
        var checkboxesRect = rowRect.LeftPartPixels(rowRect.width - labelSize.x);
        
        Widgets.CheckboxLabeled(
            checkboxesRect.LeftHalf(),
            "BetterLetters_ShowSnoozes".Translate(),
            ref showSnoozesFilter,
            false,
            null,
            null,
            true
        );
        Widgets.CheckboxLabeled(
            checkboxesRect.RightHalf(),
            "BetterLetters_ShowReminders".Translate(),
            ref showRemindersFilter,
            false,
            null,
            null,
            true
        );
#endif
    }

#if !(v1_1 || v1_2 || v1_3) // Only works on RimWorld 1.4+
    private static readonly FieldInfo? ShowLettersAnchor =
        typeof(MainTabWindow_History).GetField("showLetters", AccessTools.all);

    private static readonly FieldInfo? ShowMessagesAnchor =
        typeof(MainTabWindow_History).GetField("showMessages", AccessTools.all);

    /// Transpiler that modifies the vanilla condition that checks if an archivable row should be drawn or not
    /// to include the snooze and reminder filters.
    [HarmonyPatch(typeof(MainTabWindow_History), "DoMessagesPage")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    static IEnumerable<CodeInstruction> FilterSnoozesAndReminders(IEnumerable<CodeInstruction> instructions,
        ILGenerator generator)
    {
        var codes = new List<CodeInstruction>(instructions);
        // The label branched to if the conditions determine that DoArchivableRow should be SKIPPED 
        Label? conditionIsTrueLabel = null;
        Label? conditionIsFalseLabel = null;
        for (int i = 0; i < codes.Count; i++)
        {
            // PATCH 1:
            // Add conditions to skip or not skip DoArchivableRow based on snooze/reminder checkboxes
            if (codes[i].LoadsField(ShowLettersAnchor) && i < codes.Count - 1 &&
                codes[i + 1].opcode == OpCodes.Brtrue_S)
            {
                // Save the original start of the condition since we're going to replace it but we need its label
                var conditionStart = codes[i];

                // Search ahead for the end of the LOOP to find the label to skip to (if it's evaluated as false)
                for (int j = i; j < codes.Count; j++)
                {
                    if (codes[j].opcode == OpCodes.Isinst && codes[j].operand is Type type && type == typeof(Letter))
                    {
                        conditionIsFalseLabel = codes[j + 1].operand as Label?;
                        break;
                    }
                }

                // Search ahead for the end of the conditions to find the label they skip to (if it's evaluated as true)
                for (int j = i; j < codes.Count; j++)
                {
                    if (codes[j].LoadsField(ShowMessagesAnchor) && j < codes.Count &&
                        codes[j + 1].opcode == OpCodes.Brtrue_S)
                    {
                        conditionIsTrueLabel = codes[j + 1].operand as Label?;
                        break;
                    }
                }

                if (conditionIsTrueLabel is null || conditionIsFalseLabel is null)
                {
                    LogPrefixed.Error("Failed transpiling DoMessagesPage. Couldn't find IL Labels.");
                    yield return codes[i];
                    continue;
                }

                // Search ahead for the end of the original condition so we can skip past it
                for (int j = i; j < codes.Count; j++)
                {
                    if (codes[j].opcode == OpCodes.Isinst && codes[j].operand is Type type && type == typeof(Message) &&
                        j + 2 < codes.Count && codes[j + 1].opcode == OpCodes.Brtrue_S)
                    {
                        // Found the end of the original condition, jump ahead to skip past it.
                        i = j + 1; // New current IL is: IL_01a9: brtrue.s IL_0224
                        break;
                    }
                }
                
                // Load the List<IArchivable>
                yield return new CodeInstruction(OpCodes.Ldloc_S, 4).MoveLabelsFrom(conditionStart);
                // Load the index of the loop
                yield return new CodeInstruction(OpCodes.Ldloc_S, 8);
                // Load the IArchivable at that index
                yield return new CodeInstruction(OpCodes.Callvirt, typeof(List<IArchivable>).GetMethod("get_Item"));
                // Run our replacer condition
                yield return CodeInstruction.CallClosure<Func<IArchivable, bool>>(archivable =>
                {
                    // If this condition returns TRUE, then the row will be SKIPPED (the reverse of the original C# code)
                    bool showLetters = new Traverse(typeof(MainTabWindow_History)).Field("showLetters").GetValue<bool>();
                    bool showMessages = new Traverse(typeof(MainTabWindow_History)).Field("showMessages").GetValue<bool>();
                    if (archivable is Letter letter)
                    {
                        if (letter.IsSnoozed(true))
                        {
                            return !showSnoozesFilter;
                        }

                        if (letter.IsReminder())
                        {
                            return !showRemindersFilter;
                        }

                        return !showLetters;
                    }
                    
                    if (archivable is ArchivedDialog)
                    {
                        return true;
                    }
                    if (archivable is Message message)
                    {
                        return !showMessages;
                    }
                    return true;
                });
            }

            yield return codes[i];
        }
    }
#endif
}