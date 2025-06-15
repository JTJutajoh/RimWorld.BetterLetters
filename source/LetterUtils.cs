using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using UnityEngine;

namespace BetterLetters
{
    internal static class LetterUtils
    {
        [StaticConstructorOnStartup]
        internal static class Icons
        {
            internal static readonly Texture2D DismissIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Dismiss");
            internal static readonly Texture2D UnDismissIcon = ContentFinder<Texture2D>.Get("UI/Buttons/UnDismiss");
            internal static readonly Texture2D PinFloatMenuIcon = ContentFinder<Texture2D>.Get("UI/FloatMenuIcons/Pin");
            internal static readonly Texture2D PinIconRound = ContentFinder<Texture2D>.Get("UI/Icons/PinRound"); 
            internal static readonly Texture2D PinOutlineRound = ContentFinder<Texture2D>.Get("UI/Icons/PinRoundOutline");
            internal static readonly Texture2D PinIconAlt = ContentFinder<Texture2D>.Get("UI/Icons/Pin_alt"); 
            internal static readonly Texture2D PinOutlineAlt = ContentFinder<Texture2D>.Get("UI/Icons/PinOutline_alt");
            internal static Texture2D PinIcon => Settings.PinTexture == Settings.PinTextureMode.Round ? PinIconRound : PinIconAlt;
            internal static Texture2D PinOutline => Settings.PinTexture == Settings.PinTextureMode.Round ? PinOutlineRound : PinOutlineAlt;
            internal static Texture2D PinIconLetterStack => PinIconRound; // Might add a setting to swap this later
            internal static readonly Texture2D SnoozeFloatMenuIcon = ContentFinder<Texture2D>.Get("UI/FloatMenuIcons/Snooze");
            internal static readonly Texture2D SnoozeIcon = ContentFinder<Texture2D>.Get("UI/Icons/Snoozed");
            internal static readonly Texture2D SnoozeOutline = ContentFinder<Texture2D>.Get("UI/Icons/SnoozedOutline");
        }

        private static Archive? _archiveCached = null;
        private static Archive Archive => _archiveCached ??= Find.Archive;
        
        public static bool IsPinned(this Letter letter)
        {
            return Archive.IsPinned(letter);
        }

        public static bool IsSnoozed(this Letter letter)
        {
            return SnoozeManager.Snoozes.ContainsKey(letter);
        }

        public static void Pin(this Letter letter)
        {
            if (!Archive.Contains(letter))
            {
                Archive.Add(letter);
            }

            Archive.Pin(letter);
            SnoozeManager.RemoveSnooze(letter);
            SortLetterStackByPinned();
        }

        public static void Unpin(this Letter letter, bool alsoRemove = false)
        {
            Archive.Unpin(letter);
            if (alsoRemove)
            {
                Find.LetterStack.RemoveLetter(letter);
            }

            SortLetterStackByPinned();
        }

        private static readonly FieldInfo? LettersField =
            typeof(LetterStack).GetField("letters", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void SortLetterStackByPinned()
        {
            var letters = (List<Letter>)(LettersField?.GetValue(Find.LetterStack) ?? new List<Letter>());
            letters = letters.OrderBy(obj => obj.IsPinned()).ToList();
            LettersField?.SetValue(Find.LetterStack, letters);
        }
        
#if !v1_6
        // These two functions were added in 1.6. I copied them directly from there for legacy version support since they're so useful

        /// <summary>
        /// Backported from RimWorld 1.6 <see cref="GenUI" />
        /// </summary>
        public static Rect MiddlePart(this Rect rect, float pctWidth, float pctHeight)
        {
            return new Rect((float) ((double) rect.x + (double) rect.width / 2.0 - (double) rect.width * (double) pctWidth / 2.0), (float) ((double) rect.y + (double) rect.height / 2.0 - (double) rect.height * (double) pctHeight / 2.0), rect.width * pctWidth, rect.height * pctHeight);
        }

        /// <summary>
        /// Backported from RimWorld 1.6 <see cref="GenUI" />
        /// </summary>
        public static Rect MiddlePartPixels(this Rect rect, float width, float height)
        {
            return new Rect((float) ((double) rect.x + (double) rect.width / 2.0 - (double) width / 2.0), (float) ((double) rect.y + (double) rect.height / 2.0 - (double) height / 2.0), width, height);
        }
#endif

        /// <summary>
        /// Catch-all helper function to create float menus related to this mod.<br />
        /// Used partially to help with multi-version support since not all features are available in all versions.
        /// </summary>
        public static FloatMenuOption MakeFloatMenuOption(string label, Action action, Texture2D iconTex,
            Color iconColor)
        {
            FloatMenuOption option = new(label: label, action: action
#if v1_6
                , iconTex: iconTex, iconColor: iconColor // Only RimWorld 1.6+ has a constructor that takes icons as parameters
#endif
                ); // Legacy versions just ignore the icon parameters

            return option;
        }

        public static FloatMenuOption PinFloatMenuOption(Letter letter, Action? onPinned = null)
        {
            return MakeFloatMenuOption(
                "BetterLetters_Pin".Translate(),
                action: () =>
                {
                    letter?.Pin();
                    onPinned?.Invoke();
                },
                iconTex: Icons.PinFloatMenuIcon,
                iconColor: Color.white
            );
        }

        public static FloatMenuOption Snooze1HrFloatMenuOption(Letter letter,
            Action<SnoozeManager.Snooze?>? onClicked = null)
        {
            return MakeFloatMenuOption(
                "BetterLetters_SnoozeFor1Hour".Translate(),
                action: () =>
                {
                    var snooze = SnoozeManager.AddSnooze(letter, GenDate.TicksPerHour, Settings.SnoozePinned);
                    onClicked?.Invoke(snooze);
                },
                iconTex: Icons.SnoozeFloatMenuIcon,
                iconColor: new Color(0.2f, 0.2f, 0.2f)
            );
        }

        public static FloatMenuOption Snooze1DayFloatMenuOption(Letter letter,
            Action<SnoozeManager.Snooze?>? onClicked = null)
        {
            return MakeFloatMenuOption(
                "BetterLetters_SnoozeFor1Day".Translate(),
                action: () =>
                {
                    var snooze = SnoozeManager.AddSnooze(letter, GenDate.TicksPerDay, Settings.SnoozePinned);
                    onClicked?.Invoke(snooze);
                },
                iconTex: Icons.SnoozeFloatMenuIcon,
                iconColor: new Color(0.4f, 0.4f, 0.4f)
            );
        }

        public static FloatMenuOption SnoozeDialogFloatMenuOption(Letter letter,
            Action<SnoozeManager.Snooze?>? onSnoozed = null)
        {
            return MakeFloatMenuOption(
                "BetterLetters_SnoozeForFloatMenuOption".Translate(),
                action: () =>
                {
                    SnoozeManager.ShowSnoozeDialog(letter, onSnoozed);
                },
                iconTex: Icons.SnoozeFloatMenuIcon,
                iconColor: Color.white
            );
        }
    }
}