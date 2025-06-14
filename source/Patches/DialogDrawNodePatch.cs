using UnityEngine;
using Verse;

namespace BetterLetters.Patches
{
    [StaticConstructorOnStartup]
    internal class DialogDrawNodePatch
    {
        internal static readonly Texture2D PinTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin");
        internal static readonly Texture2D PinTex_Alt = ContentFinder<Texture2D>.Get("UI/Icons/Pin_alt");
        internal static readonly Texture2D SnoozedTex = ContentFinder<Texture2D>.Get("UI/Icons/Snoozed");
        internal static readonly Texture2D PinOutlineTex = ContentFinder<Texture2D>.Get("UI/Icons/PinOutline");
        internal static readonly Texture2D PinOutlineTex_Alt = ContentFinder<Texture2D>.Get("UI/Icons/PinOutline_alt");
        internal static readonly Color PinOutlineColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static float PinTexSize => (float)Settings.TextureInDialogSize;

        /// Reference set externally by <see cref="OpenLetterPatch.SaveLetterReference"/> patch
        public static Letter? CurrentLetter = null;

        /// <summary>
        /// Patch that draws the pin icon in the letter dialog
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static void DoWindowContents(Dialog_NodeTree __instance)
        {
            if (!(CurrentLetter?.IsPinned() ?? false) && !(CurrentLetter?.IsSnoozed() ?? false)) return;

            var offset = new Vector2(-8, -12);
            var rect = new Rect((__instance.InitialSize.x - PinTexSize), (-PinTexSize / 2), PinTexSize, PinTexSize);
            rect.x += offset.x;
            rect.y += offset.y;
            var tex = CurrentLetter?.IsPinned() ?? false
                ? (Settings.PinTexture == Settings.PinTextureMode.Round ? PinTex : PinTex_Alt)
                : SnoozedTex;
            Graphics.DrawTexture(rect, tex);
        }
    }
}