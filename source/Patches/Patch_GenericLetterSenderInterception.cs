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
    static void Prefix(MethodBase __originalMethod, IncidentDef def)
    {
        if (!LetterIconOverrides.TryGetIconOverrideDefForDef(def, out _))
        {
            Log.Trace(
                $"No generic override found for incident def \"{def.defName}\"in LetterIconOverrides.DefLetterIconOverrides");
        }
        else
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
    static void Prefix(IncidentDef? ___def)
    {
        if (!LetterIconOverrides.TryGetIconOverrideDefForDef(___def, out _))
        {
            Log.Trace(
                $"No generic override found for incident def \"{___def?.defName}\"in LetterIconOverrides.DefLetterIconOverrides");
        }
        else
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

[HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
[HarmonyPatchCategory("LetterIconCaching")]
[SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class InteractionGenericLetterPatch
{
    [UsedImplicitly]
    static void Prefix(InteractionDef? intDef)
    {
        if (!LetterIconOverrides.TryGetIconOverrideDefForDef(intDef, out _))
        {
            Log.Trace(
                $"No generic override found for interaction def \"{intDef?.defName}\"in LetterIconOverrides.DefLetterIconOverrides");
        }
        else
            LetterIconOverrides.MostRecentLetter = null;
    }

    [UsedImplicitly]
    static void Postfix(bool __result, Pawn_InteractionsTracker __instance, Pawn recipient, InteractionDef? intDef)
    {
        if (!__result || intDef is null) return;

        LetterIconOverrides.TryOverrideIconForDef(intDef, __instance, recipient, intDef);
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
        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
        var instanceType = originalMethod.DeclaringType;
        // Make room for the "this" parameter
        if (!originalMethod.IsStatic)
        {
            // Expand the parameterTypes array to make room for the "this" reference at the start if it's an instance method
            Array.Resize(ref parameterTypes!, parameterTypes.Length + 1);
            Array.Copy(parameterTypes, 0, parameterTypes, 1, parameterTypes.Length - 1);
            parameterTypes[0] = instanceType;
        }

        var paramsLength = parameterTypes.Length;

        Log.Trace(
            $"\tSearching in {originalMethod.DeclaringType!.Name}.{originalMethod.Name} for letter sending method...");
        codeMatcher.Start()!.SearchForward(CallsLetterSendingMethod);

        if (!codeMatcher.IsValid)
        {
            Log.Warning(
                $"Failed to find any letter sending methods in {originalMethod.DeclaringType!.Name}.{originalMethod.Name}.");
            return codeMatcher.Instructions()!;
        }

        Log.Trace("\tFound letter sending method, inserting patch");

        codeMatcher.InsertAndAdvance(
                CodeInstruction.Call(typeof(LetterIconOverrides),
                    nameof(LetterIconOverrides.ClearMostRecentLetter))!)!
            .Advance(1);

        // Get the current running method (will be the patched version)
        codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, MethodBaseGetCurrentMethodMethodInfo));

        // Create an array with a length that matches the length of parameters
        Log.Trace($"\tInserting parameters array with {paramsLength} params");
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

            var paramType = parameterTypes[paramIndex];
            Log.Trace($"\t\tParameter {paramIndex}: {paramType.Name}");


            if (paramType.ContainsGenericParameters)
            {
                Log.Warning($"\t\tGeneric parameter type, won't work");
            }

            if (paramType.IsValueType)
            {
                Log.Trace($"\t\t\tParam is value type, boxing type {paramType}");
                codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Box, paramType));
            }

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
            if (originalMethod is not null)
                Log.Error(
                    $"Failed patching {originalMethod.DeclaringType!.Name}.{originalMethod.Name}({originalMethod.GetParameters().Join(p => p.ParameterType.Name, ", ")})");
            else
                Log.Error("Failed patching unknown method.");

            PatchManager.Notify_Patched(originalMethod, 1, numFailed: 1);
            // Log.Exception(exception, $"Patching {originalMethod.DeclaringType!.Name}.{originalMethod.Name} failed.");
        }

        Log.Trace($"Patching {originalMethod?.DeclaringType!.Name}.{originalMethod?.Name} complete");

        PatchManager.Notify_Patched(originalMethod, 1);

        return exception!;
    }
}
