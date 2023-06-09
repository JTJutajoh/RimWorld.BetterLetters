﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;
using System.Reflection;
using System.Reflection.Emit;

namespace BetterLetters
{
    class OpenLetter_Patch
    {
        static MethodInfo anchorMethod_AddRange = typeof(List<DiaOption>).GetMethod(nameof(List<DiaOption>.AddRange));
        static MethodInfo anchorMethod_AddToStack = typeof(WindowStack).GetMethod(nameof(WindowStack.Add));
        /// <summary>
        /// General-purpose transpiler that can be applied to all vanilla implementations of Letter.OpenLetter
        /// Intercepts the list of choices sent to the dialog and adds a "Dismiss" option to the end of the list
        /// This transpiler should be applicable to any sub-class of Letter as long as it uses the same basic logic for adding options to the dialog.
        /// Specifically, as long as they call AddRange() on a List<DiaOption> (Like vanilla always does), then this transpiler should work.
        /// </summary>
        public static IEnumerable<CodeInstruction> OpenLetter(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(anchorMethod_AddRange))
                {
                    // Just executed:
                    // IL_0012: callvirt instance class [mscorlib]System.Collections.Generic.IEnumerable`1<class Verse.DiaOption> Verse.ChoiceLetter::get_Choices()

                    // A reference to the Letter.Choices Property is on the stack, about to be passed to AddRange.
                    // This transpiler intercepts it and adds an option to it before it gets sent to AddRange.
                    yield return new CodeInstruction(OpCodes.Ldarg_0);                      // Load a "this" reference onto the stack
                    yield return CodeInstruction.Call(typeof(OpenLetter_Patch), nameof(AddChoices));

                    // About to execute:
                    // IL_0017: callvirt instance void class [mscorlib]System.Collections.Generic.List`1<class Verse.DiaOption>::AddRange(class [mscorlib]System.Collections.Generic.IEnumerable`1<!0>)
                }
                if (codes[i].Calls(anchorMethod_AddToStack))
                {
                    // Save a reference to the current letter
                    // Do this last so that the constructor for the dialog (which just ran) can clear the reference
                    yield return new CodeInstruction(OpCodes.Ldarg_0);                      // Load a "this" reference onto the stack
                    yield return CodeInstruction.Call(typeof(OpenLetter_Patch), nameof(SaveLetterReference));
                }
                yield return codes[i];
            }
        }

        static void SaveLetterReference(Letter __instance)
        {
            DialogDrawNode_Patch.curLetter = __instance;
        }

        protected static DiaOption Option_Pin(Letter __instance)
        {
            var option = new DiaOption(__instance.IsPinned() ? "Unpin".Translate() : "Pin".Translate());
            option.clickSound = __instance.IsPinned() ? SoundDefOf.Checkbox_TurnedOff : SoundDefOf.Checkbox_TurnedOn;
            option.action = delegate
            {
                if (__instance.IsPinned())
                {
                    __instance.Unpin();
                    option.SetText("Pin".Translate());
                    option.clickSound = SoundDefOf.Checkbox_TurnedOn;
                }
                else
                {
                    __instance.Pin();
                    option.SetText("Unpin".Translate());
                    option.clickSound = SoundDefOf.Checkbox_TurnedOff;
                }
            };

            return option;
        }

        static IEnumerable<DiaOption> AddChoices(IEnumerable<DiaOption> options, Letter __instance)
        {
            yield return Option_Pin(__instance);
            foreach (var cur in options)
                yield return cur;
        }
    }
}