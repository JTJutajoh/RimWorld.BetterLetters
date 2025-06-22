using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;

namespace BetterLetters.Patches
{
    /// <summary>
    /// This patch intercepts <see cref="ChoiceLetter"/>.<see cref="ChoiceLetter.OpenLetter"/> to save a reference in a static field of this patch class
    /// so that it can be used in the other patches in <see cref="Patch_Dialog_NodeTree_DoWindowContents_AddPinSnoozeButtons"/> to determine how to draw the icons
    /// </summary>
    [HarmonyPatch]
    [HarmonyPatchCategory("Dialog_AddIcons")]
    [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [UsedImplicitly]
    internal static class Patch_Letter_OpenLetter_SaveReference
    {
        /// This patch needs to be applied to all implementations of Letter.OpenLetter<br />
        /// There are currently only two in vanilla<para/>
        /// If more ever get added or if modded letters add their own types of letter subclasses that need to be patched
        /// as well, add them here.
        [UsedImplicitly]
        static IEnumerable<MethodBase> TargetMethods() => new[]
        {
            AccessTools.Method(typeof(ChoiceLetter), nameof(ChoiceLetter.OpenLetter)),
            AccessTools.Method(typeof(DeathLetter), nameof(ChoiceLetter.OpenLetter)),

            // ADD ANY ADDITIONAL LETTER TYPES THAT GET ADDED BY MODS OR UPDATES HERE
        };

        private static readonly MethodInfo? AnchorMethodAddToStack =
            typeof(WindowStack).GetMethod(nameof(WindowStack.Add));

        /// <summary>
        /// Whenever a letter is opened, right <i>after</i> the dialog is created for it, cache a reference to the letter
        /// so that <see cref="Patch_Dialog_NodeTree_DoWindowContents_AddPinSnoozeButtons"/> can use it.
        /// </summary>
        [HarmonyTranspiler]
        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> SaveReferenceToOpenedLetter(
            IEnumerable<CodeInstruction> instructions)
        {
            if (AnchorMethodAddToStack == null)
                throw new InvalidOperationException(
                    $"Couldn't find {nameof(AnchorMethodAddToStack)} method for {nameof(Patch_Letter_OpenLetter_SaveReference)}.{MethodBase.GetCurrentMethod()} patch");
            var codes = new List<CodeInstruction>(instructions);

            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < codes.Count; i++)
            {
                // Save a reference to the letter this dialog is related to
                if (codes[i]!.Calls(AnchorMethodAddToStack))
                {
                    // Save a reference to the current letter
                    // Do this last so that the constructor for the dialog (which just ran) can clear the reference
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference onto the stack
                    // Send the reference to the current letter to the dialog patch
                    yield return CodeInstruction.CallClosure<Action<Letter>>((letter) =>
                    {
                        Patch_Dialog_NodeTree_DoWindowContents_AddPinSnoozeButtons.CurrentLetter = letter;
                    })!;
                }


                yield return codes[i]!;
            }
        }
    }

