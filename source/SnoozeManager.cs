using System;
using System.Collections.Generic;
using DarkLog;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace BetterLetters;

public class SnoozeManager : WorldComponent
{
    public static SnoozeManager? Instance { get; private set; }

    public static int MaxNumSnoozes => Settings.MaxNumSnoozes;
    public static int NumSnoozes => Snoozes.Count;
    
    public SnoozeManager(World world) : base(world)
    {
        Instance = this;
    }

    public enum SnoozeTypes
    {
        Letter, // Snoozed standard letter
        Reminder // User-created reminder
    }
    
    public class Snooze : IExposable
    {
        /// True if the snooze's elapsed time has passed its duration
        public bool Finished => _elapsed >= _duration;
        /// The raw number of ticks remaining before the snooze finishes
        public int RemainingTicks => _duration - _elapsed;
        
        /// The exact abs tick that this snooze was started on
        private int _start;
        /// The number of ticks that this snooze should run for in total
        private int _duration;
        /// Gets the total duration of the snooze in ticks.
        public int Duration => _duration;
        /// The number of ticks elapsed so far
        private int _elapsed;
        /// Reference to the letter that this snooze is for. If this is null for any reason, the snooze will
        /// immediately expire itself on the next tick.<br />
        /// This <i>must</i> match the letter's reference in the snooze dictionary. If a mismatch is detected, the snooze
        /// will expire itself on the next tick.
        public Letter? Letter;
        /// If true, the letter will automatically be pinned when the snooze finishes
        private bool _pinWhenFinished;
        /// Defines the behavior of this snooze and used by UI to distinguish it
        public SnoozeTypes SnoozeType = SnoozeTypes.Letter;

        [Obsolete("Do not use the blank constructor. It only exists for serialization.")]
        public Snooze()
        {
            LogPrefixed.Trace("Creating new empty snooze data");
            // Empty constructor for scribe.
            // This is only used when loading existing save files.
        }
        
        public Snooze(Letter letter, int durationTicks, bool pinWhenFinished = false, SnoozeTypes snoozeType = SnoozeTypes.Letter)
        {
            LogPrefixed.Trace("Creating new snooze");
            this.Letter = letter;
            this._duration = durationTicks;
            this._start = GenTicks.TicksGame;
            this._elapsed = 0;
            this._pinWhenFinished = pinWhenFinished;
            this.SnoozeType = snoozeType;
        }

        public void DoTipRegion(Rect rect)
        {
            if (this.Letter is null) return;
            
            var snooze = SnoozeManager.Snoozes[this.Letter];
            var remaining = snooze.RemainingTicks.ToStringTicksToPeriodVerbose();
            var end = GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs + snooze.Duration, QuestUtility.GetLocForDates());
            TooltipHandler.TipRegionByKey(rect, "BetterLetters_SnoozedButtonTooltip", end, remaining);
        }

        /// <summary>
        /// Ticks the timer, handling what happens if the letter is invalid or if the timer finishes.
        /// </summary>
        /// <returns>true if the timer is complete or invalid. false if the timer is still running</returns>
        public bool TickIntervalDelta()
        {
            if (this.Letter is null)
            {
                LogPrefixed.Warning("Snooze reference to its letter was lost!");
                Messages.Message(
                    "BetterLetters_SnoozeExpired".Translate(),
                    LookTargets.Invalid,
                    MessageTypeDefOf.RejectInput,
                    historical: false
                );
                return true;
            }
            this._elapsed = GenTicks.TicksGame - this._start;
            if (this.Finished)
            {
                this.Finish();
            }
            return this.Finished;
        }

