using RimWorld;

namespace BetterLetters;

/// <summary>
/// Custom definitions of vanilla defs that don't have a DefOf anywhere in the vanilla code for some reason
/// </summary>
[StaticConstructorOnStartup]
internal static class MissingVanillaDefOf
{
    internal static readonly IncidentDef Flashstorm = DefDatabase<IncidentDef>.GetNamed("Flashstorm")!;
}
