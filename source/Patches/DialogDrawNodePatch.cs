using UnityEngine;
using Verse;

namespace BetterLetters.Patches
{
    [StaticConstructorOnStartup]
    class DialogDrawNodePatch
    {
        private static readonly Texture2D PinTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin");
        private static readonly Texture2D PinOutlineTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin-Outline");
        private static readonly Color PinOutlineColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        // Reference set by OpenLetter patches
        public static Letter? CurrentLetter = null;

        static void DoWindowContents(Dialog_NodeTree __instance)
        {
            if (CurrentLetter?.IsPinned() ?? false)
            {
                float size = 32f;
                var offset = new Vector2(-8, -12);
                Rect rect = new Rect((__instance.InitialSize.x - size), (-size / 2), size, size);
                rect.x += offset.x;
                rect.y += offset.y;
                Graphics.DrawTexture(rect, PinTex);
            }
        }
    }
}