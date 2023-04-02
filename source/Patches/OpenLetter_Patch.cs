﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;
using System.Reflection.Emit;

namespace BetterLetters
{
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

        static MethodInfo anchorMethod = typeof(List<DiaOption>).GetMethod(nameof(List<DiaOption>.AddRange));

        public static IEnumerable<CodeInstruction> OpenLetter(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(anchorMethod))
                {
                    // Just executed:
                    // IL_0012: callvirt instance class [mscorlib]System.Collections.Generic.IEnumerable`1<class Verse.DiaOption> Verse.ChoiceLetter::get_Choices()

                    // A reference to the ChoiceLetter.Choices Property is on the stack, about to be passed to AddRange.
                    // This transpiler intercepts it and adds an option to it before it gets sent to AddRange.
                    yield return new CodeInstruction(OpCodes.Ldarg_0);                      // Load a "this" reference onto the stack
                    yield return CodeInstruction.Call(typeof(OpenLetter_Patch), nameof(AddDismissChoice));

                    // About to execute:
                    // IL_0017: callvirt instance void class [mscorlib]System.Collections.Generic.List`1<class Verse.DiaOption>::AddRange(class [mscorlib]System.Collections.Generic.IEnumerable`1<!0>)
                }
                yield return codes[i];
            }
        }

        static IEnumerable<DiaOption> AddDismissChoice(IEnumerable<DiaOption> options, Letter __instance)
        {
            foreach (var cur in options)
                yield return cur;
            yield return Option_Dismiss(__instance);
        }
    }
}