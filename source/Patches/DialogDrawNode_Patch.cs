using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BetterLetters
{
    [StaticConstructorOnStartup]
    class DialogDrawNode_Patch
    {
        private static readonly Texture2D PinTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin");
        private static readonly Texture2D PinOutlineTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin-Outline");
        private static readonly Color PinOutlineColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        // Reference set by OpenLetter patches
        public static Letter curLetter = null;

        static void DoWindowContents(Dialog_NodeTree __instance)
        {
            float size = 32f;
            var offset = new Vector2(-8, -12);
            if (curLetter.IsPinned())
            {
                Rect rect = new Rect((__instance.InitialSize.x - size), (-size / 2), size, size);
                rect.x += offset.x;
                rect.y += offset.y;
                Graphics.DrawTexture(rect, PinTex);
            }
        }
    }
}