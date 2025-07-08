using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse.Sound;

namespace BetterLetters.Patches;

/// <summary>
/// Patches that modify how letters are drawn in the letter stack if they're pinned.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Letter_DrawInLetterStack")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_Letter_DrawButton_LetterStackAppearance
{
    // Not using a nullable type because the raw field is used in emitted ILs. The prefix that sets it to a non-null value
    // is basically guaranteed to run before it is used, so it should be fine.
    static Texture2D LetterIconTexture = null!;

    /// <summary>
    /// Before a letter is drawn checks if the letter should have its icon/rect overridden or not.
    /// </summary>
    [HarmonyPatch(typeof(Letter), nameof(Letter.DrawButtonAt))]
    [HarmonyPrefix]
    [UsedImplicitly]
    static void SetupLetterOverrides(Letter __instance)
    {
        if (!Settings.ReplaceLetterIconsInLetterStack)
        {
            LetterIconTexture = __instance.def!.Icon!;
            return;
        }

        if (__instance is ChoiceLetter { quest: { } quest })
        {
            if (__instance.IsPinned())
            {
                LetterIconTexture = Icons.LetterQuestPinned;
            }
            else if (quest.Historical || quest.dismissed)
            {
                LetterIconTexture = Icons.LetterQuestExpired;
            }
            else if (quest.GetQuestLetter()?.WasEverSnoozed() ?? false)
            {
                LetterIconTexture = Icons.LetterQuestSnoozed;
            }
            else
                LetterIconTexture = quest.State switch
                {
                    QuestState.EndedSuccess => Icons.LetterQuestSuccess,
                    QuestState.Ongoing => Icons.LetterQuestAccepted,
                    QuestState.NotYetAccepted => Icons.LetterQuestAvailable,
                    _ => Icons.LetterQuest
                };
        }
        else if (__instance.IsPinned())
        {
            LetterIconTexture = Icons.LetterPinned;
        }
        else if (__instance.IsReminder())
        {
            LetterIconTexture = Icons.LetterReminder;
        }
        else if (__instance.WasEverSnoozed())
        {
            LetterIconTexture = Icons.LetterSnoozed;
        }
        //TODO: Other types of letter icon overrides
        else
        {
            LetterIconTexture = __instance.def!.Icon!;
        }
    }

    /// <summary>
    /// Clears the overrides saved by <see cref="SetupLetterOverrides"/>
    /// </summary>
    [HarmonyPatch(typeof(Letter), nameof(Letter.DrawButtonAt))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void ClearOverrides()
    {
        LetterIconTexture = LetterDefOf.NeutralEvent!.Icon!;
    }

    static bool OverrideBounce(LetterDef def, Letter letter)
    {
        // If pinned, override the bounce field. Otherwise, return whatever the original field was (or false if DisableBounceAlways is true)
        return
            !Settings.DisableBounceAlways && def.bounce &&
            !(Settings.DisableBounceIfPinned && letter.IsPinned());
    }

    static float OverrideFlash(LetterDef def, Letter letter)
    {
        // If pinned, override with 0, which disables flashing. Otherwise, return whatever the original field was (or false if DisableFlashAlways is true)
        return
            Settings.DisableFlashAlways || (!Settings.DisableFlashIfPinned || letter.IsPinned())
                ? 0f
                : def.flashInterval;
    }

    // ReSharper disable once RedundantAssignment
    static void ModifyLetterIconRect(ref Rect rect2, Rect rect)
    {
        rect2 = new Rect(rect);

        // "rect" is the original hard-coded size of vanilla letters
        // calculate the correct rect for the letter's icon based on how much larger it is than the hard-coded letter size in vanilla
        rect2 = rect.ExpandedBy((LetterIconTexture.width / 2f - rect.width) / 2f,
            (LetterIconTexture.height / 2f - rect.height) / 2f);
    }

    /// Generates and shows a float menu for pinned letters.
    static void DoPinnedFloatMenu(Letter letter, Rect rect)
    {
        // Checks for right-click first and returns early if not
        if (Event.current?.type != EventType.MouseDown || Event.current.button != 1 || !Mouse.IsOver(rect)) return;

        if (Settings.DisableRightClickPinnedLetters)
        {
            letter.Unpin();
            return;
        }

        if (letter.IsPinned())
        {
            var floatMenuOptions = new List<FloatMenuOption>();
            // Unpin option is first in the list so it's under the player's mouse after they right click, meaning you can still do the vanilla behavior of spamming right click to remove all letters
            floatMenuOptions.Add(LetterUtils.MakeFloatMenuOption(
                "BetterLetters_Unpin".Translate(),
                () => { letter.Unpin(); },
                iconTex: Icons.Dismiss,
                iconColor: Color.white
            ));
            floatMenuOptions.Add(LetterUtils.MakeFloatMenuOption(
                "BetterLetters_UnpinAndDismiss".Translate(),
                () => { letter.Unpin(true); },
                iconTex: Icons.Dismiss,
                iconColor: ColorLibrary.RedReadable
            ));
            floatMenuOptions.Add(LetterUtils.MakeFloatMenuOption(
                "BetterLetters_DismissButStayPinned".Translate(),
                () => { Find.LetterStack?.RemoveLetter(letter); },
                iconTex: Icons.Dismiss,
                iconColor: Color.gray
            ));
            floatMenuOptions.Add(LetterUtils.Snooze1HrFloatMenuOption(letter));
            floatMenuOptions.Add(LetterUtils.Snooze1DayFloatMenuOption(letter));
            floatMenuOptions.AddRange(LetterUtils.RecentSnoozeDurationsFloatMenuOptions(letter));
            floatMenuOptions.Add(LetterUtils.SnoozeDialogFloatMenuOption(letter));

            Find.WindowStack?.Add(new FloatMenu(floatMenuOptions));
            SoundDefOf.FloatMenu_Open!.PlayOneShotOnCamera();
            Event.current.Use();
        }
        else
        {
            // Right-click functionality for NOT pinned letters would go here in the future
        }
    }

    #region Transpiler

    static readonly MethodInfo? AnchorMethodButtonInvisible =
        typeof(Widgets).GetMethod(nameof(Widgets.ButtonInvisible));

    static readonly MethodInfo? LetterDefIconGetterAnchor =
        typeof(LetterDef).GetProperty("Icon")?.GetGetMethod();

    static readonly ConstructorInfo? RectConstructor =
        typeof(Rect).GetConstructor(new[] { typeof(Rect) });

    /// Patch for moving the texture, label, and altering right-click behavior
    [HarmonyPatch(typeof(Letter), nameof(Letter.DrawButtonAt))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    static IEnumerable<CodeInstruction> DrawButtonAt_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        if (AnchorMethodButtonInvisible == null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(AnchorMethodButtonInvisible)} method for {nameof(Patch_Letter_DrawButton_LetterStackAppearance)}.{MethodBase.GetCurrentMethod()} patch");
        if (LetterDefIconGetterAnchor == null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(LetterDefIconGetterAnchor)} method for {nameof(Patch_Letter_DrawButton_LetterStackAppearance)}.{MethodBase.GetCurrentMethod()} patch");
        if (RectConstructor == null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(RectConstructor)} method for {nameof(Patch_Letter_DrawButton_LetterStackAppearance)}.{MethodBase.GetCurrentMethod()} patch");

        var codes = new List<CodeInstruction>(instructions);

        var hasInjectedRectOverride = false;
        var hasInjectedTextureOverride = false;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (int i = 0; i < codes.Count; i++)
        {
            // PATCH 1:
            // Altering the Letter icon Rect
            if (!hasInjectedRectOverride && codes[i]!.opcode == OpCodes.Call &&
                (ConstructorInfo?)codes[i]!.operand == RectConstructor)
            {
                // About to construct the second rect
                // address of first rect is on the stack
                // Intercept constructor and return a different rect instead
                yield return CodeInstruction.Call(typeof(Patch_Letter_DrawButton_LetterStackAppearance),
                    nameof(ModifyLetterIconRect))!;

                hasInjectedRectOverride = true;
                // Skip over the original rect constructor
                continue;
            }

            // PATCH 2:
            // Overriding the LetterDef icon
            if (!hasInjectedTextureOverride && i + 3 < codes.Count && codes[i + 2]!.Calls(LetterDefIconGetterAnchor))
            {
                // About to load Letter.def.Icon
                // "this" Letter instance on stack
                yield return CodeInstruction.LoadField(typeof(Patch_Letter_DrawButton_LetterStackAppearance),
                    "LetterIconTexture")!;

                hasInjectedTextureOverride = true;
                // Skip over the original call to Letter.def.Icon
                i += 3;
            }

            // PATCH 3:
            // Altering right-click behavior
            if (codes[i]!.Calls(AnchorMethodButtonInvisible))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                yield return new CodeInstruction(OpCodes.Ldloc_1); // Load a reference to "rect" local variable)
                yield return CodeInstruction.Call(typeof(Patch_Letter_DrawButton_LetterStackAppearance),
                    nameof(DoPinnedFloatMenu))!;
            }

            // PATCH 4:
            // Disabling bouncing
            if (codes[i]!.opcode == OpCodes.Ldfld &&
                (FieldInfo?)codes[i]!.operand == typeof(LetterDef).GetField("bounce"))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                // Replace the original LetterDef.bounce getter
                yield return CodeInstruction.CallClosure<Func<LetterDef, Letter, bool>>(OverrideBounce)!;
                continue; // Skip over the original getter
            }

            // PATCH 5:
            // Disabling flashing
            if (codes[i]!.opcode == OpCodes.Ldfld &&
                (FieldInfo?)codes[i]!.operand == typeof(LetterDef).GetField("flashInterval"))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0); // Load a "this" reference
                // Replace the original LetterDef.flashInterval getter
                yield return CodeInstruction.CallClosure<Func<LetterDef, Letter, float>>(OverrideFlash)!;
                continue; // Skip over the original getter
            }

            // Emitting the original IL instruction
            yield return codes[i]!;
        }
    }

    #endregion Transpiler
}
