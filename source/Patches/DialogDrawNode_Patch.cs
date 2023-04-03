using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BetterLetters
{
    class DialogDrawNode_Patch
    {
        private static readonly Texture2D PinTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin");
        private static readonly Texture2D PinOutlineTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin-Outline");
        private static readonly Color PinOutlineColor = new Color(0.25f, 0.25f, 0.25f, 1f);

        static MethodInfo anchorMethod_EndGroup = typeof(Widgets).GetMethod(nameof(Widgets.EndGroup));
        static DiaOption_Pin pinOptionFound = null;
        public static IEnumerable<CodeInstruction> DrawNode(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // Manually prefix the method
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return CodeInstruction.LoadField(typeof(Dialog_NodeTree), "curNode");
            yield return CodeInstruction.Call(typeof(DialogDrawNode_Patch), nameof(FindPinOption));

            for (int i = 0; i < codes.Count; i++)
            {
                
                if (pinOptionFound != null && codes[i].Calls(anchorMethod_EndGroup))
                {
                    // Just before the UI group is ended, inject our own code
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return CodeInstruction.Call(typeof(DialogDrawNode_Patch), nameof(DrawPinButton));
                }
                yield return codes[i];
            }
        }

        // Looks through the list of options on a dialog and if it finds one of type DiaOption_Pin:
        // 1) Removes it
        // 2) Sets the pinOptionFound so that the transpiler knows to draw the pin button
        public static void FindPinOption(DiaNode curNode)
        {
            var options = new List<DiaOption>(curNode.options);
            pinOptionFound = null;
            foreach (var item in options)
            {
                if (item is DiaOption_Pin pinOption)
                {
                    pinOptionFound = pinOption;
                    curNode.options.Remove(item);
                }
            }
        }

        static void DrawPinButton(Rect rect)
        {

        }
    }
}
