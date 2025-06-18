using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse.Sound;

namespace BetterLetters.Patches
{
    /// <summary>
    /// Patches that modify how letters are drawn in the letter stack if they're pinned.
    /// </summary>
    [HarmonyPatch]
    [HarmonyPatchCategory("Letter_DrawButton_Pinned")]
    [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class Patch_Letter_DrawButton_PinnedBehavior
    {
        const float PinXOffset = 23.5f;

        /// Patch for drawing the pin button itself<br />
        /// A lot of the code is just copied directly from decompiled vanilla code so it's not very readable, but
        /// it does match the behavior of the vanilla animation perfectly.
        [HarmonyPatch(typeof(Letter), "DrawButtonAt")]
        [HarmonyPostfix]
        [UsedImplicitly]
        static void AddPinToLetterIcon(Letter __instance, float topY, float ___arrivalTime, LetterDef ___def)
        {
            if (!__instance.IsPinned())
                return;

            const float size = 14f;
            var xPos = UI.screenWidth - size;
            var pinButtonRect = new Rect(xPos - PinXOffset, topY - 6f, size, size);

            // Animate the icon moving with the letter, just copied from vanilla code
            var lerp = Time.time - ___arrivalTime;
            if (lerp < 1f)
            {
                pinButtonRect.y -= (1f - lerp) * 200f;
                GUI.color = new Color(1, 1, 1, lerp / 1f);
            }

            // Animate the icon with the letter bounce, again copied from vanilla
            var letterRect = new Rect(UI.screenWidth - 38f - 12f, topY, 38f, 30f);
            if (!Settings.DisableBounceIfPinned && !Mouse.IsOver(letterRect) && ___def.bounce && lerp > 15f &&
                lerp % 5f < 1f)
            {
                var num3 = UI.screenWidth * 0.06f;
                var num4 = 2f * (lerp % 1f) - 1f;
                var num5 = num3 * (1f - num4 * num4);
                pinButtonRect.x -= num5;
            }

            var position = pinButtonRect.Rounded();
            GUI.DrawTexture(position, LetterUtils.Icons.PinLetterStack);
            GUI.color = Color.white;
        }


        const float PinnedLetterInflateAmount = 4f;
        
        static readonly MethodInfo? AnchorMethodButtonInvisible =
            typeof(Widgets).GetMethod(nameof(Widgets.ButtonInvisible));

        static readonly ConstructorInfo? RectConstructor =
            typeof(Rect).GetConstructor(new[] { typeof(float), typeof(float), typeof(float), typeof(float) });

        /// Patch for moving the texture, label, and altering right-click behavior
        [HarmonyPatch(typeof(Letter), "DrawButtonAt")]
        [HarmonyTranspiler]
        [UsedImplicitly]
        static IEnumerable<CodeInstruction> DrawButtonAt_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            var hasInflatedLetterIcon = false;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < codes.Count; i++)
            {
                // Altering the Letter icon Rect
                if (!hasInflatedLetterIcon && codes[i].opcode == OpCodes.Call &&
                    (ConstructorInfo?)codes[i].operand == RectConstructor)
                {
                    hasInflatedLetterIcon = true;
                    // Call the first Rect constructor
                    yield return codes[i++];
                    // Skip over the ldloca.s
                    i++;
                    // Load a "this" reference
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    // Call ldloc.1 to get the base Rect that was just constructed
                    yield return codes[i++];
                    // Replace the 2nd Rect constructor with an inflated Rect
                    yield return CodeInstruction.CallClosure<Func<Letter, Rect, Rect>>(
                        (letter, rect) => !letter.IsPinned() ? new Rect(rect) : new Rect(rect.ExpandedBy(PinnedLetterInflateAmount))
                    );
                    // Store the rect in "rect2"
                    yield return new CodeInstruction(OpCodes.Stloc_2);
                    // Skip the next IL (the 2nd Rect constructor)
                    continue;
                }

                // Altering right-click behavior
                if (codes[i].Calls(AnchorMethodButtonInvisible))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                    yield return new CodeInstruction(OpCodes.Ldloc_1); // Load a reference to "rect" local variable)
                    yield return CodeInstruction.Call(typeof(Patch_Letter_DrawButton_PinnedBehavior), nameof(DoPinnedFloatMenu));
                }

                // Disabling bouncing for pinned letters
                if (codes[i].opcode == OpCodes.Ldfld &&
                    (FieldInfo?)codes[i].operand == typeof(LetterDef).GetField("bounce"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                    // Replace the original LetterDef.bounce getter
                    yield return CodeInstruction.CallClosure<Func<LetterDef, Letter, bool>>(OverrideBounce);
                    continue; // Skip over the original getter
                }

                // Disabling flashing for pinned letters
                if (codes[i].opcode == OpCodes.Ldfld &&
                    (FieldInfo?)codes[i].operand == typeof(LetterDef).GetField("flashInterval"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                    // Replace the original LetterDef.flashInterval getter
                    yield return CodeInstruction.CallClosure<Func<LetterDef, Letter, float>>(OverrideFlash);
                    continue; // Skip over the original getter
                }

                // Emitting the original IL instruction
                yield return codes[i];
            }
        }

        static bool OverrideBounce(LetterDef def, Letter letter)
        {
            // If pinned, override the bounce field. Otherwise, return whatever the original field was (or false if DisableBounceAlways is true)
            return
                !Settings.DisableBounceAlways && def.bounce &&
                !(Settings.DisableBounceIfPinned && letter.IsPinned());
        }

        static float OverrideFlash(LetterDef def, Letter letter)
        {
            // If pinned, override with 0, which disables flashing. Otherwise, return whatever the original field was (or false if DisableFlashAlways is true)
            return
                Settings.DisableFlashAlways || (!Settings.DisableFlashIfPinned || letter.IsPinned())
                    ? 0f
                    : def.flashInterval;
        }

        
        /// Generates and shows a float menu for pinned letters.
        static void DoPinnedFloatMenu(Letter letter, Rect rect)
        {
            // Checks for right-click first and returns early if not
            if (Event.current.type != EventType.MouseDown || Event.current.button != 1 || !Mouse.IsOver(rect)) return;

            if (Settings.DisableRightClickPinnedLetters)
            {
                letter.Unpin();
                return;
            }

            if (letter.IsPinned())
            {
                var floatMenuOptions = new List<FloatMenuOption>();
                // Unpin option is first in the list so it's under the player's mouse after they right click, meaning you can still do the vanilla behavior of spamming right click to remove all letters
                floatMenuOptions.Add(LetterUtils.MakeFloatMenuOption(
                    "BetterLetters_Unpin".Translate(),
                    () => { letter.Unpin(); },
                    iconTex: LetterUtils.Icons.Dismiss,
                    iconColor: Color.white
                ));
                floatMenuOptions.Add(LetterUtils.MakeFloatMenuOption(
                    "BetterLetters_UnpinAndDismiss".Translate(),
                    () => { letter.Unpin(true); },
                    iconTex: LetterUtils.Icons.Dismiss,
                    iconColor: Color.red
                ));
                floatMenuOptions.Add(LetterUtils.MakeFloatMenuOption(
                    "BetterLetters_DismissButStayPinned".Translate(),
                    () => { Find.LetterStack.RemoveLetter(letter); },
                    iconTex: LetterUtils.Icons.Dismiss,
                    iconColor: Color.gray
                ));
                floatMenuOptions.Add(LetterUtils.Snooze1HrFloatMenuOption(letter));
                floatMenuOptions.Add(LetterUtils.Snooze1DayFloatMenuOption(letter));
                floatMenuOptions.Add(LetterUtils.SnoozeDialogFloatMenuOption(letter));

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