    /// Patch to add icons to the letter dialog based on if the dialog is linked to a letter that is
    /// pinned/snoozed/a reminder/etc.<br />
    /// Also handles changing the size of the letter dialog to make room for the buttons, based on which corner is set
    /// in the mod settings.
    [HarmonyPatch]
    [HarmonyPatchCategory("Dialog_AddIcons")]
    [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [UsedImplicitly]
    internal static class Patch_Dialog_NodeTree_DoWindowContents_AddPinSnoozeButtons
    {
        /// <summary>
        /// Reference set and cleared by <see cref="Patch_Letter_OpenLetter_SaveReference"/> whenever a dialog is opened
        /// </summary>
        internal static Letter? CurrentLetter;


        /// <summary>
        /// When a new letter dialog is created/opened, clear the reference we keep to the letter from any previous dialogs
        /// This happens before the reference is set in the main transpiler patch.<para />
        /// Prevents the main patch from running on dialogs that shouldn't get an icon
        /// </summary>
        [HarmonyPatch(typeof(Dialog_NodeTree), MethodType.Constructor, typeof(DiaNode), typeof(bool), typeof(bool),
            typeof(string))]
        [HarmonyPostfix]
        [UsedImplicitly]
        static void ConstructorPostfix()
        {
            CurrentLetter = null;
        }


        // Pre-calculate the rect of the buttons based on the window size and the placement set in settings.

        #region Buttons rect calculations

        /// Directly from Verse.Window.Margin property
        private const float WindowMargin = 18f;

        /// Square size of buttons
        internal const float ButtonSize = 24f;

        private const int NumButtons = 3;
        private const float ButtonsPadding = 0f;
        private const float ButtonsSpacing = 6f;

        private const float ButtonsWidth =
            (ButtonsPadding * 2) + (NumButtons * ButtonSize) + ((NumButtons - 1) * ButtonsSpacing);

        private const float ButtonsHeight = ButtonSize + ButtonsPadding * 2;

        /// If the BottomRight mode is set, shift the dialog's DiaOptions up by this amount to make room for them<br/>
        /// The entire height of the buttons rect isn't needed, so just picked a magic value.
        private const float BottomRightDiaOptionsYShift = ButtonsHeight * 0.75f;

        /// <summary>
        /// The calculated size and position of the icon buttons.<br/>
        /// Set by the <see cref="ExpandWindowInitialSizeVertically"/> patch after it has modified the
        /// <see cref="Dialog_NodeTree.InitialSize"/> of the dialog.<para/>
        /// Dynamically changes based on the <see cref="Settings.LetterButtonsPosition"/> setting.
        /// </summary>
        private static Rect _buttonsRect;

        private static Vector2 _windowSize;

        /// <summary>
        /// Once the window's initial size has been modified by <see cref="ExpandWindowInitialSizeVertically"/>,
        /// set the values of <see cref="_buttonsRect"/> based on the <see cref="Settings.LetterButtonsPosition"/> setting.
        /// </summary>
        /// <param name="initialSize"></param>
        internal static void CalcButtonPositions()
        {
            // Calculate the actual rect inside the window that we can draw within.
            // The widgets will be drawn inside a Group, so (0,0) is the top left of the WINDOW, not the screen.
            var innerRect = new Rect(0, 0, _windowSize.x, _windowSize.y).ContractedBy(WindowMargin).AtZero();

            var buttonsPos = Settings.LetterButtonsPosition switch
            {
                Settings.ButtonPlacement.TopLeft =>
                    new Vector2(innerRect.xMin, innerRect.yMin),
                Settings.ButtonPlacement.TopMiddle =>
                    new Vector2(innerRect.xMin + (innerRect.width - ButtonsWidth) / 2f, innerRect.yMin),
                Settings.ButtonPlacement.TopRight =>
                    new Vector2(innerRect.width - ButtonsWidth, innerRect.yMin),
                Settings.ButtonPlacement.BottomLeft =>
                    new Vector2(innerRect.xMin, innerRect.height - ButtonsHeight),
                Settings.ButtonPlacement.BottomMiddle =>
                    new Vector2(innerRect.xMin + (innerRect.width - ButtonsWidth) / 2f,
                        innerRect.height - ButtonsHeight),
                Settings.ButtonPlacement.BottomRight =>
                    new Vector2(innerRect.width - ButtonsWidth, innerRect.height - ButtonsHeight),

                _ => new Vector2(innerRect.xMin, innerRect.yMin)
            };

            _buttonsRect = new Rect(buttonsPos.x, buttonsPos.y, ButtonsWidth, ButtonsHeight);
        }

        #endregion Buttons rect calculations


        #region Modify dialog window

        /// <summary>
        /// This patch expands the height of the dialog to make it taller if extra room is needed for the buttons in the corners <br/>
        /// The exact amount that it gets expanded by depends on the <see cref="Settings.LetterButtonsPosition"/> setting
        /// since each corner of the dialog has different amounts of real estate available in vanilla.<para/>
        /// After expanding the height, <b>also calculates the position of the buttons</b> using the new size that was just calculated
        /// saving the value to <see cref="_buttonsRect"/>
        /// </summary>
        [HarmonyPatch(typeof(Dialog_NodeTree), nameof(Dialog_NodeTree.InitialSize), MethodType.Getter)]
        [HarmonyPostfix]
        [UsedImplicitly]
        static void ExpandWindowInitialSizeVertically(ref Vector2 __result)
        {
            if (CurrentLetter is null || !Settings.IconButtonsEnabled) return;

            switch (Settings.LetterButtonsPosition)
            {
                // All the Top modes use the same value since there's no extra space at the top of the letter in vanilla
                case Settings.ButtonPlacement.TopLeft:
                case Settings.ButtonPlacement.TopMiddle:
                case Settings.ButtonPlacement.TopRight:
                    __result.y = Mathf.Min(UI.screenHeight, __result.y + ButtonsHeight);
                    break;

                // Expand it vertically, but a little less, for bottom right.
                // Just enough so the DiaOptions button rects don't overlap the icons:
                case Settings.ButtonPlacement.BottomRight:
                    __result.y = Mathf.Min(UI.screenHeight, __result.y + BottomRightDiaOptionsYShift);
                    break;

                // Don't expand vertically at all for these options because other patches will shift things inside the
                // window instead:
                case Settings.ButtonPlacement.BottomLeft:
                case Settings.ButtonPlacement.BottomMiddle:
                default:
                    break;
            }

            _windowSize = __result;

            CalcButtonPositions();
        }


        /// Shift original window contents up/down by the same amount we expanded the window vertically
        [HarmonyPatch(typeof(Dialog_NodeTree), nameof(Dialog_NodeTree.DoWindowContents))]
        [HarmonyTranspiler]
        [UsedImplicitly]
        static IEnumerable<CodeInstruction> ShiftWindowContentsVertically(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < codes.Count; i++)
            {
                // Before the rect gets stored after AtZero() finishes, modify it
                if (codes[i]!.opcode == OpCodes.Stloc_0)
                {
                    yield return CodeInstruction.CallClosure<Func<Rect, Rect>>((rect) =>
                    {
                        if (CurrentLetter is null || !Settings.IconButtonsEnabled) return rect;
                        switch (Settings.LetterButtonsPosition)
                        {
                            // All the Top modes need the same amount of extra space:
                            case Settings.ButtonPlacement.TopLeft:
                            case Settings.ButtonPlacement.TopMiddle:
                            case Settings.ButtonPlacement.TopRight:
                                return rect with
                                {
                                    y = rect.yMin + ButtonsHeight, height = rect.height - ButtonsHeight
                                };

                            // Bottom middle only needs the bottom of the vanilla rect to be moved higher
                            case Settings.ButtonPlacement.BottomMiddle:
                                return rect with { height = rect.height - ButtonsHeight };

                            // Don't modify the placement of vanilla elements for these modes:
                            case Settings.ButtonPlacement.BottomLeft: // Only DiaOptions need to be shifted up
                            case Settings.ButtonPlacement.BottomRight
                                : // DiaOptions and FactionInfo will be shifted up separately
                            default:
                                return rect;
                        }
                    })!;
                }

                yield return codes[i]!;
            }
        }


        static readonly MethodInfo? DrawRelatedFactionInfoAnchorMethod = AccessTools.Method(typeof(FactionUIUtility),
            nameof(FactionUIUtility.DrawRelatedFactionInfo));

        /// <summary>
        /// <b>Patch exclusively for the <see cref="Settings.ButtonPlacement.BottomRight"/> placement mode.</b><para/>
        /// Shifts the <b>faction info</b> that appears in the bottom right of some letters.<para/>
        /// This leaves the rest of the letter unchanged visually.
        /// </summary>
        [HarmonyPatch(typeof(Dialog_NodeTreeWithFactionInfo), nameof(Dialog_NodeTreeWithFactionInfo.DoWindowContents))]
        [HarmonyTranspiler]
        [UsedImplicitly]
        static IEnumerable<CodeInstruction> ShiftFactionInfoVertically(IEnumerable<CodeInstruction> instructions)
        {
            if (DrawRelatedFactionInfoAnchorMethod == null)
                throw new InvalidOperationException(
                    $"Couldn't find {nameof(DrawRelatedFactionInfoAnchorMethod)} method for {nameof(Patch_Dialog_NodeTree_DoWindowContents_AddPinSnoozeButtons)}.{MethodBase.GetCurrentMethod()} patch");

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                // PATCH 1:
                // Conditionally adjust curY before it is sent to FactionUIUtility.DrawRelatedFactionInfo
                if (codes[i]!.opcode == OpCodes.Stloc_0)
                {
                    // curY is about to be stored in a local variable, intercept it
                    yield return CodeInstruction.CallClosure<Func<float>>(() =>
                    {
                        // Return value will be subtracted from the height rect used to place the faction info
                        if (CurrentLetter is null || !Settings.IconButtonsEnabled) return 0f;

                        return Settings.LetterButtonsPosition == Settings.ButtonPlacement.BottomRight
                            ? ButtonsHeight
                            : 0f;
                    })!;
                    yield return new CodeInstruction(OpCodes.Sub);
                }

                // PATCH 2:
                // Conditionally adjust inRect before it is sent to FactionUIUtility.DrawRelatedFactionInfo
                // This is technically REDUNDANT, the function only uses curY and ignores the y component of inRect.
                // Included anyway just in case that ever changes.
                if (i + 3 < codes.Count && codes[i + 3]!.Calls(DrawRelatedFactionInfoAnchorMethod))
                {
                    // inRect was just loaded onto the stack before being passed to DrawRelatedFactionInfo
                    yield return CodeInstruction.CallClosure<Func<Rect, Rect>>((inRect) =>
                    {
                        if (CurrentLetter is null || !Settings.IconButtonsEnabled) return inRect;
                        if (Settings.LetterButtonsPosition != Settings.ButtonPlacement.BottomRight) return inRect;

                        inRect.height -= ButtonsHeight;
                        return inRect;
                    })!;
                }

                yield return codes[i]!;
            }
        }


