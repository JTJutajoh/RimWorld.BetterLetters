using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;

namespace BetterLetters;

[UsedImplicitly]
internal class WorldComponent_SnoozeManager : WorldComponent
{
    public static WorldComponent_SnoozeManager? Instance { get; private set; }

    public static int MaxNumSnoozes => Settings.MaxNumSnoozes;
    public static int NumSnoozes => Snoozes.Count;

    public WorldComponent_SnoozeManager(World world) : base(world)
    {
        Instance = this;
    }

    public enum SnoozeTypes
    {
        Letter, // Snoozed standard letter
        Reminder // User-created reminder
    }

    internal class Snooze : IExposable
    {
        /// True if the snooze's elapsed time has passed its duration
        internal bool Finished => _elapsed >= _duration;

        /// The raw number of ticks remaining before the snooze finishes
        internal int RemainingTicks => _duration - _elapsed;

        /// The exact abs tick that this snooze was started on
        private int _start;

        /// The number of ticks that this snooze should run for in total
        private int _duration;

        /// Gets the total duration of the snooze in ticks.
        internal int Duration => _duration;

        /// The number of ticks elapsed so far
        private int _elapsed;

        /// Reference to the letter that this snooze is for. If this is null for any reason, the snooze will
        /// immediately expire itself on the next tick.<br />
        /// This <i>must</i> match the letter's reference in the snooze dictionary. If a mismatch is detected, the snooze
        /// will expire itself on the next tick.
        internal Letter? Letter;

        internal bool IsReminder => SnoozeType == SnoozeTypes.Reminder;

        /// If true, the letter will automatically be pinned when the snooze finishes
        private bool _pinWhenFinished;

        internal bool PinWhenFinished => _pinWhenFinished;

        private bool _openWhenFinished;

        /// Defines the behavior of this snooze and used by UI to distinguish it
        internal SnoozeTypes SnoozeType = SnoozeTypes.Letter;

        private static int TickPeriod => Settings.SnoozeTickPeriod;
        private readonly int _tickOffset;

        /// Empty constructor for scribe.
        /// This is only used when loading existing save files.
        [Obsolete("Do not use the blank constructor. It only exists for serialization.")]
        internal Snooze()
        {
            Log.Trace("Creating new empty snooze data");
        }

        internal Snooze(Letter letter, int durationTicks, bool pinWhenFinished = false, bool openWhenFinished = false,
            SnoozeTypes snoozeType = SnoozeTypes.Letter)
        {
            Letter = letter;
            _duration = durationTicks;
            _start = GenTicks.TicksGame;
            _elapsed = 0;
            _pinWhenFinished = pinWhenFinished;
            _openWhenFinished = openWhenFinished;
            SnoozeType = snoozeType;

#if !(v1_1 || v1_2 || v1_3 || v1_4)
            // Since this is being cached, it won't properly update if the user changes the MaxNumSnoozes setting in an ongoing game...
            // but it won't really matter much. Worst case, multiple snoozes tick close together until they load a save
            _tickOffset =
                GenTicks.GetTickIntervalOffset(
                    NumSnoozes,
                    Settings.MaxNumSnoozes,
                    Settings.SnoozeTickPeriod
                );
#else
            // Legacy versions just do it the lazy way and don't do an offset
            _tickOffset = 0;
#endif

            Log.Trace(
                $"Created a new snooze for letter {letter} with duration {durationTicks} and tick offset {_tickOffset}");
        }

        internal void DoTipRegion(Rect rect)
        {
            if (Letter is null || Snoozes[Letter] is not { } snooze) return;

            var remaining = snooze.RemainingTicks.ToStringTicksToPeriodVerbose();
            var end = GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs + snooze.Duration,
                QuestUtility.GetLocForDates());
            TooltipHandler.TipRegionByKey(rect,
                snooze.Letter?.IsReminder() ?? false
                    ? "BetterLetters_SnoozedReminderButtonTooltip"
                    : "BetterLetters_SnoozedButtonTooltip", end, remaining);
        }

