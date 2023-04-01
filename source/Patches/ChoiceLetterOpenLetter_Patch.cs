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

        public static IEnumerable<CodeInstruction> OpenLetter(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(typeof(DiaNode).GetField("options")))
                {
                    DFLog.Debug("Found Field load");
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // arg 0 == this
                    DFLog.Debug("Adding call instruction");
                    yield return CodeInstruction.Call(typeof(ChoiceLetterOpenLetter_Patch), "AddDismissChoice");
                }
                DFLog.Debug($"Emitting code: {codes[i]}");
                yield return codes[i];
            }
        }

        static void AddDismissChoice(List<DiaOption> options, ChoiceLetter __instance)
        {
            options.Add(Option_Dismiss(__instance));
        }
    }
}
