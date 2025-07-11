using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace BetterLetters;

/// <summary>
/// Class for sending warnings and changing settings if certain mods are detected.
/// </summary>
[StaticConstructorOnStartup]
internal static class ModCompatibility
{
    private readonly struct ModCompatibilityBehavior
    {
        private readonly string _modPackageId;
        private readonly string _warningMessage;
        private readonly bool _warn;
        private readonly Action<ModMetaData>? _compatAction;

        /// <summary>
        /// Register a given mod's PackageId as potentially having compatibility issues.
        /// </summary>
        /// <param name="modPackageId">The exact, case-insensitive package id of the other mod.</param>
        /// <param name="warningMessage">If the other mod is detected, include this string in the message logged to the player.</param>
        /// <param name="warn">If true, the message will be sent as a warning. Meant for incompatibilities that do not have patches and require user action.</param>
        /// <param name="compatAction">An optional callback to perform if the other mod is detected, will be supplied with the mod's <see cref="ModMetaData"/></param>
        // ReSharper disable once UnusedMember.Local
        internal ModCompatibilityBehavior(string modPackageId, string warningMessage, bool warn = false,
            Action<ModMetaData>? compatAction = null)
        {
            _modPackageId = modPackageId;
            _warningMessage = warningMessage;
            _warn = warn;
            _compatAction = compatAction;
        }

        /// <summary>
        /// Check if the mod in question is active and print a warning, then invoke the action if any.
        /// </summary>
        /// <returns>true if the mod is active.</returns>
        internal bool Check()
        {
            if (!CheckForMod(_modPackageId, out var mod)) return false;

            Log.CompatibilityWarning($"{mod?.Name} [{_modPackageId}] detected.\n{_warningMessage}", _warn);
            if (mod is not null)
                _compatAction?.Invoke(mod);
            return true;
        }
    }

    /// <summary>
    /// List of mods that are known to have compatibility issues with this mod.<para/>
    /// Some may have compatibility patches, either external to or part of this mod. Others may be hard incompatible.
    /// </summary>
    // ReSharper disable once CollectionNeverUpdated.Local
    private static readonly List<ModCompatibilityBehavior> ModCompatibilityBehaviors = new()
    // ReSharper disable once RedundantEmptyObjectOrCollectionInitializer
    {
    };

    static ModCompatibility()
    {
        foreach (var behavior in ModCompatibilityBehaviors)
            behavior.Check();
    }

    internal static bool CheckForMod(string modIdentifier, out ModMetaData? otherMod)
    {
        otherMod = ModLister.GetActiveModWithIdentifier(modIdentifier);
        return otherMod != null;
    }

    internal static bool TryGetModAssembly(string packageId, out List<Assembly>? assemblies)
    {
        var mod = LoadedModManager.RunningModsListForReading?.FirstOrDefault(m =>
            m.PackageId!.ToLower() == packageId.ToLower());
        assemblies = mod?.assemblies?.loadedAssemblies;
        return assemblies != null;
    }

    internal static bool HasExistingPatches(this MethodInfo method, bool warn = true)
    {
        var patches = Harmony.GetPatchInfo(method);
        if (patches is null) return false;

        var hasPatches = patches.Prefixes?.Count > 0 || patches.Postfixes?.Count > 0 || patches.Transpilers?.Count > 0;

        if (warn && hasPatches)
        {
            Log.Warning(
                $"Found existing Harmony patches for {method.DeclaringType?.FullName}.{method.Name}. If you encounter compatibility issues, please report it on the Workshop page or GitHub issues.\nYou can safely ignore this warning if nothing seems broken.");
            foreach (var patch in patches.Prefixes!.Union(patches.Postfixes!).Union(patches.Transpilers!))
                Log.Message($"Patch: {patch.PatchMethod!.Module.Assembly.FullName}::{patch.PatchMethod.Name}");
        }

        return hasPatches;
    }
}
