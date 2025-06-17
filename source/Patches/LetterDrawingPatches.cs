using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DarkLog;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterLetters.Patches
{
    internal class LetterDrawingPatches
    {
        private const float XOffset = 0f;
        private const float PinXOffset = 23.5f;
        private const float PinnedLetterInflateAmount = 4f;

        /// Patch for drawing the pin button itself
        // ReSharper disable InconsistentNaming
        public static void DrawButtonAt_Postfix(Letter __instance, float topY, float ___arrivalTime, LetterDef ___def)
            // ReSharper restore InconsistentNaming
        {
            if (!__instance.IsPinned())
                return;

            const float size = 14f;
            var xPos = (float)UI.screenWidth - size - XOffset;
            var pinButtonRect = new Rect(xPos - PinXOffset, topY - 6f, size, size);

            // Animate the icon moving with the letter, just copied from vanilla code
            var lerp = Time.time - ___arrivalTime;
            if (lerp < 1f)
            {
                pinButtonRect.y -= (1f - lerp) * 200f;
                GUI.color = new Color(1, 1, 1, lerp / 1f);
            }

            // Animate the icon with the letter bounce, again copied from vanilla
            var letterRect = new Rect((float)UI.screenWidth - 38f - 12f, topY, 38f, 30f);
            if (!Settings.DisableBounceIfPinned && !Mouse.IsOver(letterRect) && ___def.bounce && lerp > 15f && lerp % 5f < 1f)
            {
                var num3 = (float)UI.screenWidth * 0.06f;
                var num4 = 2f * (lerp % 1f) - 1f;
                var num5 = num3 * (1f - num4 * num4);
                pinButtonRect.x -= num5;
            }

            var position = pinButtonRect.Rounded();
            GUI.DrawTexture(position, LetterUtils.Icons.PinIconLetterStack);
            GUI.color = Color.white;
        }

        /// Patch for moving the tooltip hitbox
        public static IEnumerable<CodeInstruction> CheckForMouseOverTextAt(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            var xAltered = false;
            for (int i = 0; i < codes.Count; i++)
            {
                // Enlarging the rect and shifting to the left
                // Searching for the first time that the literal 38f is loaded onto the stack, so we can load our own value instead
                if (!xAltered && codes[i].LoadsConstant(38f))
                {
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 38f + XOffset);

                    // The literal 38f is loaded again later in the original method for other purposes, so make sure this only runs once
                    xAltered = true;
                    continue;
                }

                yield return codes[i];
            }
        }

        private static readonly MethodInfo? AnchorMethodButtonInvisible =
            typeof(Widgets).GetMethod(nameof(Widgets.ButtonInvisible));

        private static readonly ConstructorInfo? RectConstructor =
            typeof(Rect).GetConstructor(new[] { typeof(float), typeof(float), typeof(float), typeof(float) });

        /// Patch for moving the texture, label, and altering right-click behavior
        public static IEnumerable<CodeInstruction> DrawButtonAt_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            var xAltered = false;
            var inflated = false;
            for (int i = 0; i < codes.Count; i++)
            {
                // Enlarging the rect and shifting to the left
                // Searching for the first time that the literal 38f is loaded onto the stack, so we can load our own value instead
                if (!xAltered && codes[i].LoadsConstant(38f))
                {
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 38f + XOffset);

                    // The literal 38f is loaded again later in the original method for other purposes, so make sure this only runs once
                    xAltered = true;
                    continue;
                }

                // Shifting the label to the left
                // Searching for the first time that the 12th local variable is stored (num7 in ILspy)
                if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 12)
                {
                    // Before it gets stored, add an x offset to it (The same that was used previously)
                    yield return new CodeInstruction(OpCodes.Ldc_R4, XOffset); // Push the offset onto the stack 
                    yield return new CodeInstruction(OpCodes.Add);
                }

                // Altering the Letter icon Rect
                if (!inflated && codes[i].opcode == OpCodes.Call &&
                    (ConstructorInfo?)codes[i].operand == RectConstructor)
                {
                    inflated = true;
                    yield return codes[i];
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 1);
                    yield return CodeInstruction.Call(typeof(LetterDrawingPatches), nameof(InflateIfPinned));
                    continue;
                }

                // Altering right-click behavior
                if (codes[i].Calls(AnchorMethodButtonInvisible))
                {
                    // IL_0312: ldloc.2
                    // IL_0313: ldc.i4.1
                    // <injecting here>
                    // IL_0314: call bool Verse.Widgets::ButtonInvisible(valuetype[UnityEngine.CoreModule]UnityEngine.Rect, bool)

                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                    yield return new CodeInstruction(OpCodes.Ldloc_1); // Load a reference to "rect" local variable
                    yield return CodeInstruction.Call(typeof(LetterDrawingPatches), nameof(DoPinnedFloatMenu));
                }

                // Disabling bouncing for pinned letters
                if (codes[i].opcode == OpCodes.Ldfld &&
                    (FieldInfo?)codes[i].operand == typeof(LetterDef).GetField("bounce"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                    yield return
                        CodeInstruction.Call(typeof(LetterDrawingPatches),
                            nameof(OverrideBounce)); // Replaces the original LetterDef.bounce getter
                    continue; // Skip over the original getter
                }

                // Disabling flashing for pinned letters
                if (codes[i].opcode == OpCodes.Ldfld &&
                    (FieldInfo?)codes[i].operand == typeof(LetterDef).GetField("flashInterval"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                    yield return
                        CodeInstruction.Call(typeof(LetterDrawingPatches),
                            nameof(OverrideFlash)); //Replaces the original LetterDef.flashInterval getter
                    continue; // Skip over the original getter
                }

                // Emitting the original IL instruction
                yield return codes[i];
            }
        }

        // ReSharper disable InconsistentNaming
        private static void InflateIfPinned(Letter __instance, ref Rect rect)
            // ReSharper restore InconsistentNaming
        {
            if (__instance.IsPinned())
            {
                rect = rect.ExpandedBy(PinnedLetterInflateAmount);
            }
        }

        // ReSharper disable InconsistentNaming
        private static bool
            OverrideBounce(LetterDef ___def, // Takes def as an arg simply to consume it from the stack, easier than other IL weirdness
                Letter __instance) 
        // ReSharper restore InconsistentNaming
        {
            // If pinned, override the bounce field. Otherwise, just return whatever the original field was (or false if DisableBounceAlways is true)
            return
                !Settings.DisableBounceAlways && ___def.bounce && !(Settings.DisableBounceIfPinned && __instance.IsPinned());
        }

        // ReSharper disable InconsistentNaming
        private static float
            OverrideFlash(LetterDef ___def, // Takes def as an arg simply to consume it from the stack, easier than other IL weirdness
                Letter __instance) 
        // ReSharper restore InconsistentNaming
        {
            // If pinned, override with 0 which disables flashing. Otherwise, just return whatever the original field was (or false if DisableFlashAlways is true)
            return
                Settings.DisableFlashAlways || (!Settings.DisableFlashIfPinned || __instance.IsPinned()) ? 0f : ___def.flashInterval;
        }

        // ReSharper disable InconsistentNaming
        private static void DoPinnedFloatMenu(Letter __instance, Rect rect)
        // ReSharper restore InconsistentNaming
        {
            // Checks for right-click first and returns early if not
            if (Event.current.type != EventType.MouseDown || Event.current.button != 1 || !Mouse.IsOver(rect)) return;

            if (Settings.DisableRightClickPinnedLetters)
            {
                __instance.Unpin();
                return;
            }

            if (__instance.IsPinned())
            {
                var floatMenuOptions = new List<FloatMenuOption>();
                // Unpin option is first in the list so it's under the player's mouse after they right click, meaning you can still do the vanilla behavior of spamming right click to remove all letters
                floatMenuOptions.Add(LetterUtils.MakeFloatMenuOption(
                    "BetterLetters_Unpin".Translate(),
                    () => { __instance.Unpin(false); },
                    iconTex: LetterUtils.Icons.DismissIcon,
                    iconColor: Color.white
                ));
                floatMenuOptions.Add(LetterUtils.MakeFloatMenuOption(
                    "BetterLetters_UnpinAndDismiss".Translate(),
                    () => { __instance.Unpin(true); },
                    iconTex: LetterUtils.Icons.DismissIcon,
                    iconColor: Color.red
                ));
                floatMenuOptions.Add(LetterUtils.MakeFloatMenuOption(
                    "BetterLetters_DismissButStayPinned".Translate(),
                    () => { Find.LetterStack.RemoveLetter(__instance); },
                    iconTex: LetterUtils.Icons.DismissIcon,
                    iconColor: Color.gray
                ));
                floatMenuOptions.Add(LetterUtils.Snooze1HrFloatMenuOption(__instance));
                floatMenuOptions.Add(LetterUtils.Snooze1DayFloatMenuOption(__instance));
                floatMenuOptions.Add(LetterUtils.SnoozeDialogFloatMenuOption(__instance));
                
                Find.WindowStack.Add(new FloatMenu(floatMenuOptions));
                SoundDefOf.FloatMenu_Open.PlayOneShotOnCamera();
                Event.current.Use();
            }
            else
            {
                // Right-click functionality for NOT pinned letters would go here in the future    
            }
        }
    }
}