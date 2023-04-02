using System;
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
    class ChoiceLetterOpenLetter_Patch
    {
        protected static DiaOption Option_Dismiss(ChoiceLetter __instance)
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

        static FieldInfo anchorField = typeof(DiaNode).GetField("options");
        static MethodInfo anchorMethod = typeof(List<DiaOption>).GetMethod("AddRange");

        public static IEnumerable<CodeInstruction> OpenLetter(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                /*if (codes[i].LoadsField(anchorField))
                {
                    // Just executed:
                    // IL_000c: ldfld class [mscorlib]System.Collections.Generic.List`1<class Verse.DiaOption> Verse.DiaNode::options
                    // DiaNode.options is on the stack
                    yield return new CodeInstruction(OpCodes.Dup);                          // Duplicate the reference to DiaNode.options on the stack so we can add to it later
                    yield return new CodeInstruction(OpCodes.Ldarg_0);                      // Load a "this" reference onto the stack
                    yield return CodeInstruction.Call(typeof(ChoiceLetterOpenLetter_Patch), "AddDismissChoice");
                }*/
                if (codes[i].Calls(anchorMethod))
                {
                    // Just executed:
                    // IL_0012: callvirt instance class [mscorlib]System.Collections.Generic.IEnumerable`1<class Verse.DiaOption> Verse.ChoiceLetter::get_Choices()

                    //yield return new CodeInstruction(OpCodes.Dup);                          // Duplicate the reference to Choices on the stack so we can add to it
                    yield return new CodeInstruction(OpCodes.Ldarg_0);                      // Load a "this" reference onto the stack
                    yield return CodeInstruction.Call(typeof(ChoiceLetterOpenLetter_Patch), "AddDismissChoice");

                    // About to execute:
                    // IL_0017: callvirt instance void class [mscorlib]System.Collections.Generic.List`1<class Verse.DiaOption>::AddRange(class [mscorlib]System.Collections.Generic.IEnumerable`1<!0>)
                }
                yield return codes[i];
            }
        }

        static IEnumerable<DiaOption> AddDismissChoice(IEnumerable<DiaOption> options, ChoiceLetter __instance)
        {
            foreach (var cur in options)
                yield return cur;
            yield return Option_Dismiss(__instance);
            //options.Add(Option_Dismiss(__instance));
        }
    }
}
