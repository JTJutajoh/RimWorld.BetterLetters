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

//MAYBE: Replace this entire patch with buttons in the corner of letters instead?
// Rather than adding DiaOptions to letters with float menus, maybe just mirror the buttons added to the Quest tab for letters?
// Could even use literally the same exact buttons with callbacks to make them generic.
// Less clicking, harder to miss, less clutter if a letter has a lot of options already.
// Where would the float menu go though? Would the snooze button always open a float menu? Would it have right click behavior?
// Alternately, could make it an option to choose between them. Potentially even switching which patch is patched and requiring a restart.

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

            var codes = new List<CodeInstruction>(instructions);

            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < codes.Count; i++)
            {
                // Add choices to the dialog
                if (codes[i]!.Calls(AnchorMethodAddRange))
                {
                    // A reference to the Letter.Choices Property is on the stack, about to be passed to AddRange.
                    // This transpiler intercepts it and adds an option to it before it gets sent to AddRange.
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference onto the stack
                    // Modify the list of options
                    yield return CodeInstruction
                        .CallClosure<Func<IEnumerable<DiaOption>, Letter, IEnumerable<DiaOption>>>((options, letter) =>
                            !Settings.DiaOptionButtonsEnabled ? options : options.Prepend(Option_Pin(letter)))!;
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
                optionText = "BetterLetters_CancelReminder".Translate(WorldComponent_SnoozeManager.Snoozes[__instance]?
                    .Duration.ToStringTicksToPeriodVague(vagueMin: false));
            }
            else if (__instance.IsSnoozed())
            {
                optionText = "BetterLetters_CancelSnooze".Translate(WorldComponent_SnoozeManager.Snoozes[__instance]?
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
                    WorldComponent_SnoozeManager.RemoveSnooze(__instance);
                    option.SetText(defaultText);
                    option.clickSound = SoundDefOf.Checkbox_TurnedOn!;
                }
                else
                {
                    // Not pinned or snoozed, show a float menu to pin or snooze it
                    var updateDiaOptionText = (WorldComponent_SnoozeManager.Snooze? snooze) =>
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
