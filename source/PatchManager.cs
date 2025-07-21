using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace BetterLetters;

/// <summary>
///     Helper class for all Harmony patching functionality.
/// </summary>
[StaticConstructorOnStartup]
[UsedImplicitly]
internal static class PatchManager
{
    internal static readonly Harmony Harmony;

    internal static List<string> AllPatchCategories = new();

    private static int _loadedPatches;
    private static int _failedPatches;
    private static int _skippedPatches;
    private static List<string> _allEnabledSuccessfulPatches = new();

    static PatchManager()
    {
        Harmony = new Harmony(BetterLettersMod.Instance!.Content!.PackageId!);

        Log.Message("Running Harmony patches...");

        try
        {
            PatchAll();
        }
        catch (Exception e)
        {
            Log.Exception(e,
                "Error doing Harmony patches. This likely means either the wrong game version or a hard incompatibility with another mod.");
        }
    }

    internal static void Notify_PatchingComplete()
    {
        var totalPatches = _loadedPatches + _failedPatches + _skippedPatches;
        Log.Message($"{_loadedPatches}/{totalPatches} Harmony patches successful.");
        if (_skippedPatches > 0)
            Log.Message($"{_skippedPatches}/{totalPatches} Harmony patches skipped.");
        if (_failedPatches > 0)
            Log.Warning(
                $"{_failedPatches}/{totalPatches} Harmony patches failed! The mod/game might behave in undesirable ways.");
    }

    private static void PatchAll()
    {
        AllPatchCategories = Assembly.GetExecutingAssembly()
            .GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(HarmonyPatchCategory), true)
                .Cast<HarmonyPatchCategory>()
                .Select(attr => attr.info?.category))
            .Where(category => !string.IsNullOrEmpty(category!))
            .Distinct()
            .ToList()!;

        foreach (var category in AllPatchCategories)
        {
            PatchCategory(category!);
        }
    }

    internal static void RepatchAll()
    {
        Log.Warning("Attempting to unpatch and re-patch selected Harmony patches...");
        foreach (var patch in Settings.DisabledPatchCategories)
        {
            UnpatchCategory(patch);
        }

        foreach (var patch in AllPatchCategories)
        {
            PatchCategory(patch);
        }

        Log.Warning(
            "Re-patching complete. Game restart is still recommended, especially if there were any warnings or errors.");
    }

    /// <summary>
    ///     Wrapper for <see cref="Harmony" />.<see cref="Harmony.PatchCategory(string)" /> that logs any errors that occur and
    ///     skips patches that are disabled in the mod's configs.
    /// </summary>
    /// <param name="category">
    ///     Name of the category to pass to <see cref="Harmony" />.
    ///     <see cref="Harmony.PatchCategory(string)" />
    /// </param>
    private static void PatchCategory(string category)
    {
        if (_allEnabledSuccessfulPatches.Contains(category))
            return;

        var patchTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttributes(typeof(HarmonyPatchCategory), true)
                .Cast<HarmonyPatchCategory>()
                .Any(attr => attr.info?.category == category))
            .ToList();

        // Find any classes in the assembly with a [HarmonyPatchCategory] attribute that matches the category
        var numMethods = patchTypes.SelectMany(t =>
                t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Count(m => m.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0);

        if (Settings.DisabledPatchCategories.Contains(category))
        {
            Log.Message($"Patch category \"{category}\" disabled in mod settings. Skipping.");
            _skippedPatches += numMethods;
            return;
        }

        try
        {
            Log.Trace($"Patching category \"{category}\" ({numMethods} methods)...");
            Harmony.PatchCategory(category);
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Error patching category {category}");
            _failedPatches += numMethods;
            return;
        }

        _allEnabledSuccessfulPatches.Add(category);
        _loadedPatches += numMethods;
    }

    internal static void UnpatchCategory(string category)
    {
        if (_allEnabledSuccessfulPatches.Contains(category) == false)
            return;
        var patchTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttributes(typeof(HarmonyPatchCategory), true)
                .Cast<HarmonyPatchCategory>()
                .Any(attr => attr.info?.category == category))
            .ToList();

        // Find any classes in the assembly with a [HarmonyPatchCategory] attribute that matches the category
        var numMethods = patchTypes.SelectMany(t =>
                t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Count(m => m.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0);

        Log.Message($"Unpatching category {category} ({numMethods} methods)");
        Harmony.UnpatchCategory(category);

        _allEnabledSuccessfulPatches.Remove(category);
        _skippedPatches += numMethods;
        _loadedPatches -= numMethods;
    }

    internal static void SetPatched(string category, bool patch)
    {
        var alreadyPatched = _allEnabledSuccessfulPatches.Contains(category);
        if (patch && !alreadyPatched)
        {
            Settings.DisabledPatchCategories.Remove(category);
            PatchCategory(category);
        }
        else if (!patch && alreadyPatched)
        {
            Settings.DisabledPatchCategories.Add(category);
            UnpatchCategory(category);
        }
        else
            Log.Trace($"Patch {category} already set to {patch} state.");
    }

    /// <summary>
    /// Called by dynamic patches to notify every time they are run and track them.
    /// </summary>
    /// <param name="original">The original unpatched MethodBase</param>
    /// <param name="numPatches">Total number of patches, including failed/skipped</param>
    /// <param name="numSkipped">Skipped patches due to settings or incompatibilities or whatever</param>
    /// <param name="numFailed">Patches that completely failed due to an exception or something</param>
    internal static void Notify_Patched(MethodBase? original, int numPatches, int numSkipped = 0, int numFailed = 0)
    {
        _loadedPatches += numPatches - numSkipped - numFailed;
        _skippedPatches += numSkipped;
        _failedPatches += numFailed;
        if (original == null)
            Log.Warning($"{numPatches} patches failed, couldn't find target method.");
        else
            Log.Trace($"Patched {original.Name} with {numPatches} patches, {numSkipped} skipped, {numFailed} failed.");
    }

    /// <summary>
    /// Helper method to get the method info of an interface property for patching
    /// </summary>
    public static MethodBase? GetInterfaceProperty(this Type type, Type interfaceType, string propName)
    {
        var interfaceProp = interfaceType.GetProperty(propName, AccessTools.all);
        var map = type.GetInterfaceMap(interfaceType);
        var interfaceMethod = interfaceProp?.GetGetMethod(true);

        var index = Array.IndexOf(map.InterfaceMethods, interfaceMethod);

        return index != -1 ? map.TargetMethods[index] : null;
    }
}