        static readonly MethodInfo? EndScrollViewMethodAnchor =
            AccessTools.Method(typeof(Widgets), nameof(Widgets.EndScrollView));

        /// <summary>
        /// <b>Used primarily for the <see cref="Settings.ButtonPlacement.BottomLeft"/> placement mode.</b><br/>
        /// However, also used by <see cref="Settings.ButtonPlacement.BottomRight"/> since otherwise the bottommost DiaOption
        /// overlaps slightly with the added buttons, since the DiaOption's hit rect extends pretty much all the way to the right
        /// edge of the window.<para/>
        /// Move the vanilla DiaOptions and shrink their rect in the window upward so that they don't overlap with the
        /// icon buttons.<para/>
        /// </summary>
        [HarmonyPatch(typeof(Dialog_NodeTree), "DrawNode")]
        [HarmonyTranspiler]
        [UsedImplicitly]
        static IEnumerable<CodeInstruction> ShiftDiaOptionsVertically(IEnumerable<CodeInstruction> instructions)
        {
            if (EndScrollViewMethodAnchor == null)
                throw new InvalidOperationException(
                    $"Couldn't find {nameof(EndScrollViewMethodAnchor)} method for {nameof(Patch_Dialog_NodeTree_DoWindowContents_AddPinSnoozeButtons)}.{MethodBase.GetCurrentMethod()} patch");

            var codes = new List<CodeInstruction>(instructions);

            var hasPatchedHeight = false;

            for (int i = 0; i < codes.Count; i++)
            {
                // PATCH 1:
                // Move the rect used to draw the DiaOptions up
                // Intercept the literal 100f that vanilla uses in the rect calculation and modify it based on
                // the placement mode.

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (codes[i]!.opcode == OpCodes.Ldc_R4 && (float)codes[i]!.operand == 100f)
                {
                    // The literal 100f was just loaded onto the stack to be subtracted from the rect's height,
                    // intercept it and conditionally modify it
                    yield return CodeInstruction.CallClosure<Func<float, float>>((y) =>
                    {
                        if (CurrentLetter is null || !Settings.IconButtonsEnabled) return y;

                        return Settings.LetterButtonsPosition switch
                        {
                            Settings.ButtonPlacement.BottomRight => y + BottomRightDiaOptionsYShift,
                            Settings.ButtonPlacement.BottomLeft => y + ButtonsHeight,
                            _ => y // unchanged
                        };
                    })!;
                }

                // PATCH 2:
                // Shrink the rect used to draw the DiaOptions.
                // Intercept the call to EndScrollView() and modify the height of the rect passed to it.
                if (!hasPatchedHeight && codes[i]!.Calls(EndScrollViewMethodAnchor))
                {
                    // EndScrollView()
                    yield return codes[i++]!;
                    // float literal
                    yield return codes[i++]!;
                    // Load ref rect
                    yield return codes[i++]!;
                    // Rect.get_height()
                    yield return codes[i++]!;
                    // call a closure that returns a float
                    yield return CodeInstruction.CallClosure<Func<float>>(() =>
                    {
                        if (CurrentLetter is null || !Settings.IconButtonsEnabled) return 0f;

                        return Settings.LetterButtonsPosition switch
                        {
                            Settings.ButtonPlacement.BottomRight => BottomRightDiaOptionsYShift,
                            Settings.ButtonPlacement.BottomLeft => ButtonsHeight,
                            _ => 0f // unchanged
                        };
                    })!;
                    // Subtract the value we just loaded from the height
                    yield return new CodeInstruction(OpCodes.Sub);
                    hasPatchedHeight = true; // EndScrollView() is called twice, ensure only the first one is patched
                }

                yield return codes[i]!;
            }
        }

