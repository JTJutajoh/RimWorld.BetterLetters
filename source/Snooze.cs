using System;
using System.Diagnostics.CodeAnalysis;
using RimWorld;
using UnityEngine;

namespace BetterLetters;

internal enum SnoozeTypes
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

    internal bool OpenWhenFinished => _openWhenFinished;

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
                WorldComponent_SnoozeManager.NumSnoozes,
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
        if (Letter is null || WorldComponent_SnoozeManager.Snoozes[Letter] is not { } snooze) return;

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

        Letter.UnSnooze(true);
        Find.LetterStack?.ReceiveLetter(Letter);
        if (_pinWhenFinished)
        {
            Letter.Pin(suppressSnoozeCanceledMessage: true);
        }

        if (_openWhenFinished)
        {
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
        Scribe_Values.Look(ref _openWhenFinished, "openWhenFinished", false, false);
        Scribe_Values.Look(ref SnoozeType, "snoozeType", SnoozeTypes.Letter, false);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && !Finished)
        {
            _start = GenTicks.TicksGame;
        }
    }
}
