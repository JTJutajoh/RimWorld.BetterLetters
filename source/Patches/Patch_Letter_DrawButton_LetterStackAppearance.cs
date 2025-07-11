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
[StaticConstructorOnStartup]
[HarmonyPatch]
[HarmonyPatchCategory("Letter_DrawInLetterStack")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_Letter_DrawButton_LetterStackAppearance
{
    const float LetterLabelXOffset = 52f;
    const float DecoratorSize = 16f;
    const float HoverRectLeftExpansion = 40f;
    const float VanillaLetterWidth = 38f;

    static Texture2D LetterIcon = null!;
    static bool IsPinned;
    static bool WasEverSnoozed;

    /// <summary>
    /// Before a letter is drawn checks if the letter should have its icon/rect overridden or not.
    /// </summary>
    [HarmonyPatch(typeof(Letter), nameof(Letter.DrawButtonAt))]
    [HarmonyPrefix]
    [UsedImplicitly]
    static void Prefix(Letter __instance)
    {
        if (__instance.IsPinned())
        {
            IsPinned = true;
        }

        if (__instance.WasEverSnoozed())
        {
            WasEverSnoozed = true;
        }

        if (!Settings.EnableLetterAppearancePatches || !Settings.ReplaceLetterIcons)
        {
            LetterIcon = __instance.def!.Icon!;
            return;
        }

        if (__instance.TryGetLetterIcon(out var texture) && texture != null)
        {
            LetterIcon = texture;
            return;
        }


        //MAYBE: Switch this to using the override cache system
        if (__instance is ChoiceLetter { quest: { } quest })
        {
            if (quest.Historical || quest.dismissed)
            {
                LetterIcon = Icons.LetterQuestExpired;
            }
            else
                LetterIcon = quest.State switch
                {
                    QuestState.EndedSuccess => Icons.LetterQuestSuccess,
                    QuestState.Ongoing => Icons.LetterQuestAccepted,
                    QuestState.NotYetAccepted => Icons.LetterQuestAvailable,
                    _ => Icons.LetterQuest
                };
        }
        else
        {
            LetterIcon = __instance.def!.Icon!;
        }
    }

    /// <summary>
    /// Clears the overrides saved by <see cref="Prefix"/>
    /// </summary>
    [HarmonyPatch(typeof(Letter), nameof(Letter.DrawButtonAt))]
    [HarmonyPostfix]
    [UsedImplicitly]
    static void Postfix(Letter __instance)
    {
        ClearOverrides();
    }

    static void ClearOverrides()
    {
        LetterIcon = LetterDefOf.NeutralEvent!.Icon!;
        IsPinned = false;
        WasEverSnoozed = false;
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
    static void OverrideInnerRect(ref Rect rect2, Rect rect)
    {
        // "rect" is the original hard-coded size of vanilla letters
        // calculate the correct rect for the letter's icon based on how much larger it is than the hard-coded letter size in vanilla
        rect2 = rect.ExpandedBy((LetterIcon.width / 2f - rect.width) / 2f,
            (LetterIcon.height / 2f - rect.height) / 2f);
    }

    static Rect ModifyLabelRect(Rect rect, Rect rect2)
    {
        if (!Settings.EnableLetterAppearancePatches || (!Settings.OffsetLetterLabels && !Settings.DoLetterDecorators)) return rect;

        //MAYBE: Only offset if there's a custom icon

        rect.x -= LetterLabelXOffset - Settings.LetterLabelsOffsetAmount;

        if (!Settings.DoLetterDecorators) return rect;

        var hoverRect = new Rect(rect2);
        hoverRect.xMin -= HoverRectLeftExpansion;
        var hovered = Mouse.IsOver(hoverRect);
        if (WasEverSnoozed || IsPinned || hovered)
        {
            rect.x -= DecoratorSize + 2f;
        }

        return rect;
    }

    /// Generates and shows a float menu for pinned letters.
    static void DoPinnedFloatMenu(Letter letter, Rect rect)
    {
        // Checks for right-click first and returns early if not
        if (Event.current?.type != EventType.MouseDown || Event.current.button != 1 || !Mouse.IsOver(rect)) return;

        if (!Settings.EnableRightClickPinnedLetters)
        {
            letter.Unpin();
            return;
        }

        if (IsPinned)
        {
            var floatMenuOptions = new List<FloatMenuOption>();
            floatMenuOptions.Add(FloatMenuFactory.MakeFloatMenuOption(
                "BetterLetters_DismissButStayPinned".Translate(),
                () => { Find.LetterStack?.RemoveLetter(letter); },
                iconTex: Icons.Dismiss,
                iconColor: Color.gray
            ));
            floatMenuOptions.Add(FloatMenuFactory.MakeFloatMenuOption(
                "BetterLetters_Unpin".Translate(),
                () => { letter.Unpin(); },
                iconTex: Icons.PinFloatMenu,
                iconColor: Color.white
            ));
            floatMenuOptions.Add(FloatMenuFactory.MakeFloatMenuOption(
                "BetterLetters_UnpinAndDismiss".Translate(),
                () => { letter.Unpin(true); },
                iconTex: Icons.PinFloatMenu,
                iconColor: ColorLibrary.RedReadable
            ));
            if (letter.lookTargets is not null && letter.lookTargets.Any && letter.lookTargets.IsValid)
            {
                floatMenuOptions.Add(FloatMenuFactory.MakeFloatMenuOption("JumpToLocation".Translate(),
                    () => { CameraJumper.TryJumpAndSelect(letter.lookTargets.PrimaryTarget); },
                    iconTex: letter.lookTargets.PrimaryTarget.Thing?.def?.uiIcon!, iconColor: Color.white
                ));
            }

            floatMenuOptions.AddRange(FloatMenuFactory.SnoozeFloatMenuOptions(letter));

            Find.WindowStack?.Add(new FloatMenu(floatMenuOptions));
            SoundDefOf.FloatMenu_Open!.PlayOneShotOnCamera();
            Event.current.Use();
        }
        else
        {
            // Right-click functionality for NOT pinned letters would go here in the future
        }
    }

    static void DoLetterDecorators(Letter letter, Rect rect2)
    {
        if (!Settings.EnableLetterAppearancePatches || !Settings.DoLetterDecorators) return;

        var decoratorColumnRect = new Rect(
            rect2.center.x - VanillaLetterWidth / 2f - DecoratorSize - 2f,
            rect2.center.y - DecoratorSize,
            DecoratorSize, DecoratorSize * 2f
        );
        var topButtonRect = decoratorColumnRect.TopHalf();
        var bottomButtonRect = decoratorColumnRect.BottomHalf();

        var hoverRect = new Rect(rect2);
        hoverRect.xMin -= HoverRectLeftExpansion;
        var hovered = Mouse.IsOver(hoverRect);

        if (WasEverSnoozed || hovered)
        {
            CustomWidgets.SnoozeIconButton(letter, topButtonRect);
        }

        if (IsPinned || hovered)
        {
            CustomWidgets.PinIconButton(letter, bottomButtonRect);
        }
    }

    #region Transpiler

    static readonly MethodInfo? AnchorMethodButtonInvisible =
        typeof(Widgets).GetMethod(nameof(Widgets.ButtonInvisible));

    static readonly MethodInfo? LetterDefIconGetterAnchor =
        typeof(LetterDef).GetProperty("Icon")?.GetGetMethod();

    static readonly ConstructorInfo? RectConstructorAnchor =
        typeof(Rect).GetConstructor(new[] { typeof(Rect) });

    static readonly FieldInfo? GrayTextBGFieldAnchor =
        typeof(TexUI).GetField(nameof(TexUI.GrayTextBG));

    private static readonly MethodInfo? TextWordWrapSetAnchor =
        typeof(Text).PropertySetter(nameof(Text.WordWrap));

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
        if (RectConstructorAnchor == null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(RectConstructorAnchor)} method for {nameof(Patch_Letter_DrawButton_LetterStackAppearance)}.{MethodBase.GetCurrentMethod()} patch");
        if (GrayTextBGFieldAnchor == null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(GrayTextBGFieldAnchor)} method for {nameof(Patch_Letter_DrawButton_LetterStackAppearance)}.{MethodBase.GetCurrentMethod()} patch");
        if (TextWordWrapSetAnchor == null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(TextWordWrapSetAnchor)} method for {nameof(Patch_Letter_DrawButton_LetterStackAppearance)}.{MethodBase.GetCurrentMethod()} patch");

        var codes = new List<CodeInstruction>(instructions);

        var hasInjectedRectOverride = false;
        var hasInjectedTextureOverride = false;
        var hasInjectedLabelOverride = false;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (int i = 0; i < codes.Count; i++)
        {
            // PATCH 1:
            // Altering the Letter icon Rect
            if (!hasInjectedRectOverride && codes[i]!.opcode == OpCodes.Call &&
                (ConstructorInfo?)codes[i]!.operand == RectConstructorAnchor)
            {
                // About to construct the second rect
                // address of first rect is on the stack
                // Intercept constructor and return a different rect instead
                yield return CodeInstruction.Call(typeof(Patch_Letter_DrawButton_LetterStackAppearance),
                    nameof(OverrideInnerRect))!;

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
                    "LetterIcon")!;

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

            // PATCH 6:
            // Offsetting the label
            // Offsetting the label BG first
            if (codes[i]!.LoadsField(GrayTextBGFieldAnchor))
            {
                yield return CodeInstruction.LoadLocal(2)!;
                yield return CodeInstruction.Call(typeof(Patch_Letter_DrawButton_LetterStackAppearance),
                    nameof(ModifyLabelRect))!;
            }

            // Offsetting the label itself next
            if (!hasInjectedLabelOverride && i + 2 < codes.Count && codes[i + 1]!.Calls(TextWordWrapSetAnchor))
            {
                yield return CodeInstruction.LoadLocal(2)!;
                yield return CodeInstruction.Call(typeof(Patch_Letter_DrawButton_LetterStackAppearance),
                    nameof(ModifyLabelRect))!;

                // Text.WordWrap is set twice, so make sure to only inject before the first time
                hasInjectedLabelOverride = true;
            }

            // Draw the extra buttons
            if (codes[i]!.opcode == OpCodes.Ret)
            {
                // Right before the end of the method, inject
                yield return CodeInstruction.LoadArgument(0)!.MoveLabelsFrom(codes[i]!)!;
                yield return CodeInstruction.LoadLocal(2)!;
                yield return CodeInstruction.Call(typeof(Patch_Letter_DrawButton_LetterStackAppearance),
                    nameof(DoLetterDecorators))!;
            }

            // Emitting the original IL instruction
            yield return codes[i]!;
        }
    }

    #endregion Transpiler
}
