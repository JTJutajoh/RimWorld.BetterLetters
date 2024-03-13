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
        const float xOffset = 0f;
        const float pinXOffset = 23.5f;
        const float pinnedLetterInflateAmount = 4f;
        private static readonly Texture2D PinTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin");
        private static readonly Texture2D PinOutlineTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin-Outline");
        private static readonly Color PinOutlineColor = new Color(0.75f, 0.65f, 0.65f, 1f);

        // Patch for drawing the pin button itself
        public static void DrawButtonAt_Postfix(Letter __instance, float topY, float ___arrivalTime, LetterDef ___def)
        {
            if (!__instance.IsPinned())
                return;

            float size = 14f;
            float xPos = (float)UI.screenWidth - size - xOffset;
            Rect pinButtonRect = new Rect(xPos-pinXOffset, topY-6f, size, size);

            // Animate the icon moving with the letter, just copied from vanilla code
            float lerp = Time.time - ___arrivalTime;
            if (lerp < 1f)
            {
                pinButtonRect.y -= (1f - lerp) * 200f;
                GUI.color = new Color(1,1,1,lerp / 1f);
            }
            // Animate the icon with the letter bounce, again copied from vanilla
            Rect letterRect = new Rect((float)UI.screenWidth - 38f - 12f, topY, 38f, 30f);
            if (!Mouse.IsOver(letterRect) && ___def.bounce && lerp > 15f && lerp % 5f < 1f)
            {
                float num3 = (float)UI.screenWidth * 0.06f;
                float num4 = 2f * (lerp % 1f) - 1f;
                float num5 = num3 * (1f - num4 * num4);
                pinButtonRect.x -= num5;
            }

            Rect position = pinButtonRect.Rounded();
            GUI.DrawTexture(position, PinTex);
            GUI.color = Color.white;
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

        static MethodInfo anchorMethod_ButtonInvisible = typeof(Widgets).GetMethod(nameof(Widgets.ButtonInvisible));
        static ConstructorInfo rectConstructor = typeof(Rect).GetConstructor(new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
        // Patch for moving the texture, label, and altering right-click behavior
        public static IEnumerable<CodeInstruction> DrawButtonAt_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            bool xAltered = false;
            bool inflated = false;
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

                // Altering the Letter icon Rect
                if (!inflated && codes[i].opcode == OpCodes.Call && codes[i].operand == rectConstructor)
                {
                    inflated = true;
                    yield return codes[i];
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 1);
                    yield return CodeInstruction.Call(typeof(LetterDrawingPatches), nameof(InflateIfPinned));
                    continue;
                }

                // Altering right-click behavior
                if (codes[i].Calls(anchorMethod_ButtonInvisible))
                {
                    // IL_0312: ldloc.2
                    // IL_0313: ldc.i4.1
                    // <injecting here>
                    // IL_0314: call bool Verse.Widgets::ButtonInvisible(valuetype[UnityEngine.CoreModule]UnityEngine.Rect, bool)

                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                    yield return new CodeInstruction(OpCodes.Ldloc_1); // Load a reference to "rect" local variable
                    yield return CodeInstruction.Call(typeof(LetterDrawingPatches), nameof(DoPinnedFloatMenu));
                }
                yield return codes[i];
            }
        }

        static void InflateIfPinned(Letter __instance, ref Rect rect)
        {
            if (__instance.IsPinned())
            {
                rect = rect.ExpandedBy(pinnedLetterInflateAmount);
            }
        }

        static void DoPinnedFloatMenu(Letter __instance, Rect rect)
        {
            if (__instance.IsPinned() && Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(rect))
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
                //floatMenuOptions.Add(new FloatMenuOption(
                //    "OpenLetter".Translate(),
                //    delegate { __instance.OpenLetter(); }
                //    ));

                Find.WindowStack.Add(new FloatMenu(floatMenuOptions));
                SoundDefOf.FloatMenu_Open.PlayOneShotOnCamera();
                Event.current.Use();
            }
        }
    }
}
