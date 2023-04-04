using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Reflection.Emit;
using Verse.Sound;

namespace BetterLetters
{
    [StaticConstructorOnStartup]
    class LetterDrawingPatches
    {
        const float xOffset = 30f;
        private static readonly Texture2D PinTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin");
        private static readonly Texture2D PinOutlineTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin-Outline");
        private static readonly Color PinOutlineColor = new Color(0.75f, 0.65f, 0.65f, 1f);

        // Patch for drawing the pin button
        public static void DrawButtonAt_Postfix(Letter __instance, float topY)
        {
            float width = 38f;
            float xPos = (float)UI.screenWidth - width + 4f;
            Rect pinButtonRect = new Rect(xPos, topY, width, 30f);

            // Draw pin button
            // Code adapted from vanilla MainTabWindow_History.DoArchivableRow
            float pinAlpha = (Find.Archive.IsPinned(__instance) ? 1f : ((!Mouse.IsOver(pinButtonRect)) ? 0f : 0.65f));
            Rect position = new Rect(pinButtonRect.x + (pinButtonRect.width - 22f) / 2f, pinButtonRect.y + (pinButtonRect.height - 22f) / 2f, 22f, 22f).Rounded();
            if (pinAlpha > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, pinAlpha);
                GUI.DrawTexture(position, PinTex);
            }
            else if (Mouse.IsOver(pinButtonRect.ExpandedBy(100f, 4f)))
            {
                GUI.color = PinOutlineColor;
                GUI.DrawTexture(position, PinOutlineTex);
            }


            TooltipHandler.TipRegionByKey(pinButtonRect, "PinTip", 200);
            if (Widgets.ButtonInvisible(pinButtonRect))
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
            }
        }

        // Patch for moving the tooltip hitbox
        public static IEnumerable<CodeInstruction> CheckForMouseOverTextAt(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            bool xAltered = false;
            for (int i = 0; i < codes.Count; i++)
            {
                // Enlarging the rect and shifting to the left
                // Searching for the first time that the literal 38f is loaded onto the stack, so we can load our own value instead
                if (!xAltered && codes[i].LoadsConstant(38f))
                {
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 38f + xOffset);

                    // The literal 38f is loaded again later in the original method for other purposes, so make sure this only runs once
                    xAltered = true;
                    continue;
                }
                yield return codes[i];
            }
        }

        static MethodInfo anchorMethod = typeof(Widgets).GetMethod(nameof(Widgets.ButtonInvisible));
        // Patch for moving the texture, label, and altering right-click behavior
        public static IEnumerable<CodeInstruction> DrawButtonAt_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            bool xAltered = false;
            for (int i = 0; i < codes.Count; i++)
            {
                // Enlarging the rect and shifting to the left
                // Searching for the first time that the literal 38f is loaded onto the stack, so we can load our own value instead
                if (!xAltered && codes[i].LoadsConstant(38f))
                {
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 38f + xOffset);

                    // The literal 38f is loaded again later in the original method for other purposes, so make sure this only runs once
                    xAltered = true; 
                    continue;
                }
                // Shifting the label to the left
                // Searching for the first time that the 12th local variable is stored (num7 in ILspy)
                if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 12)
                {
                    // Before it gets stored, add an x offset to it (The same that was used previously)
                    yield return new CodeInstruction(OpCodes.Ldc_R4, xOffset); // Push the offset onto the stack 
                    yield return new CodeInstruction(OpCodes.Add);
                }

                // Altering right-click behavior
                if (codes[i].Calls(anchorMethod))
                {
                    // IL_0312: ldloc.2
                    // IL_0313: ldc.i4.1
                    // <injecting here>
                    // IL_0314: call bool Verse.Widgets::ButtonInvisible(valuetype[UnityEngine.CoreModule]UnityEngine.Rect, bool)

                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                    yield return new CodeInstruction(OpCodes.Ldloc_1); // Load a reference to "rect" local variable
                    yield return CodeInstruction.Call(typeof(LetterDrawingPatches), nameof(CheckForRightClick));
                }
                yield return codes[i];
            }
        }

        static void CheckForRightClick(Letter __instance, Rect rect)
        {
            if (Find.Archive.IsPinned(__instance) && Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(rect))
            {
                List<FloatMenuOption> floatMenuOptions = new List<FloatMenuOption>();
                // Unpin option is first in the list so it's under the player's mouse after they right click, meaning you can still do the vanilla behavior of spamming right click to remove all letters
                floatMenuOptions.Add(new FloatMenuOption(
                    "Unpin".Translate(),
                    delegate { Find.Archive.Unpin(__instance); }
                    ));
                floatMenuOptions.Add(new FloatMenuOption(
                    "UnpinAndDismiss".Translate(),
                    delegate { 
                        Find.Archive.Unpin(__instance);
                        Find.LetterStack.RemoveLetter(__instance); 
                    }
                    ));
                floatMenuOptions.Add(new FloatMenuOption(
                    "DismissButStayPinned".Translate(),
                    delegate { Find.LetterStack.RemoveLetter(__instance); }
                    ));
                floatMenuOptions.Add(new FloatMenuOption(
                    "OpenLetter".Translate(),
                    delegate { __instance.OpenLetter(); }
                    ));

                Find.WindowStack.Add(new FloatMenu(floatMenuOptions));
                SoundDefOf.FloatMenu_Open.PlayOneShotOnCamera();
                Event.current.Use();
            }
        }
    }
}
