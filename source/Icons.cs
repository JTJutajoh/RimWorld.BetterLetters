using System;
using UnityEngine;

namespace BetterLetters;

// ReSharper disable AssignNullToNotNullAttribute
[StaticConstructorOnStartup]
internal static class Icons
{
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

    internal static readonly Texture2D Gear = ContentFinder<Texture2D>.Get("UI/Icons/Gear");

    [Obsolete] internal static readonly Texture2D LetterPlaceholder = ContentFinder<Texture2D>.Get("UI/Letters/LetterPLACEHOLDER");
    internal static readonly Texture2D LetterQuest = ContentFinder<Texture2D>.Get("UI/Letters/LetterQuest");
    internal static readonly Texture2D LetterQuestAvailable = ContentFinder<Texture2D>.Get("UI/Letters/LetterQuestAvailable");
    internal static readonly Texture2D LetterQuestExpired = ContentFinder<Texture2D>.Get("UI/Letters/LetterQuestExpired");
    internal static readonly Texture2D LetterQuestAccepted = ContentFinder<Texture2D>.Get("UI/Letters/LetterQuestAccepted");
    internal static readonly Texture2D LetterQuestSuccess = ContentFinder<Texture2D>.Get("UI/Letters/LetterQuestAccepted");
}
