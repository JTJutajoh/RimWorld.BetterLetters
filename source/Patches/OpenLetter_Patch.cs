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
    // Marker class that doesn't do anything on its own but is used to pass the special Pin option. Another patch will cast for this sub class and behave differently if it's found
    class DiaOption_Pin : DiaOption
    {
        public DiaOption_Pin()
        {
            text = "PinArchivableTip".Translate();
        }
    }

    class OpenLetter_Patch
    {
        protected static DiaOption Option_Dismiss(Letter __instance)
        {
            return new DiaOption("Dismiss".Translate())
            {
                action = delegate ()
                {
                    Find.LetterStack.RemoveLetter(__instance);
                },
                resolveTree = true
            };
        }

        protected static DiaOption_Pin Option_Pin(Letter __instance)
        {
            return new DiaOption_Pin()
            {
                action = delegate ()
                {
                    if (Find.Archive.IsPinned(__instance))
                    {
                        Find.Archive.Unpin(__instance);
                        SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                    }
                    else
                    {
                        Find.Archive.Pin(__instance);
                        SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                    }
                },
                resolveTree = false
            };
        }

        static MethodInfo anchorMethod = typeof(List<DiaOption>).GetMethod(nameof(List<DiaOption>.AddRange));
        /// <summary>
        /// General-purpose transpiler that can be applied to all vanilla implementations of Letter.OpenLetter
        /// Intercepts the list of choices sent to the dialog and adds a "Dismiss" option to the end of the list
        /// This transpiler should be applicable to any sub-class of Letter as long as it uses the same basic logic for adding options to the dialog.
        /// Specifically, as long as they call AddRange() on a List<DiaOption> (Like vanilla always does), then this transpiler should work.
        /// </summary>
        public static IEnumerable<CodeInstruction> OpenLetter(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            yield return new CodeInstruction(OpCodes.Ldarg_0);                      // Load a "this" reference onto the stack
            yield return CodeInstruction.Call(typeof(OpenLetter_Patch), nameof(SaveLetterReference));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(anchorMethod))
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
                yield return codes[i];
            }
        }

        static void SaveLetterReference(Letter __instance)
        {
            DialogDrawNode_Patch.curLetter = __instance;
        }

        static IEnumerable<DiaOption> AddChoices(IEnumerable<DiaOption> options, Letter __instance)
        {
            foreach (var cur in options)
                yield return cur;
            foreach (var cur in AddDismissChoice(__instance))
                yield return cur;
        }

        static IEnumerable<DiaOption> AddDismissChoice(Letter __instance)
        {
            if (Find.LetterStack.LettersListForReading.Contains(__instance))
            {
                yield return Option_Dismiss(__instance);
            }
        }
    }
}
