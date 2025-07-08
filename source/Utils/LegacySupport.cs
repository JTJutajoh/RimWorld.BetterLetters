// ReSharper disable RedundantUsingDirective
// ReSharper disable InconsistentNaming
// ReSharper disable RedundantCast

using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse.Sound;

namespace BetterLetters.Utils
{
    /// <summary>
    /// Class with helper functions that assist with multi-version support.<br />
    /// Primarily, backporting functions from newer versions
    /// </summary>
    public static class LegacySupport
    {
        [Flags]
        internal enum RWVersion
        {
            None = 0,
            v1_0 = 2,
            v1_1 = 4,
            v1_2 = 8,
            v1_3 = 16,
            v1_4 = 32,
            v1_5 = 64,
            v1_6 = 128,
            All = v1_0 | v1_1 | v1_2 | v1_3 | v1_4 | v1_5 | v1_6,
        }

        internal static RWVersion CurrentRWVersion
        {
            get
            {
#if v1_1
                return RWVersion.v1_1;
#elif v1_2
                return RWVersion.v1_2;
#elif v1_3
                return RWVersion.v1_3;
#elif v1_4
                return RWVersion.v1_4;
#elif v1_5
                return RWVersion.v1_5;
#elif v1_6
                return RWVersion.v1_6;
#else
                return RWVersion.None;
#endif
            }
        }

#if v1_1 || v1_2 || v1_3
        /// Extension method for a function that was added in RimWorld 1.4+
        public static float SliderLabeled(
            this Listing_Standard this_,
            string label,
            float val,
            float min,
            float max,
            float labelPct = 0.5f,
            string? tooltip = null)
        {
            var rect = this_.GetRect(30f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect.LeftPart(labelPct), label);
            if (tooltip != null)
                TooltipHandler.TipRegion(rect.LeftPart(labelPct), (TipSignal)tooltip);
            Text.Anchor = TextAnchor.UpperLeft;
            var num = Widgets.HorizontalSlider(rect.RightPart(1f - labelPct), val, min, max, true);
            this_.Gap(this_.verticalSpacing);
            return num;
        }
#endif

#if v1_1 || v1_2 || v1_3
        /// New version of GetRect that also takes a widthPct parameter. Added in RW 1.4+
        public static Rect GetRect(this Listing_Standard this_, float height, float widthPct)
        {
            var rect = this_.GetRect(height);
            rect.width = this_.ColumnWidth * widthPct;
            return rect;
        }
#endif

#if v1_1 || v1_2 || v1_3 || v1_4 || v1_5
        // These two functions were added in 1.6. I copied them directly from there for legacy version support since they're so useful

        /// Backported from RimWorld 1.6 <see cref="GenUI" />
        public static Rect MiddlePart(this Rect rect, float pctWidth, float pctHeight)
        {
            return new Rect(
                (float)((double)rect.x + (double)rect.width / 2.0 - (double)rect.width * (double)pctWidth / 2.0),
                (float)((double)rect.y + (double)rect.height / 2.0 - (double)rect.height * (double)pctHeight / 2.0),
                rect.width * pctWidth, rect.height * pctHeight);
        }

