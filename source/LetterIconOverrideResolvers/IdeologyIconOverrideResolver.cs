using System.Linq;
using JetBrains.Annotations;
using RimWorld;

namespace BetterLetters.LetterIconOverrideResolvers;

[UsedImplicitly]
internal class IdeologyIconOverrideResolver : LetterIconOverrideResolver
{
    protected override void ResolvePath(out string? iconPath, params object[] context)
    {
        var pawn = context.OfType<Pawn>().FirstOrFallback();
        var ritual = context.OfType<LordJob_Ritual>().FirstOrFallback();

        PreceptDef? role = null;
        if (ritual is not null)
            role = ritual.assignments?.RoleChangeSelection?.def;
        else if (pawn is not null)
            role = pawn.Ideo?.GetRole(pawn)?.def;

        //TODO: Add more ideology sub-icons

        iconPath = role?.defName switch
        {
            "IdeoRole_Leader" => def!.iconPath + "_" + "Leader",
            "IdeoRole_Moralist" => def!.iconPath + "_" + "Moralist",
            "IdeoRole_ShootingSpecialist" => def!.iconPath + "_" + "ShootingSpecialist",
            "IdeoRole_MeleeSpecialist" => def!.iconPath + "_" + "MeleeSpecialist",
            "IdeoRole_ResearchSpecialist" => def!.iconPath + "_" + "ResearchSpecialist",
            "IdeoRole_PlantSpecialist" => def!.iconPath + "_" + "PlantSpecialist",
            "IdeoRole_ProductionSpecialist" => def!.iconPath + "_" + "ProductionSpecialist",
            "IdeoRole_MiningSpecialist" => def!.iconPath + "_" + "MiningSpecialist",
            "IdeoRole_AnimalsSpecialist" => def!.iconPath + "_" + "AnimalsSpecialist",
            "IdeoRole_MedicalSpecialist" => def!.iconPath + "_" + "MedicalSpecialist",
            _ => null
        };
    }
}
