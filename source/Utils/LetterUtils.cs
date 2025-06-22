using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using System.Reflection;
using UnityEngine;

namespace BetterLetters.Utils
{
    internal static class LetterUtils
    {
        [StaticConstructorOnStartup]
        internal static class Icons
        {
            // ReSharper disable AssignNullToNotNullAttribute
            internal static readonly Texture2D Dismiss = ContentFinder<Texture2D>.Get("UI/Buttons/Dismiss");
            internal static readonly Texture2D UnDismiss = ContentFinder<Texture2D>.Get("UI/Buttons/UnDismiss");
            internal static readonly Texture2D PinFloatMenu = ContentFinder<Texture2D>.Get("UI/FloatMenuIcons/Pin");

            internal static readonly Texture2D ReminderFloatMenu =
                ContentFinder<Texture2D>.Get("UI/FloatMenuIcons/Reminder");

            internal static readonly Texture2D Reminder = ContentFinder<Texture2D>.Get("UI/Icons/Reminder");
            internal static readonly Texture2D ReminderOutline = ContentFinder<Texture2D>.Get("UI/Icons/Reminder");

            internal static readonly Texture2D ReminderSmall =
                ContentFinder<Texture2D>.Get("UI/GlobalControls/Reminder");

            internal static readonly Texture2D PinRound = ContentFinder<Texture2D>.Get("UI/Icons/PinRound");

            internal static readonly Texture2D PinOutlineRound =
                ContentFinder<Texture2D>.Get("UI/Icons/PinRoundOutline");

            internal static readonly Texture2D PinAlt = ContentFinder<Texture2D>.Get("UI/Icons/Pin_alt");
            internal static readonly Texture2D PinOutlineAlt = ContentFinder<Texture2D>.Get("UI/Icons/PinOutline_alt");

            internal static Texture2D PinIcon =>
                Settings.PinTexture == Settings.PinTextureMode.Round ? PinRound : PinAlt;

            internal static Texture2D PinOutline => Settings.PinTexture == Settings.PinTextureMode.Round
                ? PinOutlineRound
                : PinOutlineAlt;

            internal static Texture2D PinLetterStack => PinRound; // Might add a setting to swap this later

            internal static readonly Texture2D SnoozeFloatMenu =
                ContentFinder<Texture2D>.Get("UI/FloatMenuIcons/Snooze");

            internal static readonly Texture2D SnoozeIcon = ContentFinder<Texture2D>.Get("UI/Icons/Snoozed");

            internal static readonly Texture2D SnoozeOutline = ContentFinder<Texture2D>.Get("UI/Icons/SnoozedOutline");
            // ReSharper restore AssignNullToNotNullAttribute
        }

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
                       WorldComponent_SnoozeManager.SnoozeTypes.Reminder;
            }

            return WorldComponent_SnoozeManager.Snoozes.ContainsKey(letter);
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

        public static void Snooze(this Letter letter, int durationTicks, bool isPinned = false)
        {
            WorldComponent_SnoozeManager.AddSnooze(letter, durationTicks, isPinned);
        }

        public static bool UnSnooze(this Letter letter)
        {
            return WorldComponent_SnoozeManager.RemoveSnooze(letter);
        }

        public static void AddReminder(this Letter letter, int durationTicks, bool isPinned = false)
        {
            WorldComponent_SnoozeManager.AddSnooze(new WorldComponent_SnoozeManager.Snooze(letter, durationTicks,
                isPinned,
                WorldComponent_SnoozeManager.SnoozeTypes.Reminder), suppressMessage: true);
            if (durationTicks > 0)
            {
                Messages.Message(new Message(
                    "BetterLetters_ReminderCreated".Translate(durationTicks.ToStringTicksToPeriod()),
                    MessageTypeDefOf.SilentInput!));
            }
        }

