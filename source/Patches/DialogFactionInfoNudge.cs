using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Reflection.Emit;

namespace BetterLetters
{
    class DialogFactionInfoNudge
    {
        static float offsetAmount = 10f;

        // Patch that slightly nudges the faction display on Dialog_NodeTreeWithFactionInfo windows up a bit to make room for the pin button
        public static IEnumerable<CodeInstruction> DoWindowContents(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Stloc_0)
                {
                    // Right before curY is stored to be sent off, subtract from it
                    yield return new CodeInstruction(OpCodes.Ldc_R4, offsetAmount);
                    yield return new CodeInstruction(OpCodes.Sub);
                }

                yield return codes[i];
            }
        }
    }
}