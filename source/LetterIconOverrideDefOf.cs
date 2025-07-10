using RimWorld;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace BetterLetters;

[DefOf]
internal static class LetterIconOverrideDefOf
{
    static LetterIconOverrideDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(LetterIconOverrideDefOf));
    }

    public static LetterIconOverrideDef MentalBreak;


    public static LetterIconOverrideDef SolarFlare;

    public static LetterIconOverrideDef Flashstorm;

    public static LetterIconOverrideDef VolcanicWinter;

    public static LetterIconOverrideDef Eclipse;

    public static LetterIconOverrideDef ToxicFallout;

    public static LetterIconOverrideDef ColdSnap;

    public static LetterIconOverrideDef HeatWave;

    public static LetterIconOverrideDef Aurora;

    public static LetterIconOverrideDef PsychicDrone;

    public static LetterIconOverrideDef PsychicSoothe;


    public static LetterIconOverrideDef Raid;


    public static LetterIconOverrideDef TraderCaravan;

    public static LetterIconOverrideDef TraderOrbital;


    public static LetterIconOverrideDef Ideology;


    public static LetterIconOverrideDef Anomaly;
}
