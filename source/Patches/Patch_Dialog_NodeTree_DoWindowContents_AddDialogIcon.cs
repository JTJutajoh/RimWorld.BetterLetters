using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace BetterLetters.Patches
{
    [HarmonyPatch]
    [HarmonyPatchCategory("Dialog_AddIcons")]
    [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [UsedImplicitly]
    internal static class Patch_Letter_OpenLetter_SaveReference
    {
        /// This patch needs to be applied to all implementations of Letter.OpenLetter<br />
        /// There are currently only two in vanilla
        [UsedImplicitly]
        static IEnumerable<MethodBase> TargetMethods() => new[]
        {
            AccessTools.Method(typeof(ChoiceLetter), nameof(ChoiceLetter.OpenLetter)),
            AccessTools.Method(typeof(DeathLetter), nameof(ChoiceLetter.OpenLetter)),
        };

        private static readonly MethodInfo? AnchorMethodAddToStack =
            typeof(WindowStack).GetMethod(nameof(WindowStack.Add));

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
                        Patch_Dialog_NodeTree_DoWindowContents_AddDialogIcon.CurrentLetter = letter;
                    })!;
                }


                yield return codes[i]!;
            }
        }
    }

    /// Patch to add icons to the letter dialog based on if the dialog is linked to a letter that is
    /// pinned/snoozed/a reminder/etc.<br />
    [HarmonyPatch]
    [HarmonyPatchCategory("Dialog_AddIcons")]
    [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [UsedImplicitly]
    internal static class Patch_Dialog_NodeTree_DoWindowContents_AddDialogIcon
    {
        static float PinTexSize => Settings.TextureInDialogSize;

        /// Reference set by <see cref="Patch_Letter_OpenLetter_AddDiaOptions"/>
        public static Letter? CurrentLetter;

        /// Patch that draws an additional icon in the letter dialog, pin/snooze/reminder/etc.
        [HarmonyPatch(typeof(Dialog_NodeTree), nameof(Dialog_NodeTree.DoWindowContents))]
        [HarmonyPostfix]
        [UsedImplicitly]
        static void DoWindowContents(Dialog_NodeTree __instance)
        {
            if (CurrentLetter is null || !(CurrentLetter.IsPinned() && !CurrentLetter.IsSnoozed())) return;

            var offset = new Vector2(-8, -12);
            var rect = new Rect((__instance.InitialSize.x - PinTexSize), (-PinTexSize / 2), PinTexSize, PinTexSize);
            rect.x += offset.x;
            rect.y += offset.y;
            var tex = CurrentLetter.IsPinned() ? LetterUtils.Icons.PinIcon :
                CurrentLetter.IsReminder() ? LetterUtils.Icons.Reminder : LetterUtils.Icons.SnoozeIcon;
            Graphics.DrawTexture(rect, tex);
        }

        /// When a new letter dialog is created/opened, clear the reference we keep to the letter<br />
        /// Basically prevents the main patch from running on dialogs that shouldn't get an icon
        [HarmonyPatch(typeof(Dialog_NodeTree), MethodType.Constructor, typeof(DiaNode), typeof(bool), typeof(bool),
            typeof(string))]
        [HarmonyPostfix]
        [UsedImplicitly]
        static void ConstructorPostfix()
        {
            Patch_Dialog_NodeTree_DoWindowContents_AddDialogIcon.CurrentLetter = null;
        }
    }
}