        public static void AddReminder(string label, string text, LetterDef def, int durationTicks,
            bool isPinned = false, LookTargets? lookTargets = null)
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
            letter.AddReminder(durationTicks, isPinned);
        }

        public static bool IsReminder(this Letter letter)
        {
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
        public static ChoiceLetter? GetLetter(this Quest quest)
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
        /// Catch-all helper function to create float menus related to this mod.<br />
        /// Used partially to help with multi-version support since not all features are available in all versions.
        /// </summary>
        public static FloatMenuOption MakeFloatMenuOption(string label, Action action, Texture2D iconTex,
            Color iconColor)
        {
            FloatMenuOption option = new(label: label, action: action
#if v1_6
                , iconTex: iconTex,
                iconColor: iconColor // Only RimWorld 1.6+ has a constructor that takes icons as parameters
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
                    letter.Pin();
                    onPinned?.Invoke();
                },
                iconTex: Icons.PinFloatMenu,
                iconColor: Color.white
            );
        }

        public static FloatMenuOption Snooze1HrFloatMenuOption(Letter letter,
            Action<WorldComponent_SnoozeManager.Snooze?>? onClicked = null)
        {
            return MakeFloatMenuOption(
                "BetterLetters_SnoozeFor1Hour".Translate(),
                action: () =>
                {
                    var snooze = WorldComponent_SnoozeManager.AddSnooze(letter, GenDate.TicksPerHour);
                    onClicked?.Invoke(snooze);
                },
                iconTex: Icons.SnoozeFloatMenu,
                iconColor: new Color(0.2f, 0.2f, 0.2f)
            );
        }

        public static FloatMenuOption Snooze1DayFloatMenuOption(Letter letter,
            Action<WorldComponent_SnoozeManager.Snooze?>? onClicked = null)
        {
            return MakeFloatMenuOption(
                "BetterLetters_SnoozeFor1Day".Translate(),
                action: () =>
                {
                    var snooze = WorldComponent_SnoozeManager.AddSnooze(letter, GenDate.TicksPerDay);
                    onClicked?.Invoke(snooze);
                },
                iconTex: Icons.SnoozeFloatMenu,
                iconColor: new Color(0.4f, 0.4f, 0.4f)
            );
        }

        public static FloatMenuOption SnoozeDialogFloatMenuOption(Letter letter,
            Action<WorldComponent_SnoozeManager.Snooze?>? onSnoozed = null)
        {
            return MakeFloatMenuOption(
                "BetterLetters_SnoozeForFloatMenuOption".Translate(),
                action: () => { WorldComponent_SnoozeManager.ShowSnoozeDialog(letter, onSnoozed); },
                iconTex: Icons.SnoozeFloatMenu,
                iconColor: Color.white
            );
        }

        internal static float TicksToTimeUnit(this int numTicks, TimeUnits timeUnit)
        {
            return (float)numTicks / (float)timeUnit;
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
                    ? (string)"PeriodYears".Translate(years)
                    : (string)"Period1Year".Translate();
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
                    ? (string)("PeriodDays".Translate(days))
                    : (string)("Period1Day".Translate());
                if (!Mathf.Approximately(hoursFloat, 0f)) ticksToPeriodVeryVerbose += ", ";
            }

            if ((years == 0 && quadrums == 0 && days == 0) || hours > 0)
            {
                if (Mathf.Approximately(hoursFloat % 1, 0f))
                {
                    ticksToPeriodVeryVerbose += hours == 1
                        ? (string)("Period1Hour".Translate())
                        : (string)("PeriodHours".Translate(hours));
                }
                else
                {
                    ticksToPeriodVeryVerbose += Mathf.Approximately(hoursFloat, 1f)
                        ? (string)("Period1Hour".Translate())
                        : (string)("PeriodHours".Translate(hoursFloat.ToString("F1")));
                }
            }

            return color != null ? ticksToPeriodVeryVerbose.Colorize(color.Value)! : ticksToPeriodVeryVerbose;
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