        /// <summary>
        /// Ticks the timer, handling what happens if the letter is invalid or if the timer finishes.
        /// </summary>
        /// <returns>true if the timer is complete or invalid. false if the timer is still running</returns>
        internal bool Tick()
        {
#if !(v1_1 || v1_2 || v1_3 || v1_4)
            // Legacy versions just do it the lazy way and don't do an offset
            if (!GenTicks.IsTickInterval(_tickOffset, TickPeriod)) return false;
#endif

            if (Letter is null)
            {
                Log.Warning("Snooze reference to its letter was lost!");
                Messages.Message(
                    "BetterLetters_SnoozeExpired".Translate(),
                    LookTargets.Invalid!,
                    MessageTypeDefOf.RejectInput!,
                    historical: false
                );
                return true;
            }

            _elapsed = GenTicks.TicksGame - _start;
            if (Finished)
            {
                Finish();
            }

            return Finished;
        }

        internal void Finish()
        {
            if (Letter is null)
            {
                Log.Warning("Tried to finish a snooze with a null letter.");
                return;
            }

            if (_pinWhenFinished)
            {
                Letter.Pin(suppressSnoozeCanceledMessage: true);
            }
            else
            {
                Find.LetterStack?.ReceiveLetter(Letter);
            }

            if (_openWhenFinished)
            {
                Letter.UnSnooze();
                Letter.OpenLetter();
            }
        }

        [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && Letter is null)
            {
                Log.Warning("Tried to save a snooze with an expired letter.");
                return;
            }

