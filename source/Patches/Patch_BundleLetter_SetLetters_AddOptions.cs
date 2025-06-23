using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;

namespace BetterLetters.Patches;

/// <summary>
/// Simple patch that adds additional options to the float menu that comes up when you click on a <see cref="BundleLetter"/>
/// (The special type of letter when the letter stack is full).
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("BundleLetters")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
internal static class Patch_BundleLetter_SetLetters_AddOptions
{
    [HarmonyPatch(typeof(BundleLetter), nameof(BundleLetter.SetLetters))]
    [HarmonyPrefix]
    [UsedImplicitly]
    static void CacheDidListChange(List<Letter> letters, List<Letter> ___bundledLetters)
    {
        _listChanged = !GenCollection.ListsEqual<Letter>(letters, ___bundledLetters);
    }

    private static bool _listChanged = false;

    /// <summary>
    ///
    /// </summary>
    [HarmonyPatch(typeof(BundleLetter), nameof(BundleLetter.SetLetters))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void AddFloatMenuOptions(
        BundleLetter __instance,
        ref List<Letter> ___bundledLetters,
        List<Letter> letters,
        ref List<FloatMenuOption> ___floatMenuOptions
    )
    {
        // Just like the vanilla method, only run the rest of the method if the list actually changed.
        if (!_listChanged) return;

        var pinnedLetters = new List<Letter>();
        var unpinnedLetters = new List<Letter>();
        // First, find all the pinned letters in the list
        foreach (var letter in ___bundledLetters)
        {
            if (letter.IsPinned())
            {
                pinnedLetters.Add(letter);
            }
            else
            {
                unpinnedLetters.Add(letter);
            }
        }

        if (pinnedLetters.Count > 0)
        {
            // Unpin all pinned letters
            ___floatMenuOptions.Add(new FloatMenuOption(
                "BetterLetters_BundleLetter_UnpinAll".Translate(pinnedLetters.Count),
                () =>
                {
                    foreach (var letter in pinnedLetters)
                        letter.Unpin();
                },
#if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5)
                Icons.Dismiss, ColorLibrary.RedReadable.WithAlpha(0.8f),
#endif
                MenuOptionPriority.High
            ));
            // Unpin & dismiss all pinned letters
            ___floatMenuOptions.Add(new FloatMenuOption(
                "BetterLetters_BundleLetter_UnpinAndDismissAll".Translate(pinnedLetters.Count),
                () =>
                {
                    foreach (var letter in pinnedLetters)
                        letter.Unpin(true);
                },
#if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5)
                Icons.Dismiss, ColorLibrary.RedReadable,
#endif
                MenuOptionPriority.High
            ));
        }

        // Dismiss all unpinned letters
        ___floatMenuOptions.Add(new FloatMenuOption(
            "BetterLetters_BundleLetter_DismissAll".Translate(unpinnedLetters.Count),
            () =>
            {
                foreach (var letter in unpinnedLetters)
                    Find.LetterStack?.RemoveLetter(letter);
            },
#if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5)
            Icons.Dismiss, ColorLibrary.DarkRed,
#endif
            MenuOptionPriority.High
        ));
        // Snooze all unpinned letters
        ___floatMenuOptions.Add(new FloatMenuOption(
            "BetterLetters_BundleLetter_SnoozeAll".Translate(unpinnedLetters.Count),
            () =>
            {
                var snoozeOptions = new List<FloatMenuOption>()
                {
                    LetterUtils.Snooze1HrFloatMenuOption(__instance),
                    LetterUtils.Snooze1DayFloatMenuOption(__instance),
                    LetterUtils.SnoozeDialogFloatMenuOption(__instance)
                };
                Find.WindowStack?.Add(new FloatMenu(snoozeOptions));
            },
#if !(v1_1 || v1_2 || v1_3 || v1_4 || v1_5)
            Icons.Dismiss, ColorLibrary.SkyBlue,
#endif
            MenuOptionPriority.High
        ));
    }
}
