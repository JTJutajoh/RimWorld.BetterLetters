using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace BetterLetters.Patches
{
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
            var tex = CurrentLetter.IsPinned() ? LetterUtils.Icons.PinIcon : CurrentLetter.IsReminder() ? LetterUtils.Icons.Reminder : LetterUtils.Icons.SnoozeIcon;
            Graphics.DrawTexture(rect, tex);
        }

        /// When a new letter dialog is created/opened, clear the reference we keep to the letter<br />
        /// Basically prevents the main patch from running on dialogs that shouldn't get an icon
        [HarmonyPatch(typeof(Dialog_NodeTree), MethodType.Constructor, typeof(DiaNode), typeof(bool), typeof(bool), typeof(string))]
        [HarmonyPostfix]
        [UsedImplicitly]
        static void ConstructorPostfix()
        {
            Patch_Dialog_NodeTree_DoWindowContents_AddDialogIcon.CurrentLetter = null;
        }
    }
}