            Scribe_References.Look(ref Letter!, "letter", false);
            Scribe_Values.Look(ref _elapsed, "elapsed", 0, false);
            Scribe_Values.Look(ref _duration, "duration", 0, false);
            Scribe_Values.Look(ref _start, "start", 0, false);
            Scribe_Values.Look(ref _pinWhenFinished, "pinWhenFinished", false, false);
            Scribe_Values.Look(ref SnoozeType, "snoozeType", SnoozeTypes.Letter, false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !Finished)
            {
                _start = GenTicks.TicksGame;
            }
        }
    }

    private static Dictionary<Letter?, Snooze> _snoozes = new();
    public static Dictionary<Letter?, Snooze> Snoozes => _snoozes;

    /// <summary>
    /// Base method for adding snoozes to the dictionary.
    /// </summary>
    /// <param name="snooze">A snooze instance created externally. The `Letter` field on the snooze will be
    /// used as the key in the dictionary.</param>
    /// <param name="suppressMessage">If true, the top-left message saying that a new snooze was created will not appear.</param>
    /// <returns>True if the snooze was successfully added (not a duplicate, Letter was not null, etc.)</returns>
    public static bool AddSnooze(Snooze snooze, bool suppressMessage = false)
    {
        if (snooze.Letter is null)
        {
            Log.Warning("Tried to add a snooze with a null letter. Skipping.");
            return false;
        }

        if (NumSnoozes >= MaxNumSnoozes)
        {
            Log.Warning("Tried to add a snooze but there are already too many. Skipping.");
            Messages.Message(
                "BetterLetters_TooManySnoozes".Translate(),
                LookTargets.Invalid!,
                MessageTypeDefOf.RejectInput!,
                historical: false
            );
            return false;
        }

        if (Snoozes.ContainsKey(snooze.Letter))
        {
            Log.Warning("Tried to add a snooze for a letter that already has one.");
            return false;
        }
#if !(v1_1 || v1_2 || v1_3)
        if (snooze.Letter is BundleLetter bundleLetter)
        {
            // Snooze each individual letter contained by the BundleLetter instead of the bundle itself
            var bundledLetters = Traverse.Create(bundleLetter)?.Field("bundledLetters")?.GetValue<List<Letter>>() ??
                                 new List<Letter>();
            foreach (var letter in bundledLetters)
            {
                // Recursively snooze each bundled letter
                letter.Snooze(snooze.Duration, snooze.PinWhenFinished, false);
            }

            // Return early to avoid snoozing the BundleLetter itself
            return false;
        }
#endif

        Snoozes.Add(snooze.Letter, snooze);
        snooze.Letter.Unpin();
        Log.Trace("Added snooze for letter " + snooze.Letter);
        if (!suppressMessage && snooze.Duration > 0)
        {
            Messages.Message(
                "BetterLetters_SnoozeAdded".Translate(snooze.Duration.ToStringTicksToPeriod()),
                LookTargets.Invalid!,
                MessageTypeDefOf.PositiveEvent!,
                historical: false
            );
        }

        if (Find.LetterStack?.LettersListForReading?.Contains(snooze.Letter) ?? false)
        {
            Find.LetterStack.RemoveLetter(snooze.Letter);
        }

        return true;
    }

    /// <summary>
    /// Helper method to create a snooze for a letter and add it to the dictionary.
    /// </summary>
    /// <param name="letter">The letter that will be snoozed</param>
    /// <param name="durationTicks">How long the snooze will last</param>
    /// <param name="pinWhenFinished">If true, the letter will be automatically pinned when the snooze finishes</param>
    /// <param name="openWhenFinished">If true, the letter will be automatically opened when the snooze finishes</param>
    /// <returns>Newly-created instance of the snooze for this letter, or null if it failed.</returns>
    public static Snooze? AddSnooze(Letter letter, int durationTicks, bool pinWhenFinished = false,
        bool openWhenFinished = false)
    {
        var success = AddSnooze(new Snooze(letter, durationTicks, Settings.SnoozePinned || pinWhenFinished,
            Settings.SnoozeOpen || openWhenFinished));
        return success ? Snoozes[letter] : null;
    }

    public static bool RemoveSnooze(Letter? letter, bool suppressSnoozeCanceledMessage = false)
    {
        if (letter is null)
        {
            Log.Warning("Tried to remove a null snooze. Skipping.");
            return false;
        }

        if (Snoozes.Remove(letter))
        {
#if v1_4 || v1_5 || v1_6
            var label = letter.Label;
#elif v1_1 || v1_2 || v1_3
            var label = letter.label;
#endif
            if (!suppressSnoozeCanceledMessage)
            {
                Messages.Message(
                    "BetterLetters_SnoozeRemoved".Translate(label),
                    LookTargets.Invalid!,
                    MessageTypeDefOf.PositiveEvent!,
                    historical: false
                );
            }

            return true;
        }

        return false;
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        var allSnoozes = new Dictionary<Letter?, Snooze>(Snoozes);
        foreach (var snooze in allSnoozes)
        {
            if (snooze.Key != null && (snooze.Value?.Tick() ?? true))
            {
                Snoozes.Remove(snooze.Key);
            }
        }
    }

    /// <summary>
    /// Creates and shows the snooze dialog over everything
    /// </summary>
    /// <param name="letter">Instance of the letter that will be snoozed</param>
    /// <param name="onSnooze">Optional callback for when the snooze is completed. Passes the duration of the snooze as a string</param>
    /// <returns>True if the letter was snoozed. False if the user canceled.</returns>
    public static void ShowSnoozeDialog(Letter letter, Action<Snooze?>? onSnooze = null)
    {
        int? maxDurationOverride = null;
        if (letter is LetterWithTimeout { disappearAtTick: > 0 } timedLetter)
        {
            maxDurationOverride = timedLetter.disappearAtTick - Find.TickManager?.TicksGame;
        }

        if (letter is ChoiceLetter choiceLetter && choiceLetter.quest?.GetTicksUntilExpiry() > 0)
        {
            maxDurationOverride = Math.Max(choiceLetter.quest.GetTicksUntilExpiry(), maxDurationOverride ?? 0);
        }

        var snoozeDialog = new Dialog_Snooze(duration =>
            {
                var snooze = AddSnooze(letter, duration);
                onSnooze?.Invoke(snooze);
            },
            maxDurationOverride
        );
        Find.WindowStack?.Add(snoozeDialog);
    }

    // Used just for reassembling the dictionary when a save is loaded
    private static List<Letter>? _letterList;
    private static List<Snooze>? _snoozeList;

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(
            ref _snoozes!,
            "Snoozes",
            LookMode.Reference,
            LookMode.Deep,
            ref _letterList,
            ref _snoozeList
        );

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // Safety checks to sanitize null refs or mismatched data
            foreach (var letter in Snoozes.Keys)
            {
                if (letter == null)
                {
                    Log.Warning("Found a null letter reference in the snooze dictionary. Removing.");
                    Snoozes.Remove(null!);
                    continue;
                }

                if (Snoozes[letter] == null)
                {
                    Log.Warning("Found a null snooze reference for letter " + letter + ". Removing.");
                    Snoozes.Remove(letter);
                    continue;
                }

                if (Snoozes[letter]?.Letter != letter)
                {
                    Log.Warning("Found a mismatched snooze reference for letter " + letter + ". Removing.");
                    Snoozes.Remove(letter);
                }
            }

            _letterList = null;
            _snoozeList = null;
        }
    }
}
