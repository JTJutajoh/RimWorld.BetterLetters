using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse.Sound;

namespace BetterLetters.Patches
{
    /// Patches for all the vanilla Letter.OpenLetter implementations that add option(s) to the dialog for
    /// the mod's functions.
    [HarmonyPatch]
    [HarmonyPatchCategory("Letter_OpenLetter_AddDiaOptions")]
    [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class Patch_Letter_OpenLetter_AddDiaOptions
    {
        /// This patch needs to be applied to all implementations of Letter.OpenLetter<br />
        /// There are currently only two in vanilla
        [UsedImplicitly]
        static IEnumerable<MethodBase> TargetMethods() => new[]
        {
            AccessTools.Method(typeof(ChoiceLetter), nameof(ChoiceLetter.OpenLetter)),
            AccessTools.Method(typeof(DeathLetter), nameof(ChoiceLetter.OpenLetter)),
        };

        // Anchor methods used in the transpiler
        private static readonly MethodInfo? AnchorMethodAddRange =
            typeof(List<DiaOption>).GetMethod(nameof(List<DiaOption>.AddRange));

        private static readonly MethodInfo? AnchorMethodAddToStack =
            typeof(WindowStack).GetMethod(nameof(WindowStack.Add));

        /// General-purpose transpiler that can be applied to all vanilla implementations of Letter.OpenLetter
        /// Intercepts the list of choices sent to the dialog and adds a "Pin" option to the end of the list
        /// This transpiler should be applicable to any subclass of Letter as long as it uses the same basic logic for adding options to the dialog.
        /// Specifically, as long as they call AddRange() on a List of DiaOption (Like vanilla always does), then this transpiler should work.
        [HarmonyTranspiler] // Don't need a [HarmonyPatch] attribute, instead it's handled by TargetMethods()
        [UsedImplicitly]
        static IEnumerable<CodeInstruction> OpenLetter(IEnumerable<CodeInstruction> instructions)
        {
            if (AnchorMethodAddRange == null)
                throw new InvalidOperationException(
                    $"Couldn't find {nameof(AnchorMethodAddRange)} method for {nameof(Patch_Letter_OpenLetter_AddDiaOptions)}.{MethodBase.GetCurrentMethod()} patch");
            if (AnchorMethodAddToStack == null)
                throw new InvalidOperationException(
                    $"Couldn't find {nameof(AnchorMethodAddToStack)} method for {nameof(Patch_Letter_OpenLetter_AddDiaOptions)}.{MethodBase.GetCurrentMethod()} patch");

            var codes = new List<CodeInstruction>(instructions);

            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < codes.Count; i++)
            {
                // PATCH 1:
                // Add choices to the dialog
                if (codes[i]!.Calls(AnchorMethodAddRange))
                {
                    // A reference to the Letter.Choices Property is on the stack, about to be passed to AddRange.
                    // This transpiler intercepts it and adds an option to it before it gets sent to AddRange.
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference onto the stack
                    // Modify the list of options
                    yield return CodeInstruction
                        .CallClosure<Func<IEnumerable<DiaOption>, Letter, IEnumerable<DiaOption>>>((options, letter) =>
                            options.Prepend(Option_Pin(letter)))!;
                }

                //TODO: Extract this to a transpiler in Patch_Dialog_NodeTree_DoWindowContents_AddDialogIcon, which is where it's relevant

                // PATCH 2:
                // Save a reference to the letter this dialog is related to
                if (codes[i]!.Calls(AnchorMethodAddToStack))
                {
                    // Save a reference to the current letter
                    // Do this last so that the constructor for the dialog (which just ran) can clear the reference
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference onto the stack
                    // Send the reference to the current letter to the dialog patch
                    yield return CodeInstruction.CallClosure<Action<Letter>>((letter) =>
                    {
                        Patch_Dialog_NodeTree_DoWindowContents_AddDialogIcon.CurrentLetter = letter;
                    })!;
                }

                yield return codes[i]!;
            }
        }

        /// Creates the "Pin" button for the dialog and defines the result when you click it, as well as the
        /// FloatMenu that opens to choose between pin/snooze
        static DiaOption Option_Pin(Letter __instance)
        {
            var defaultText = "BetterLetters_DialogFloatMenuButton".Translate();
            var pinnedText = "BetterLetters_Unpin".Translate();

            string optionText;
            if (__instance.IsPinned())
            {
                optionText = pinnedText;
            }
            else if (__instance.IsReminder())
            {
                optionText = "BetterLetters_CancelReminder".Translate(SnoozeManager.Snoozes[__instance]?
                    .Duration.ToStringTicksToPeriodVague(vagueMin: false));
            }
            else if (__instance.IsSnoozed())
            {
                optionText = "BetterLetters_CancelSnooze".Translate(SnoozeManager.Snoozes[__instance]?
                    .Duration.ToStringTicksToPeriodVague(vagueMin: false));
            }
            else
            {
                optionText = defaultText;
            }

            var option = new DiaOption(optionText)
            {
                clickSound = __instance.IsPinned() ? SoundDefOf.Checkbox_TurnedOff! : SoundDefOf.Checkbox_TurnedOn!
            };
            option.action = delegate
            {
                if (__instance.IsPinned())
                {
                    __instance.Unpin();
                    option.SetText(defaultText);
                    option.clickSound = SoundDefOf.Checkbox_TurnedOn!;
                }
                else if (__instance.IsSnoozed())
                {
                    SnoozeManager.RemoveSnooze(__instance);
                    option.SetText(defaultText);
                    option.clickSound = SoundDefOf.Checkbox_TurnedOn!;
                }
                else
                {
                    // Not pinned or snoozed, show a float menu to pin or snooze it
                    var updateDiaOptionText = (SnoozeManager.Snooze? snooze) =>
                    {
                        if (snooze == null)
                            option.SetText(pinnedText);
                        else
                            option.SetText("BetterLetters_CancelSnooze".Translate(
                                snooze.Duration.ToStringTicksToPeriodVague(vagueMin: false)));
                        option.clickSound = SoundDefOf.Checkbox_TurnedOff!;
                    };
                    var floatMenuOptions = new List<FloatMenuOption>
                    {
                        LetterUtils.PinFloatMenuOption(__instance, () =>
                        {
                            option.SetText(pinnedText);
                            option.clickSound = SoundDefOf.Checkbox_TurnedOff!;
                        }),
                        LetterUtils.Snooze1HrFloatMenuOption(__instance, updateDiaOptionText),
                        LetterUtils.Snooze1DayFloatMenuOption(__instance, updateDiaOptionText),
                        LetterUtils.SnoozeDialogFloatMenuOption(__instance, updateDiaOptionText)
                    };

                    Find.WindowStack?.Add(new FloatMenu(floatMenuOptions));
                    SoundDefOf.FloatMenu_Open!.PlayOneShotOnCamera();
                }
            };

            return option;
        }
    }
}
