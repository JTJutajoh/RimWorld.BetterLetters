using System.Collections.Generic;
using System.Linq;
using RimWorld;
using System.Reflection;
using UnityEngine;

namespace BetterLetters.Utils
{
    internal static class LetterUtils
    {
        internal static readonly LetterDef ReminderLetterDef = DefDatabase<LetterDef>.GetNamed("Reminder")!;


        public static bool IsPinned(this Letter letter)
        {
            return Find.Archive?.IsPinned(letter) ?? false;
        }

        public static bool IsSnoozed(this Letter letter, bool ignoreReminders = false)
        {
            if (ignoreReminders)
            {
                return WorldComponent_SnoozeManager.Snoozes.ContainsKey(letter) &&
                       WorldComponent_SnoozeManager.Snoozes[letter]?.SnoozeType !=
                       SnoozeTypes.Reminder;
            }

            return WorldComponent_SnoozeManager.Snoozes.ContainsKey(letter);
        }

        internal static bool WasEverSnoozed(this Letter letter)
        {
            return WorldComponent_SnoozeManager.AllSnoozesSeen.Contains(letter.ID);
        }

        public static void Pin(this Letter letter, bool suppressSnoozeCanceledMessage = false)
        {
            if (!Find.Archive?.Contains(letter) ?? false)
            {
                Find.Archive!.Add(letter);
            }

            Find.Archive?.Pin(letter);
            WorldComponent_SnoozeManager.RemoveSnooze(letter, suppressSnoozeCanceledMessage);
            SortLetterStackByPinned();
            if (letter is ChoiceLetter { quest: not null } choiceLetter
#if !(v1_1 || v1_2)
                && choiceLetter.quest?.GetSubquests() is { } subQuests
#endif
               )
            {
                choiceLetter.quest.dismissed = false;
#if !(v1_1 || v1_2)
                foreach (var subQuest in subQuests)
                {
                    subQuest.dismissed = choiceLetter.quest.dismissed;
                }
#endif
                ((MainTabWindow_Quests)MainButtonDefOf.Quests!.TabWindow!).Select(choiceLetter.quest);
            }
        }

        public static void Unpin(this Letter letter, bool alsoRemove = false)
        {
            Find.Archive?.Unpin(letter);
            if (alsoRemove)
            {
                Find.LetterStack?.RemoveLetter(letter);
            }

            SortLetterStackByPinned();
        }

        public static void Snooze(this Letter letter, int durationTicks, bool pinWhenFinished = false,
            bool openWhenFinished = false)
        {
            WorldComponent_SnoozeManager.AddSnooze(letter, durationTicks, pinWhenFinished, openWhenFinished);
        }

        public static bool UnSnooze(this Letter letter, bool suppressCancelMessage = false)
        {
            return WorldComponent_SnoozeManager.RemoveSnooze(letter, suppressCancelMessage);
        }

        public static void AddReminder(this Letter letter, int durationTicks, bool isPinned = false,
            bool openWhenFinished = false)
        {
            WorldComponent_SnoozeManager.AddSnooze(new Snooze(letter, durationTicks,
                pinWhenFinished: isPinned,
                openWhenFinished: openWhenFinished,
                snoozeType: SnoozeTypes.Reminder), suppressMessage: true);
            if (durationTicks > 0)
            {
                Messages.Message(new Message(
                    "BetterLetters_ReminderCreated".Translate(durationTicks.ToStringTicksToPeriod()),
                    MessageTypeDefOf.SilentInput!));
            }
        }

        public static void AddReminder(string label, string text, LetterDef def, int durationTicks,
            bool isPinned = false, bool openWhenFinished = false, LookTargets? lookTargets = null)
        {
            if (text.Length == 0)
            {
                text = label;
            }

            var letter = LetterMaker.MakeLetter(
                label: label,
                text: text,
                def: def
            )!;
            letter.lookTargets = lookTargets!;
            Find.Archive?.Add(letter);
            letter.AddReminder(durationTicks, isPinned, openWhenFinished);
        }

        public static bool IsReminder(this Letter letter)
        {
            if (letter.def!.defName == "Reminder" ||
                WorldComponent_SnoozeManager.AllRemindersSeen.Contains(letter.ID))
            {
                return true;
            }

            return WorldComponent_SnoozeManager.Snoozes.ContainsKey(letter) &&
                   (WorldComponent_SnoozeManager.Snoozes[letter]?.IsReminder ?? false);
        }