        #endregion Modify dialog window


        /// <summary>
        /// After the window has been adjusted by the other parts of this patch and after the vanilla elements have been drawn,
        /// add the icon buttons on top
        /// </summary>
        [HarmonyPatch(typeof(Dialog_NodeTree), nameof(Dialog_NodeTree.DoWindowContents))]
        [HarmonyPostfix]
        [UsedImplicitly]
        static void AddIconButtons(Dialog_NodeTree __instance, ref Rect inRect)
        {
            if (CurrentLetter is not { } letter || !Settings.IconButtonsEnabled) return;

            var innerButtonsRect = _buttonsRect.ContractedBy(ButtonsPadding);

            // Switch the order of buttons so the gear button is always on the "inside"
            List<Action<Letter, Rect>> buttons;
            switch (Settings.LetterButtonsPosition)
            {
                case Settings.ButtonPlacement.TopRight:
                case Settings.ButtonPlacement.BottomRight:
                    buttons = new List<Action<Letter, Rect>>(NumButtons)
                    {
                        CustomWidgets.GearIconButton,
                        CustomWidgets.SnoozeIconButton,
                        CustomWidgets.PinIconButton,
                    };
                    break;
                case Settings.ButtonPlacement.TopMiddle:
                case Settings.ButtonPlacement.BottomMiddle:
                    buttons = new List<Action<Letter, Rect>>(NumButtons)
                    {
                        CustomWidgets.GearIconButton,
                        CustomWidgets.PinIconButton,
                        CustomWidgets.SnoozeIconButton,
                    };
                    break;
                case Settings.ButtonPlacement.TopLeft:
                case Settings.ButtonPlacement.BottomLeft:
                default:
                    buttons = new List<Action<Letter, Rect>>(NumButtons)
                    {
                        CustomWidgets.PinIconButton,
                        CustomWidgets.SnoozeIconButton,
                        CustomWidgets.GearIconButton,
                    };
                    break;
            }

            // Widgets.DrawWindowBackground(_buttonsRect);
            var curX = innerButtonsRect.xMin;
            var buttonRect = innerButtonsRect with { x = curX, width = ButtonSize };
            foreach (var button in buttons)
            {
                buttonRect.x = curX;
                button(letter, buttonRect);
                curX += ButtonsSpacing + ButtonSize;
            }
        }
    }
}
