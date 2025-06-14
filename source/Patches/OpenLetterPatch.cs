using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;

namespace BetterLetters.Patches
{
    internal class OpenLetterPatch
    {
        private static readonly MethodInfo? AnchorMethodAddRange = typeof(List<DiaOption>).GetMethod(nameof(List<DiaOption>.AddRange));
        private static readonly MethodInfo? AnchorMethodAddToStack = typeof(WindowStack).GetMethod(nameof(WindowStack.Add));
        /// <summary>
        /// General-purpose transpiler that can be applied to all vanilla implementations of Letter.OpenLetter
        /// Intercepts the list of choices sent to the dialog and adds a "Pin" option to the end of the list
        /// This transpiler should be applicable to any sub-class of Letter as long as it uses the same basic logic for adding options to the dialog.
        /// Specifically, as long as they call AddRange() on a List of DiaOption (Like vanilla always does), then this transpiler should work.
        /// </summary>
        public static IEnumerable<CodeInstruction> OpenLetter(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(AnchorMethodAddRange))
                {
                    // Just executed:
                    // IL_0012: callvirt instance class [mscorlib]System.Collections.Generic.IEnumerable`1<class Verse.DiaOption> Verse.ChoiceLetter::get_Choices()

                    // A reference to the Letter.Choices Property is on the stack, about to be passed to AddRange.
                    // This transpiler intercepts it and adds an option to it before it gets sent to AddRange.
                    yield return new CodeInstruction(OpCodes.Ldarg_0);                      // Load a "this" reference onto the stack
                    yield return CodeInstruction.Call(typeof(OpenLetterPatch), nameof(AddChoices));

                    // About to execute:
                    // IL_0017: callvirt instance void class [mscorlib]System.Collections.Generic.List`1<class Verse.DiaOption>::AddRange(class [mscorlib]System.Collections.Generic.IEnumerable`1<!0>)
                }
                if (codes[i].Calls(AnchorMethodAddToStack))
                {
                    // Save a reference to the current letter
                    // Do this last so that the constructor for the dialog (which just ran) can clear the reference
                    yield return new CodeInstruction(OpCodes.Ldarg_0);                      // Load a "this" reference onto the stack
                    yield return CodeInstruction.Call(typeof(OpenLetterPatch), nameof(SaveLetterReference)); // Send the reference to the current letter to the dialog patch
                }
                yield return codes[i];
            }
        }

        // ReSharper disable once InconsistentNaming
        private static void SaveLetterReference(Letter __instance)
        {
            DialogDrawNodePatch.CurrentLetter = __instance;
        }

        /// <summary>
        /// Creates the "Pin" button for the dialog and defines the result when you click it, as well as the
        /// FloatMenu that opens to choose between pin/snooze
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static DiaOption Option_Pin(Letter __instance)
        {
            var defaultText = "BetterLetters_DialogFloatMenuButton".Translate();
            var pinnedText = "BetterLetters_Unpin".Translate();
            
            var option = new DiaOption(__instance.IsPinned() ? pinnedText : defaultText)
                {
                    clickSound = __instance.IsPinned() ? SoundDefOf.Checkbox_TurnedOff : SoundDefOf.Checkbox_TurnedOn
                };
            option.action = delegate
            {
                if (__instance.IsPinned())
                {
                    __instance.Unpin();
                    option.SetText(defaultText);
                    option.clickSound = SoundDefOf.Checkbox_TurnedOn;
                }
                else if (__instance.IsSnoozed())
                {
                    SnoozeManager.RemoveSnooze(__instance);
                    option.SetText(defaultText);
                    option.clickSound = SoundDefOf.Checkbox_TurnedOn;
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
                        option.clickSound = SoundDefOf.Checkbox_TurnedOff;
                    };
                    var floatMenuOptions = new List<FloatMenuOption>
                    {
                        LetterUtils.PinFloatMenuOption(__instance, () =>
                        {
                            option.SetText(pinnedText);
                            option.clickSound = SoundDefOf.Checkbox_TurnedOff;
                        }),
                        LetterUtils.Snooze1HrFloatMenuOption(__instance, updateDiaOptionText),
                        LetterUtils.Snooze1DayFloatMenuOption(__instance, updateDiaOptionText),
                        LetterUtils.SnoozeDialogFloatMenuOption(__instance, updateDiaOptionText)
                    };

                    Find.WindowStack.Add(new FloatMenu(floatMenuOptions));
                    SoundDefOf.FloatMenu_Open.PlayOneShotOnCamera();
                }
            };

            return option;
        }

        /// <summary>
        /// Method that injects the mod's dialog choice(s) before the original list of choices
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static IEnumerable<DiaOption> AddChoices(IEnumerable<DiaOption> options, Letter __instance)
        {
            yield return Option_Pin(__instance);
            foreach (var cur in options)
                yield return cur;
        }
    }
}