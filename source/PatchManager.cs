using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace BetterLetters;

/// <summary>
/// Class used to represent a target method to be patched through XML.
/// </summary>
[UsedImplicitly]
public class PatchTarget
{
    internal string TypeColonName => $"{typeName}:{methodName}";

    internal Type[]? Parameters
    {
        get
        {
            if (argumentTypes == null || argumentTypes.Count == 0)
                return null;

            var argTypes = new Type[argumentTypes.Count];
            for (var i = 0; i < argumentTypes.Count; i++)
            {
                var type = Type.GetType(argumentTypes[i]!);

                argTypes[i] = type ?? throw new InvalidOperationException(
                    $"{argumentTypes[i]} is not a valid type in any loaded assemblies.");
            }

            return argTypes;
        }
    }

    private MethodInfo? _targetMethodInt;

    internal MethodInfo? TargetMethod
    {
        get
        {
            if (_targetMethodInt != null) return _targetMethodInt;

            var method = AccessTools.Method(
                typeColonName: TypeColonName,
                parameters: Parameters!
            );

            _targetMethodInt = method;
            return method;
        }
    }

    internal string LetterSendingMethod =>
        letterSendingMethodName.NullOrEmpty() ? "ReceiveLetter" : letterSendingMethodName;

    internal IEnumerable<string> ConfigErrors()
    {
        if (TargetMethod == null)
            yield return $"Target method {TypeColonName} not found in any loaded assemblies";

        foreach (var arg in argumentTypes ?? new List<string>())
        {
            if (arg == null)
            {
                yield return "Argument type is null";
                continue;
            }

            if (arg.Trim() == "")
            {
                yield return "Argument type is empty";
                continue;
            }

            var type = Type.GetType(arg);
            if (type == null)
            {
                yield return $"Argument type {arg} not found in any loaded assemblies";
                continue;
            }
        }
    }


    // ReSharper disable InconsistentNaming
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

    // XML-defined fields
    private string typeName;

    private string methodName;

    private string letterSendingMethodName;

    private List<string>? argumentTypes;


#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    // ReSharper restore InconsistentNaming
}

/// <summary>
///     Helper class for all Harmony patching functionality.
/// </summary>
[StaticConstructorOnStartup]
[UsedImplicitly]
internal static class PatchManager
{
    internal static readonly Harmony Harmony;

    private static int _loadedPatches;
    private static int _failedPatches;
    private static int _skippedPatches;
    private static List<string> _allEnabledSuccessfulPatches = new();

    static PatchManager()
    {
        // Harmony.DEBUG = true;
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
        //MAYBE: Harvest all existent patch categories and iterate over them here instead
        foreach (var patchCategory in Settings.EnabledPatchCategories)
            PatchCategory(patchCategory);
    }

    internal static void RepatchAll()
    {
        Log.Warning("Attempting to unpatch and re-patch selected Harmony patches...");
        foreach (var patch in Settings.DisabledPatchCategories)
        {
            UnpatchCategory(patch);
        }

        foreach (var patch in Settings.EnabledPatchCategories)
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

        if (Settings.EnabledPatchCategories.Contains(category) == false)
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
            Settings.EnabledPatchCategories.Add(category);
            Settings.DisabledPatchCategories.Remove(category);
            PatchCategory(category);
        }
        else if (!patch && alreadyPatched)
        {
            Settings.DisabledPatchCategories.Add(category);
            Settings.EnabledPatchCategories.Remove(category);
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