        private static readonly FieldInfo? LettersField =
            typeof(LetterStack).GetField("letters", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void SortLetterStackByPinned()
        {
            var letters = (List<Letter>)(LettersField?.GetValue(Find.LetterStack!) ?? new List<Letter>());
            letters = letters.OrderBy(obj => obj.IsPinned()).ToList();
            LettersField?.SetValue(Find.LetterStack!, letters);
        }

        // This helper function will be called at least twice every frame and iterates over potentially hundreds of letters
        // to find a match, so it's important to cache the results.
        private static readonly Dictionary<Quest, ChoiceLetter?> QuestLetterCache = new();

        /// <summary>
        /// Search for the "new quest" letter associated with a quest, since the quest does not store a reference to it.<br />
        /// Results (including null) are cached. The cache is however not serialized.
        /// </summary>
        public static ChoiceLetter? GetQuestLetter(this Quest quest)
        {
            if (QuestLetterCache.TryGetValue(quest, out var cachedLetter))
            {
                return cachedLetter;
            }

            foreach (var archivable in Find.Archive?.ArchivablesListForReading ?? new List<IArchivable>())
            {
                if (archivable is not ChoiceLetter letter || letter.quest != quest) continue;
                QuestLetterCache[quest] = letter;
                return letter;
            }

            // Cache null results too so we don't have to search for them again. A quest won't gain a letter if it didn't have one
            QuestLetterCache[quest] = null;
            return null;
        }

        /// <summary>
        /// Given a letter, checks if the letter itself has an expiration or if it's associated with a quest that does.
        /// </summary>
        /// <returns>Number of ticks until expiration OR -1 if it does not have an expiration.</returns>
        internal static int RemainingTicks(this Letter letter)
        {
            var remainingTicks = -1;
            if (letter is LetterWithTimeout { disappearAtTick: > 0 } timedLetter)
            {
                remainingTicks = timedLetter.disappearAtTick - Find.TickManager!.TicksGame;
            }

            if (letter is ChoiceLetter choiceLetter && choiceLetter.quest?.GetTicksUntilExpiryOrFail() > 0)
            {
                remainingTicks = choiceLetter.quest.GetTicksUntilExpiryOrFail();
            }

            return remainingTicks;
        }

        internal static float TicksToTimeUnit(this int numTicks, TimeUnits timeUnit)
        {
            return numTicks / (float)timeUnit;
        }

        internal static string ToStringTicksToPeriodVeryVerbose(this int numTicks, Color? color = null)
        {
            if (numTicks <= 0)
                return "BetterLetters_Immediately".Translate();

            int years;
            int quadrums;
            int days;
            float hoursFloat;
            numTicks.TicksToPeriod(out years, out quadrums, out days, out hoursFloat);
            int hours = Mathf.RoundToInt(hoursFloat);

            var ticksToPeriodVeryVerbose = "";

            if (years > 0)
            {
                ticksToPeriodVeryVerbose += years != 1
                    ? (string)"BetterLetters_PeriodYears".Translate(years)
                    : (string)"BetterLetters_Period1Year".Translate();
                ticksToPeriodVeryVerbose += ", ";
            }

            if (quadrums > 0 || years > 0)
            {
                ticksToPeriodVeryVerbose += quadrums != 1
                    ? (string)("BetterLetters_PeriodSeasons".Translate(quadrums))
                    : (string)("BetterLetters_Period1Season".Translate());
                ticksToPeriodVeryVerbose += ", ";
            }

            if (days > 0 || quadrums > 0 || years > 0)
            {
                ticksToPeriodVeryVerbose += days != 1
                    ? (string)("BetterLetters_PeriodDays".Translate(days))
                    : (string)("BetterLetters_Period1Day".Translate());
                if (!Mathf.Approximately(hoursFloat, 0f)) ticksToPeriodVeryVerbose += ", ";
            }

            if ((years == 0 && quadrums == 0 && days == 0) || hours > 0)
            {
                if (hoursFloat < 0.1f)
                {
                    ticksToPeriodVeryVerbose += numTicks == 1
                        ? (string)("BetterLetters_Period1Tick".Translate())
                        : (string)("BetterLetters_PeriodTicks".Translate(numTicks));
                }
                else if (Mathf.Approximately(hoursFloat % 1, 0f))
                {
                    ticksToPeriodVeryVerbose += hours == 1
                        ? (string)("BetterLetters_Period1Hour".Translate())
                        : (string)("BetterLetters_PeriodHours".Translate(hours));
                }
                else
                {
                    ticksToPeriodVeryVerbose += Mathf.Approximately(hoursFloat, 1f)
                        ? (string)("BetterLetters_Period1Hour".Translate())
                        : (string)("BetterLetters_PeriodHours".Translate(hoursFloat.ToString("F1")));
                }
            }

            return color != null ? ticksToPeriodVeryVerbose.Colorize(color.Value)! : ticksToPeriodVeryVerbose;
        }

        internal static int TicksFromPeriod(int ticks, float hours, int days, int quadrums, int years)
        {
            return ticks + Mathf.RoundToInt(hours * GenDate.TicksPerHour) + days * GenDate.TicksPerDay +
                   quadrums * GenDate.TicksPerSeason + years * GenDate.TicksPerYear;
        }

        internal enum TimeUnits
        {
            Ticks = 1,
            Hours = GenDate.TicksPerHour,
            Days = GenDate.TicksPerDay,
            Seasons = GenDate.TicksPerSeason,
            Years = GenDate.TicksPerYear,
            Decades = GenDate.TicksPerYear * 10
        }
    }
}
