using System;
using System.Collections.Generic;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;

namespace BetterLetters;

[UsedImplicitly]
internal class WorldComponent_SnoozeManager : WorldComponent
{
    private const int MaxSnoozeCount = 250;
    public static WorldComponent_SnoozeManager? Instance { get; private set; }

    public static int MaxNumSnoozes => Settings.MaxNumSnoozes;
    public static int NumSnoozes => Snoozes.Count;

    public WorldComponent_SnoozeManager(World world) : base(world)
    {
        Instance = this;
    }

    private static Dictionary<Letter?, Snooze> _snoozes = new();
    internal static Dictionary<Letter?, Snooze> Snoozes => _snoozes;

    internal static HashSet<int> AllSnoozesSeen = new();
    internal static HashSet<int> AllRemindersSeen = new();

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
                letter.Snooze(snooze.Duration, snooze.PinWhenFinished);
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

        var hash = snooze.Letter.ID;
        if (snooze.SnoozeType == SnoozeTypes.Reminder)
        {
            AllRemindersSeen.Add(hash);
        }
        else
        {
            AllSnoozesSeen.Add(hash);
        }

        Settings.TryCacheSnoozeDuration(snooze);

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
    public static Snooze? AddSnooze(Letter letter, int durationTicks, bool? pinWhenFinished = null,
        bool? openWhenFinished = null)
    {
        var success = AddSnooze(new Snooze(letter, durationTicks, pinWhenFinished ?? Settings.SnoozePinned,
            openWhenFinished ?? Settings.SnoozeOpen));
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
        var remainingTicks = letter.RemainingTicks();
        if (remainingTicks > 0)
        {
            maxDurationOverride = remainingTicks;
        }

        var snoozeDialog = new Dialog_Snooze((duration, pinWhenFinished, openWhenFinished) =>
            {
                var snooze = AddSnooze(letter, duration, pinWhenFinished, openWhenFinished);
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

        Scribe_Collections.Look(ref AllSnoozesSeen, "AllSnoozesSeen", LookMode.Value);
        Scribe_Collections.Look(ref AllRemindersSeen, "AllRemindersSeen", LookMode.Value);

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

            if (AllSnoozesSeen?.Count > MaxSnoozeCount)
                Log.Warning("Loaded many snoozes from save file.");
            if (AllRemindersSeen?.Count > MaxSnoozeCount)
                Log.Warning("Loaded many reminders from save file.");

            AllSnoozesSeen ??= new HashSet<int>();
            AllRemindersSeen ??= new HashSet<int>();
        }
    }
}