        /// Backported from RimWorld 1.6 <see cref="GenUI" />
        public static Rect MiddlePartPixels(this Rect rect, float width, float height)
        {
            return new Rect((float)((double)rect.x + (double)rect.width / 2.0 - (double)width / 2.0),
                (float)((double)rect.y + (double)rect.height / 2.0 - (double)height / 2.0), width, height);
        }
#endif

#if v1_1 || v1_2 || v1_3 || v1_4 || v1_5
        public static TargetingParameters ForThing()
        {
            return new TargetingParameters()
            {
                canTargetPawns = true,
                canTargetBuildings = true,
                canTargetItems = true,
#if !(v1_1 || v1_2)
                canTargetPlants = true,
#endif
                canTargetFires = true,
                mapObjectTargetsMustBeAutoAttackable = false
            };
        }
#endif

#if v1_1 || v1_2 || v1_3
        /// <summary>
        /// Stub version of CheckboxLabeled that just ignores the 2 float params
        /// </summary>
        public static void CheckboxLabeled(this Listing_Standard listingStandard, string label, ref bool val,
            string? tooltip = null, float height = 0f, float labelPct = 1f)
        {
            listingStandard.CheckboxLabeled(label, ref val, tooltip);
        }
#endif

#if v1_1 || v1_2 || v1_3 || v1_4
        /// <summary>
        /// Stub version of <see cref="Listing_Standard.RadioButton"/> that just ignores the params that only exist in RW 1.5+
        /// </summary>
        public static bool RadioButton(this Listing_Standard listingStandard, string label, bool active, float tabIn,
            float tabInRight, string tooltip, float? tooltipDelay, bool disabled)
        {
#if !v1_2
            return listingStandard.RadioButton(label, active, tabIn, tooltip, tooltipDelay);
#else // In 1.2 they marked the normal method obsolete and had the new one as "_NewTemp"
        return listingStandard.RadioButton_NewTemp(label, active, tabIn, tooltip, tooltipDelay);
#endif
        }
#endif

#if v1_1 || v1_2 || v1_3
        public static Rect SubLabel(this Listing_Standard listingStandard, string label, float widthPct)
        {
            Rect rect = listingStandard.GetRect(Text.CalcHeight(label, listingStandard.ColumnWidth * widthPct),
                widthPct);
            float num = 20f;
            rect.x += num;
            rect.width -= num;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listingStandard.Gap(listingStandard.verticalSpacing);
            return rect;
        }
#endif

#if v1_1 || v1_2 || v1_3 || v1_4 || v1_5
        /// <summary>
        /// RW 1.6 bugfixed <see cref="Widgets.TextFieldNumeric"/> so that it only clamps the input value if a minimum is specified<br />
        /// In 1.5 or earlier, it is automatically clamped to 0, and the minimum is not passed through from <see cref="Listing_Standard.IntEntry"/>
        /// so this is a replacement version that fixes that, so that negative numbers are allowed.<br />
        /// </summary>
        internal static void IntEntryWithNegative(this Listing_Standard _this, ref int val, ref string editBuffer,
            int multiplier = 1, int min = 0)
        {
            Rect rect = _this.GetRect(24f);
            // ReSharper disable once BadPreprocessorIndent
#if !v1_2
            if (!_this.BoundingRectCached.HasValue || rect.Overlaps(_this.BoundingRectCached.Value))
#endif
                IntEntryWithNegative(rect, ref val, ref editBuffer, multiplier,
                    min); // Call the replaced bugfix version
            _this.Gap(_this.verticalSpacing);
        }

