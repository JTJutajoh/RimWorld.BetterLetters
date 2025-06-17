using UnityEngine;
using Verse;

namespace BetterLetters.Patches
{
    internal class DialogDrawNodePatch
    {
        private static float PinTexSize => (float)Settings.TextureInDialogSize;

        /// Reference set externally by <see cref="OpenLetterPatch.SaveLetterReference"/> patch
        public static Letter? CurrentLetter = null;

        /// <summary>
        /// Patch that draws the pin icon in the letter dialog
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static void DoWindowContents(Dialog_NodeTree __instance)
        {
            if (CurrentLetter is null || (!(CurrentLetter?.IsPinned() ?? false) && !(CurrentLetter?.IsSnoozed() ?? false))) return;

            var offset = new Vector2(-8, -12);
            var rect = new Rect((__instance.InitialSize.x - PinTexSize), (-PinTexSize / 2), PinTexSize, PinTexSize);
            rect.x += offset.x;
            rect.y += offset.y;
            var tex = CurrentLetter.IsPinned() ? LetterUtils.Icons.PinIcon : CurrentLetter.IsReminder() ? LetterUtils.Icons.Reminder : LetterUtils.Icons.SnoozeIcon;
            Graphics.DrawTexture(rect, tex);
        }
    }
}