        public void Finish()
        {
            if (this._pinWhenFinished)
            {
                this.Letter?.Pin();
            }
            else
            {
                Find.LetterStack.ReceiveLetter(this.Letter);
            }
        }
        
        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && this.Letter is null)
            {
                LogPrefixed.Warning("Tried to save a snooze with an expired letter.");
                return;
            }
            Scribe_References.Look<Letter>(ref this.Letter!, "letter", false);
            
            Scribe_Values.Look<int>(ref this._elapsed, "elapsed", 0, false);
            Scribe_Values.Look<int>(ref this._duration, "duration", 0, false);
            Scribe_Values.Look<int>(ref this._start, "start", 0, false);
            Scribe_Values.Look<bool>(ref this._pinWhenFinished, "pinWhenFinished", false, false);
            Scribe_Values.Look<SnoozeTypes>(ref this.SnoozeType, "snoozeType", SnoozeTypes.Letter, false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !this.Finished)
            {
                this._start = GenTicks.TicksGame;
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
    /// <returns>True if the snooze was successfully added (not a duplicate, Letter was not null, etc.)</returns>
    public static bool AddSnooze(Snooze snooze)
    {
        if (snooze.Letter is null)
        {
            LogPrefixed.Warning("Tried to add a snooze with a null letter. Skipping.");
            return false;
        }
        if (NumSnoozes >= MaxNumSnoozes)
        {
            LogPrefixed.Warning("Tried to add a snooze but there are already too many. Skipping.");
            Messages.Message(
                "BetterLetters_TooManySnoozes".Translate(),
                LookTargets.Invalid,
                MessageTypeDefOf.RejectInput,
                historical: false
            );
            return false;
        }
        if (Snoozes.ContainsKey(snooze.Letter))
        {
            LogPrefixed.Warning("Tried to add a snooze for a letter that already has one.");
            return false;
        }
        Snoozes.Add(snooze.Letter, snooze);
        snooze.Letter.Unpin();
        LogPrefixed.Trace("Added snooze for letter " + snooze.Letter.ToString());
        Messages.Message(
            "BetterLetters_SnoozeAdded".Translate(snooze.Duration.ToStringTicksToPeriod()),
            LookTargets.Invalid,
            MessageTypeDefOf.PositiveEvent,
            historical: false
        );
        return true;
    }
    
    /// <summary>
    /// Helper method to create a snooze for a letter and add it to the dictionary.
    /// </summary>
    /// <param name="letter">The letter that will be snoozed</param>
    /// <param name="durationTicks">How long the snooze will last</param>
    /// <param name="pinned">If true, the letter will be automatically pinned when the snooze finishes</param>
    /// <returns>Newly-created instance of the snooze for this letter, or null if it failed.</returns>
    public static Snooze? AddSnooze(Letter letter, int durationTicks, bool pinned = false)
    {
        AddSnooze(new Snooze(letter, durationTicks, Settings.SnoozePinned || pinned));
        
        return Snoozes[letter];
    }

    public static bool RemoveSnooze(Letter? letter)
    {
        if (letter is null)
        {
            LogPrefixed.Warning("Tried to remove a null snooze. Skipping.");
            return false;
        }
        if (Snoozes.Remove(letter))
        {
#if v1_4 || v1_5 || v1_6
            var label = letter?.Label ?? "null";
#elif v1_1 || v1_2 || v1_3
            var label = letter?.label ?? "null";
#endif
            Messages.Message(
                "BetterLetters_SnoozeRemoved".Translate(label),
                LookTargets.Invalid,
                MessageTypeDefOf.PositiveEvent,
                historical: false
            );
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
            if (snooze.Key != null && snooze.Value.TickIntervalDelta())
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
        var snoozeDialog = new Dialog_Snooze(duration =>
        {
            var snooze = AddSnooze(letter, duration);
            onSnooze?.Invoke(snooze);
        });
        Find.WindowStack.Add(snoozeDialog);
    }

    // Used just for reassembling the dictionary when a save is loaded
    private static List<Letter>? _letterList = null;
    private static List<Snooze>? _snoozeList = null;
    public override void ExposeData()
    {
        base.ExposeData();
        
        Scribe_Collections.Look<Letter, Snooze>(
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
                    LogPrefixed.Warning("Found a null letter reference in the snooze dictionary. Removing.");
                    Snoozes.Remove(null!);
                    continue;
                }
                if (Snoozes[letter] == null)
                {
                    LogPrefixed.Warning("Found a null snooze reference for letter " + letter.ToString() + ". Removing.");
                    Snoozes.Remove(letter);
                    continue;
                }
                if (Snoozes[letter].Letter != letter)
                {
                    LogPrefixed.Warning("Found a mismatched snooze reference for letter " + letter.ToString() + ". Removing.");
                    Snoozes.Remove(letter);
                }
            }
            _letterList = null;
            _snoozeList = null;
        }
    }
}