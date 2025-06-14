using System;
using System.Collections.Generic;
using DarkLog;
using RimWorld;
using RimWorld.Planet;
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

    public class Snooze : IExposable
    {
        public bool Finished => _elapsed >= _duration;
        public int RemainingTicks => _duration - _elapsed;
        
        private int _start;
        private int _duration;
        public int Duration => _duration;
        private int _elapsed;
        public Letter? Letter;
        private bool _pinned;

        public Snooze()
        {
            LogPrefixed.Trace("Creating new empty snooze data");
            // Empty constructor for scribe.
            // This is only used when loading existing save files.
        }
        
        public Snooze(Letter letter, int durationTicks, bool pinned = false)
        {
            LogPrefixed.Trace("Creating new snooze");
            this.Letter = letter;
            this._duration = durationTicks;
            this._start = GenTicks.TicksGame;
            this._elapsed = 0;
            this._pinned = pinned;
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
                SnoozeManager.Snoozes.Remove(null!);
                return true;
            }
            this._elapsed = GenTicks.TicksGame - this._start;
            if (this.Finished)
            {
                if (this._pinned)
                {
                    this.Letter.Pin();
                }
                else
                {
                    Find.LetterStack.ReceiveLetter(this.Letter);
                }
            }
            return this.Finished;
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
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !this.Finished)
            {
                this._start = GenTicks.TicksGame;
            }
        }
    }

    private static Dictionary<Letter?, Snooze> _snoozes = new();
    public static Dictionary<Letter?, Snooze> Snoozes => _snoozes;

    public static Snooze? AddSnooze(Letter letter, int durationTicks, bool pinned = false)
    {
        if (NumSnoozes >= MaxNumSnoozes)
        {
            LogPrefixed.Warning("Tried to add a snooze but there are already too many. Skipping.");
            Messages.Message(
                "BetterLetters_TooManySnoozes".Translate(),
                LookTargets.Invalid,
                MessageTypeDefOf.RejectInput,
                historical: false
            );
            return null;
        }
        if (Snoozes.ContainsKey(letter))
        {
            LogPrefixed.Warning("Tried to add a snooze for a letter that already has one.");
            return null;
        }
        Snoozes.Add(letter, new Snooze(letter, durationTicks, pinned));
        LogPrefixed.Trace("Added snooze for letter " + letter.ToString());
        Messages.Message(
            "BetterLetters_SnoozeAdded".Translate(durationTicks.ToStringTicksToPeriod()),
            LookTargets.Invalid,
            MessageTypeDefOf.PositiveEvent,
            historical: false
        );
        
        return Snoozes[letter];
    }

    public static void RemoveSnooze(Letter? letter)
    {
        if (letter is null)
        {
            LogPrefixed.Warning("Tried to remove a null snooze. Skipping.");
            return;
        }
        Messages.Message(
            "BetterLetters_SnoozeRemoved".Translate(letter?.Label ?? "null"),
            LookTargets.Invalid,
            MessageTypeDefOf.PositiveEvent,
            historical: false
        );
        Snoozes.Remove(letter);
    }
    
    public override void WorldComponentTick()
    {
        base.WorldComponentTick();
        
        var expiredSnoozes = new List<Letter?>();
        foreach (var snooze in Snoozes)
        {
            if (snooze.Value.TickIntervalDelta())
            {
                expiredSnoozes.Add(snooze.Key);
            }
        }
        
        // Iterate a second time to remove expired snoozes, since we can't modify the list while iterating over it the first time.
        foreach (var letter in expiredSnoozes)
        {
            Snoozes.Remove(letter!);
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
            // onSnooze?.Invoke(duration.ToStringTicksToPeriodVague(vagueMin: false));
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