        /// <summary>
        /// Same bugfix as the above overload: <see cref="IntEntryWithNegative(Verse.Listing_Standard,ref int,ref string,int,int)"/>,
        /// this replaces vanilla <see cref="Widgets.IntEntry"/> to fix the bug by passing on the value of min to the call to
        /// <see cref="Widgets.TextFieldNumeric"/>.
        /// </summary>
        // [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        private static void IntEntryWithNegative(Rect rect, ref int value, ref string editBuffer, int multiplier = 1,
            int min = 0)
        {
            // IntEntryButtonWidth is a private field on Widgets but it's needed here
            var IntEntryButtonWidth = new Traverse(typeof(Widgets)).Field("IntEntryButtonWidth")?.GetValue<int>() ?? 40;

            // Original method
            int width = Mathf.Min(IntEntryButtonWidth, (int)rect.width / 5);
            if (Widgets.ButtonText(new Rect(rect.xMin, rect.yMin, (float)width, rect.height),
                    (-10 * multiplier).ToStringCached()!))
            {
                value -= 10 * multiplier * GenUI.CurrentAdjustmentMultiplier();
                editBuffer = value.ToStringCached()!;
                SoundDefOf.Checkbox_TurnedOff!.PlayOneShotOnCamera();
            }

            if (Widgets.ButtonText(new Rect(rect.xMin + (float)width, rect.yMin, (float)width, rect.height),
                    (-1 * multiplier).ToStringCached()!))
            {
                value -= multiplier * GenUI.CurrentAdjustmentMultiplier();
                editBuffer = value.ToStringCached()!;
                SoundDefOf.Checkbox_TurnedOff!.PlayOneShotOnCamera();
            }

            if (Widgets.ButtonText(new Rect(rect.xMax - (float)width, rect.yMin, (float)width, rect.height),
                    "+" + (10 * multiplier).ToStringCached()))
            {
                value += 10 * multiplier * GenUI.CurrentAdjustmentMultiplier();
                editBuffer = value.ToStringCached()!;
                SoundDefOf.Checkbox_TurnedOn!.PlayOneShotOnCamera();
            }

            if (Widgets.ButtonText(new Rect(rect.xMax - (float)(width * 2), rect.yMin, (float)width, rect.height),
                    "+" + multiplier.ToStringCached()))
            {
                value += multiplier * GenUI.CurrentAdjustmentMultiplier();
                editBuffer = value.ToStringCached()!;
                SoundDefOf.Checkbox_TurnedOn!.PlayOneShotOnCamera();
            }

            Widgets.TextFieldNumeric(
                new Rect(rect.xMin + (float)(width * 2), rect.yMin, rect.width - (float)(width * 4), rect.height),
                ref value, ref editBuffer, min);
        }
#endif

#if v1_1 || v1_2 || v1_3 || v1_4
        internal static void AdjustRectsForScrollView(Rect parentRect, ref Rect outRect, ref Rect viewRect)
        {
            if ((double)viewRect.height < (double)outRect.height)
                return;
            viewRect.width -= 20f;
            outRect.xMax -= 4f;
            outRect.yMin = Mathf.Max(parentRect.yMin + 6f, outRect.yMin);
            outRect.yMax = Mathf.Min(parentRect.yMax - 6f, outRect.yMax);
        }
#endif

        /// <summary>
        /// Extension method for <see cref="Quest"/> that checks for when it expires/fails.<br/>
        /// If it has not been accepted, returns the number of ticks until the offer expires.<br/>
        /// If it has been accepted or if it started active, it returns the number of ticks until the quest "ends".
        /// What exactly counts as an ending depends on the specific quest but overall it seems to work.
        /// </summary>
        internal static int GetTicksUntilExpiryOrFail(this Quest quest)
        {
            // For quests that start active or that the player has accepted and have a deadline
            if (quest.State == QuestState.Ongoing && quest.PartsListForReading is { } questParts)
            {
                // Look through the quest's quest parts for any that have a TicksLeft property
                // This mirrors the code used in MainTabWindow_Quests for displaying the amount of time left for a selected quest.
                foreach (var questPart in questParts)
                {
                    if (questPart is QuestPart_Delay { State: QuestPartState.Enabled, TicksLeft: > 0 } questPartDelay)
                    {
                        //BUG: Auto snooze duration is spoiling quests like betrayal offers
                        // Filter for specific types of QuestPart.
                        // Or maybe just filter for if the QuestPart is hidden or not? I think that's a thing.
                        return questPartDelay.TicksLeft;
                    }
                }
            }
#if v1_1 || v1_2 || v1_3 || v1_4 || v1_5
            return quest.ticksUntilAcceptanceExpiry;
#else
            return quest.TicksUntilExpiry;
#endif
        }

#if (v1_1 || v1_2)
    internal static void Label(float x, ref float curY, float width, string text, TipSignal tip = default(TipSignal))
    {
        if (text.NullOrEmpty())
            return;
        float height = Verse.Text.CalcHeight(text, width);
        Rect rect1 = new Rect(x, curY, width, height);
        if (!(tip.text?.NullOrEmpty() ?? true) || tip.textGetter != null)
        {
            float x1 = Verse.Text.CalcSize(text).x;
            Rect rect2 = new Rect(rect1.x, rect1.y, x1, height);
            Widgets.DrawHighlightIfMouseover(rect2);
            TooltipHandler.TipRegion(rect2, tip);
        }

        Widgets.Label(rect1, text);
        curY += height;
    }
#endif
    }
}
