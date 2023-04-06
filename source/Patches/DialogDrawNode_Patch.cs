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
    [StaticConstructorOnStartup]
    class DialogDrawNode_Patch
    {
        private static readonly Texture2D PinTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin");
        private static readonly Texture2D PinOutlineTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin-Outline");
        private static readonly Color PinOutlineColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        // Reference set by OpenLetter patches
        public static Letter curLetter = null;
#if (v1_2 || v1_1)
        static MethodInfo anchorMethod_EndGroup = typeof(GUI).GetMethod(nameof(GUI.EndGroup));
#else
        static MethodInfo anchorMethod_EndGroup = typeof(Widgets).GetMethod(nameof(Widgets.EndGroup));
#endif
        public static IEnumerable<CodeInstruction> DrawNode(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                yield return codes[i];
                if (false && codes[i].Calls(anchorMethod_EndGroup)) //TODO: Probably remove this
                {
                    // Just before the UI group is ended, inject our own code
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return CodeInstruction.Call(typeof(DialogDrawNode_Patch), nameof(DrawPinButton));
                }
            }
        }

        static void DrawPinButton(Rect rect)
        {
            if (curLetter == null) 
                return;


            float size = 32f;

            Widgets.BeginGroup(rect);

            Rect pinButtonRect = new Rect(rect.xMax-size, rect.yMin, size, size);

            // Draw pin button
            // Code adapted from vanilla MainTabWindow_History.DoArchivableRow
            float pinAlpha = (Find.Archive.IsPinned(curLetter) ? 1f : ((!Mouse.IsOver(pinButtonRect)) ? 0f : 0.65f));
            if (pinAlpha > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, pinAlpha);
                GUI.DrawTexture(pinButtonRect, PinTex);
            }
            else
            {
                GUI.color = PinOutlineColor;
                GUI.DrawTexture(pinButtonRect, PinOutlineTex);
            }


            TooltipHandler.TipRegionByKey(pinButtonRect, "PinTipFromDialog", 200);
            if (Widgets.ButtonInvisible(pinButtonRect))
            {
                if (Find.Archive.IsPinned(curLetter))
                {
                    Find.Archive.Unpin(curLetter);
                    SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                }
                else
                {
                    Find.Archive.Pin(curLetter);
                    SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                }
            }
            Widgets.EndGroup();
        }

        static void DoWindowContents(Dialog_NodeTree __instance)
        {
            float size = 32f;
            if (curLetter.IsPinned())
            {
                Rect rect = new Rect((__instance.InitialSize.x - size)-8f, (-size / 2)-12f, size, size);
                Graphics.DrawTexture(rect, PinTex);
            }
        }
    }
}