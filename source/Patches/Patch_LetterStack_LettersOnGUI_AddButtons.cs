using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse.Sound;

namespace BetterLetters.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("LetterStack_AddButtons")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_LetterStack_LettersOnGUI_AddButtons
{
    private const float ButtonSize = 24f;

    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.LettersOnGUI))]
    [HarmonyPrefix]
    [UsedImplicitly]
    static void AddButtonsToLetterStack(ref float baseY, LetterStack __instance)
    {
        if (!Settings.AddBulkDismissButton || __instance.LettersListForReading?.Count == 0)
            return;

        var buttonRect = new Rect(UI.screenWidth - ButtonSize - 16f, baseY - 18f, ButtonSize, ButtonSize);

        baseY -= buttonRect.height;

        TooltipHandler.TipRegion(buttonRect, "BetterLetters_LetterStack_FloatMenu_Tooltip".Translate());
        if (Widgets.ButtonImageFitted(buttonRect, Icons.Dismiss, Color.white))
        {
            var floatMenuOptions = new List<FloatMenuOption>
            {
                new("BetterLetters_LetterStack_FloatMenu_DismissUnpinned".Translate(),
                    () =>
                    {
                        var letters = __instance.LettersListForReading!.ListFullCopy()!;
                        foreach (var letter in letters)
                        {
                            if (!letter.IsPinned())
                            {
                                __instance.RemoveLetter(letter);
                            }
                        }
                    },
                    iconTex: Icons.Dismiss, iconColor: Color.white
                ),
                new("BetterLetters_LetterStack_FloatMenu_DismissAll".Translate(),
                    () =>
                    {
                        var letters = __instance.LettersListForReading!.ListFullCopy()!;
                        foreach (var letter in letters)
                        {
                            __instance.RemoveLetter(letter);
                        }
                    },
                    iconTex: Icons.Dismiss, iconColor: Color.white
                ),
                new("BetterLetters_LetterStack_FloatMenu_DismissExpired".Translate(),
                    () =>
                    {
                        var letters = __instance.LettersListForReading!.ListFullCopy()!;
                        foreach (var letter in letters)
                        {
                            if (letter is ChoiceLetter { quest: { } quest })
                            {
                                if (quest.Historical || quest.dismissed)
                                    __instance.RemoveLetter(letter);
                            }
                            if (letter is LetterWithTimeout { TimeoutPassed: true })
                                __instance.RemoveLetter(letter);
                        }
                    },
                    iconTex: Icons.Dismiss, iconColor: Color.white
                ),
                new("BetterLetters_LetterStack_FloatMenu_DismissSnoozed".Translate(),
                    () =>
                    {
                        var letters = __instance.LettersListForReading!.ListFullCopy()!;
                        foreach (var letter in letters)
                        {
                            if (letter.WasEverSnoozed())
                                __instance.RemoveLetter(letter);
                        }
                    },
                    iconTex: Icons.Dismiss, iconColor: Color.white
                ),
                new("BetterLetters_LetterStack_FloatMenu_DismissReminders".Translate(),
                    () =>
                    {
                        var letters = __instance.LettersListForReading!.ListFullCopy()!;
                        foreach (var letter in letters)
                        {
                            if (letter.IsReminder())
                                __instance.RemoveLetter(letter);
                        }
                    },
                    iconTex: Icons.Dismiss, iconColor: Color.white
                ),
                new("BetterLetters_LetterStack_FloatMenu_DismissPositive".Translate(),
                    () =>
                    {
                        var letters = __instance.LettersListForReading!.ListFullCopy()!;
                        foreach (var letter in letters)
                        {
                            if (letter.def!.defName!.Contains("Positive"))
                                __instance.RemoveLetter(letter);
                        }
                    },
                    iconTex: LetterDefOf.PositiveEvent!.Icon!, iconColor: LetterDefOf.PositiveEvent.color
                ),
                new("BetterLetters_LetterStack_FloatMenu_DismissNeutral".Translate(),
                    () =>
                    {
                        var letters = __instance.LettersListForReading!.ListFullCopy()!;
                        foreach (var letter in letters)
                        {
                            if (letter.def!.defName!.Contains("Neutral"))
                                __instance.RemoveLetter(letter);
                        }
                    },
                    iconTex: LetterDefOf.NeutralEvent!.Icon!, iconColor: LetterDefOf.NeutralEvent.color
                ),
                new("BetterLetters_LetterStack_FloatMenu_DismissNegative".Translate(),
                    () =>
                    {
                        var letters = __instance.LettersListForReading!.ListFullCopy()!;
                        foreach (var letter in letters)
                        {
                            if (letter.def!.defName!.Contains("Negative"))
                                __instance.RemoveLetter(letter);
                        }
                    },
                    iconTex: LetterDefOf.NegativeEvent!.Icon!, iconColor: LetterDefOf.NegativeEvent.color
                ),
                new("BetterLetters_LetterStack_FloatMenu_DismissThreatSmall".Translate(),
                    () =>
                    {
                        var letters = __instance.LettersListForReading!.ListFullCopy()!;
                        foreach (var letter in letters)
                        {
                            if (letter.def!.defName!.Contains("ThreatSmall"))
                                __instance.RemoveLetter(letter);
                        }
                    },
                    iconTex: LetterDefOf.ThreatSmall!.Icon!, iconColor: LetterDefOf.ThreatSmall.color
                ),
                new("BetterLetters_LetterStack_FloatMenu_DismissThreatBig".Translate(),
                    () =>
                    {
                        var letters = __instance.LettersListForReading!.ListFullCopy()!;
                        foreach (var letter in letters)
                        {
                            if (letter.def!.defName!.Contains("ThreatBig"))
                                __instance.RemoveLetter(letter);
                        }
                    },
                    iconTex: LetterDefOf.ThreatBig!.Icon!, iconColor: LetterDefOf.ThreatBig.color
                ),
            };
            Find.WindowStack!.Add(new FloatMenu(floatMenuOptions));
            SoundDefOf.FloatMenu_Open!.PlayOneShotOnCamera();
        }
    }
}
