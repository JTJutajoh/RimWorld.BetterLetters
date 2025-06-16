using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DarkLog;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterLetters.Patches;

public static class HistoryRemindersTabPatch
{
    private enum Tab
    {
        Messages,
        Reminders
    }

    private static readonly List<TabRecord> Tabs = new List<TabRecord>()
    {
        new("Messages".Translate(), () => { _curTab = Tab.Messages; }, () => _curTab == Tab.Messages),
        new("BetterLetters_Reminders".Translate(), () => { _curTab = Tab.Reminders; }, () => _curTab == Tab.Reminders)
    };

    private static Tab _curTab = Tab.Messages;
    private static bool showReminders = true;
    private static bool showSnoozes = true;

    private static void DoAdditionalButtons(Rect rect)
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
            // LetterUtils.AddReminder(
            //     label: "Test reminder letter",
            //     text: "This is a test label for custom reminders",
            //     def: LetterDefOf.NeutralEvent,
            //     durationTicks: Mathf.RoundToInt(GenDate.TicksPerHour / 2f)
            // );

            // Can we assign the lookTargets based on what the user has selected when they create it? that would be really useful
            // letter.lookTargets
        }

        // Draw the checkboxes for filtering letters

        var checkboxesRect = rowRect.LeftPartPixels(rowRect.width - labelSize.x);
        
        Widgets.CheckboxLabeled(
            checkboxesRect.LeftHalf(),
            "BetterLetters_ShowSnoozes".Translate(),
            ref showSnoozes,
            false,
            null,
            null,
            true
        );
        Widgets.CheckboxLabeled(
            checkboxesRect.RightHalf(),
            "BetterLetters_ShowReminders".Translate(),
            ref showReminders,
            false,
            null,
            null,
            true
        );
    }

    /// <summary>
    /// Simple prefix patch 
    /// </summary>
    // Resharper disable once InconsistentNaming
    internal static void DoMessagesPage_Prefix(ref Rect rect)
        // Resharper restore InconsistentNaming
    {
        DoAdditionalButtons(rect);
    }

    private static readonly FieldInfo? ShowLettersAnchor =
        typeof(MainTabWindow_History).GetField("showLetters", AccessTools.all);

    private static readonly FieldInfo? ShowMessagesAnchor =
        typeof(MainTabWindow_History).GetField("showMessages", AccessTools.all);

    internal static IEnumerable<CodeInstruction> DoMessagesPage_Transpiler(IEnumerable<CodeInstruction> instructions,
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
                            return !showSnoozes;
                        }

                        if (letter.IsReminder())
                        {
                            return !showReminders;
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

    private static void DoRemindersPage(Rect rect)
    {
        Widgets.Label(rect.MiddlePart(0.5f, 0.5f), "Reminders would go here");
    }
}