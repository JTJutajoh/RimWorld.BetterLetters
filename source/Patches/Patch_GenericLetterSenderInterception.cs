using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;

namespace BetterLetters.Patches;

[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.SendIncidentLetter))]
[HarmonyPatchCategory("LetterIconCaching")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class IncidentGenericLetterPatch
{
    [UsedImplicitly]
    static void Prefix(MethodBase __originalMethod)
    {
        LetterIconOverrides.MostRecentLetter = null;
    }

    [UsedImplicitly]
    static void Postfix(IncidentDef def, IncidentParms parms)
    {
        if (def is null) return;

        LetterIconOverrides.TryOverrideIconForDef(def);
    }
}

[HarmonyPatch(typeof(IncidentWorker_MakeGameCondition), "TryExecuteWorker")]
[HarmonyPatchCategory("LetterIconCaching")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class GameConditionGenericLetterPatch
{
    [UsedImplicitly]
    static void Prefix()
    {
        LetterIconOverrides.MostRecentLetter = null;
    }

    [UsedImplicitly]
    static void Postfix(bool __result, IncidentParms parms, IncidentDef? ___def)
    {
        if (!__result || (___def?.letterDef is null && ___def?.gameCondition?.letterDef is null)) return;

        if (___def.letterDef is null) return;

        LetterIconOverrides.TryOverrideIconForDef(___def.gameCondition);
    }
}

/// <summary>
/// Generic patch applied to any method that sends a letter.<br />
/// Manually patched by <see cref="LetterIconOverrides"/> at static construction time based on
/// <see cref="LetterIconOverrideDef"/> optional <see cref="LetterIconOverrideDef.patchTargets"/> field.
/// </summary>
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class Patch_GenericLetterSenderInterception
{
    /// <summary>
    /// Used if <see cref="LetterSendingMethodName"/> is not specified (or not found).<br />
    /// A list of default methods to look for when injecting ILs to intercept newly-received letters.
    /// Mostly just contains all the overloads of <see cref="LetterStack.ReceiveLetter(Letter, string, int, bool)"/>
    /// </summary>
    private static readonly List<MethodInfo> DefaultLetterSendingMethods = new()
    {
        AccessTools.Method(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
            new[]
            {
                typeof(TaggedString), typeof(TaggedString), typeof(LetterDef), typeof(LookTargets), typeof(Faction),
                typeof(Quest), typeof(List<ThingDef>), typeof(string), typeof(int), typeof(bool)
            }),
        AccessTools.Method(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
            new[]
            {
                typeof(TaggedString), typeof(TaggedString), typeof(LetterDef), typeof(string), typeof(int),
                typeof(bool)
            }),
        AccessTools.Method(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
            new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) }),
    };

    internal static string? LetterSendingMethodName;

    private static bool CallsLetterSendingMethod(CodeInstruction instruction)
    {
        if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) return false;

        return instruction.operand?.ToString().Contains(LetterSendingMethodName ?? "ReceiveLetter") ??
               DefaultLetterSendingMethods.Select(instruction.Calls).FirstOrDefault();
    }

    private static readonly MethodInfo? MethodBaseGetCurrentMethodMethodInfo =
        AccessTools.Method(typeof(MethodBase), nameof(MethodBase.GetCurrentMethod));

    [UsedImplicitly]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase originalMethod)
    {
        Log.Trace($"Auto-transpiling {originalMethod.DeclaringType!.Name}.{originalMethod.Name}...");

        if (MethodBaseGetCurrentMethodMethodInfo is null)
            throw new InvalidOperationException(
                $"Couldn't find {nameof(MethodBaseGetCurrentMethodMethodInfo)} method for {nameof(Patch_GenericLetterSenderInterception)}.{MethodBase.GetCurrentMethod()} patch");

        var codeMatcher = new CodeMatcher(instructions);
        var parameters = originalMethod.GetParameters();
        var paramsLength = parameters.Length;
        if (!originalMethod.IsStatic) paramsLength++;

        Log.Trace(
            $"Searching in {originalMethod.DeclaringType!.Name}.{originalMethod.Name} for letter sending method...");
        codeMatcher.Start()!.SearchForward(CallsLetterSendingMethod);

        if (!codeMatcher.IsValid)
        {
            Log.Warning(
                $"Failed to find any letter sending methods in {originalMethod.DeclaringType!.Name}.{originalMethod.Name}.");
            return codeMatcher.Instructions()!;
        }

        codeMatcher.InsertAndAdvance(
                CodeInstruction.Call(typeof(LetterIconOverrides),
                    nameof(LetterIconOverrides.ClearMostRecentLetter))!)!
            .Advance(1);

        // Get the current running method (will be the patched version)
        codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, MethodBaseGetCurrentMethodMethodInfo));

        // Create an array with a length that matches the length of parameters
        codeMatcher.InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldc_I4, paramsLength),
            new CodeInstruction(OpCodes.Newarr, typeof(object))
        );
        // Iterate over all the parameters that were passed to the running method and insert them into the object[] array
        for (int paramIndex = 0; paramIndex < paramsLength; paramIndex++)
        {
            // Duplicate reference to the array
            codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Dup));
            // Push the current index
            codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4, paramIndex));
            // Load the parameter value
            codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg, paramIndex));
            // Store value in array
            codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Stelem_Ref));
        }

        // Call the postfix method, sending the currently running method and all arguments passed to it
        codeMatcher.InsertAndAdvance(CodeInstruction.Call(typeof(Patch_GenericLetterSenderInterception),
            nameof(InterceptLetter))!);

        //TODO: Figure out how to use CodeMatcher.Repeat()

        return codeMatcher.Instructions()!;
    }

    static void InterceptLetter(MethodBase __originalMethod, params object[] context)
    {
        if (Harmony.GetOriginalMethod((MethodInfo)__originalMethod) is MethodInfo method &&
            LetterIconOverrides.GenericPatchedOverrideMethods.TryGetValue(method, out var def) &&
            def is not null)
        {
            LetterIconOverrides.TryOverrideMostRecentLetterIcon(def, context);
            return;
        }

        Log.WarningOnce(
            $"Letter Icon Override postfix for {__originalMethod.DeclaringType!.Name}.{__originalMethod.Name} was not found in LetterIconOverrides.GenericPatchedOverrideMethods. This likely indicates a misconfigured patchTargets in a LetterIconOverrideDef.",
            __originalMethod.GetHashCode().ToString());
    }

    [UsedImplicitly]
    internal static Exception Cleanup(MethodBase originalMethod, Exception? exception)
    {
        LetterSendingMethodName = null;

        if (exception is not null)
        {
            PatchManager.Notify_Patched(originalMethod, 1, numFailed: 1);
            Log.Exception(exception, $"Patching {originalMethod.DeclaringType!.Name}.{originalMethod.Name} failed.");
        }

        PatchManager.Notify_Patched(originalMethod, 1);

        return exception!;
    }
}
