using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse.AI;

namespace BetterLetters;

/// <summary>
/// Class used to represent a target method to be patched through XML.
/// </summary>
[UsedImplicitly]
public class PatchTarget
{
    internal string TypeColonName => $"{typeName ?? "null"}:{methodName ?? "null"}";

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

            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                Log.Error($"No type named {typeName} found in any loaded assemblies");
                return null;
            }

            var method = AccessTools.Method(type, methodName, Parameters!);

            _targetMethodInt = method;
            return method;
        }
    }

    internal string LetterSendingMethod =>
        letterSendingMethodName.NullOrEmpty() ? "ReceiveLetter" : letterSendingMethodName;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public PatchTarget()
    {
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public PatchTarget(MethodInfo method, string? letterSendingMethodName = null)
    {
        methodName = method.Name;
        typeName = method.DeclaringType!.Name;
        this.letterSendingMethodName = letterSendingMethodName ?? "ReceiveLetter";

        _targetMethodInt = method;
    }

    public PatchTarget(Type type, string methodName, string? letterSendingMethodName = null)
    {
        this.methodName = methodName;
        typeName = type.Name;
        this.letterSendingMethodName = letterSendingMethodName ?? "ReceiveLetter";

        _targetMethodInt = AccessTools.Method(type, methodName);
    }

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

[UsedImplicitly]
public abstract class PatchWorker
{
    public abstract List<PatchTarget>? GetPatchTargetsForDef(LetterIconOverrideDef def);

    public virtual IEnumerable<string> ConfigErrors()
    {
        yield break;
    }
}

[UsedImplicitly]
public class GenericTranspilerPatchWorker : PatchWorker
{
    public override List<PatchTarget>? GetPatchTargetsForDef(LetterIconOverrideDef def)
    {
        return def.defName switch
        {
            "Faction" => new List<PatchTarget>
            {
                new(typeof(Faction), nameof(Faction.Notify_LeaderLost)),
                new(typeof(Faction), nameof(Faction.Notify_LeaderDied)),
                // This one is disabled because the actual call to ReceiveLetter occurs inside of an anonymous delegate
                // new(typeof(PeaceTalks), "Outcome_Disaster"),
                new(typeof(PeaceTalks), "Outcome_Backfire"),
                new(typeof(PeaceTalks), "Outcome_TalksFlounder"),
                new(typeof(PeaceTalks), "Outcome_Success"),
                new(typeof(PeaceTalks), "Outcome_Triumph"),
                new(typeof(SettlementProximityGoodwillUtility),
                    nameof(SettlementProximityGoodwillUtility.CheckSettlementProximityGoodwillChange)),
            },
            "BaseDestroyed" => new List<PatchTarget>
            {
                new(typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.CheckDefeated)),
            },
            "Caravan" => new List<PatchTarget>
            {
                new(typeof(Caravan), nameof(Caravan.Notify_MemberDied)),
                new(typeof(CaravanArrivalAction_VisitSettlement), nameof(CaravanArrivalAction_VisitSettlement.Arrived)),
                new(typeof(CaravanArrivalAction_Enter), nameof(CaravanArrivalAction_Enter.Arrived)),
                new(typeof(TransportersArrivalAction_AttackSettlement),
                    nameof(TransportersArrivalAction_AttackSettlement.Arrived)),
                new(typeof(TransportersArrivalAction_TransportShip),
                    nameof(TransportersArrivalAction_TransportShip.Arrived)),
                new(typeof(CaravanArrivalAction_VisitEscapeShip), "DoArrivalAction"),
                new(typeof(CaravanArrivalAction_VisitSite), "DoEnter"),
                new(typeof(SettlementUtility), "AttackNow"),
                new(typeof(SignalAction_Ambush), "DoAction"),
                new(typeof(CaravansBattlefield), "CheckWonBattle"),
            },
            "MainQuest" => new List<PatchTarget>
            {
                new(typeof(CompHibernatable), nameof(CompHibernatable.CompTick)),
                new(typeof(GameComponent_OnetimeNotification),
                    nameof(GameComponent_OnetimeNotification.GameComponentTick)),
            },
            "MapClosed" => new List<PatchTarget>
            {
                new(typeof(MapDeiniter), "PassPawnsToWorld"),
            },
            "Gift" => new List<PatchTarget>
            {
                new(typeof(VisitorGiftForPlayerUtility), nameof(VisitorGiftForPlayerUtility.GiveGift)),
            },
            "DeepScanner" => new List<PatchTarget>
            {
                new(typeof(CompDeepScanner), "DoFind"),
            },
            "PrisonBreak" => new List<PatchTarget>
            {
                new(AccessTools.Method(typeof(PrisonBreakUtility), nameof(PrisonBreakUtility.StartPrisonBreak),
                    new[] { typeof(Pawn) })!)
            },
            "MentalBreak" => new List<PatchTarget>
            {
                new(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState)),
                new(typeof(MentalBreakWorker_RunWild), nameof(MentalBreakWorker_RunWild.TryStart), "TrySendLetter"),
                new(typeof(MentalBreakWorker_Catatonic), nameof(MentalBreakWorker_Catatonic.TryStart), "TrySendLetter"),
            },
            "Inspiration" => new List<PatchTarget>
            {
                new(typeof(Inspiration), "SendBeginLetter")
            },
            "CraftedQuality" => new List<PatchTarget>
            {
                new(typeof(QualityUtility), nameof(QualityUtility.SendCraftNotification))
            },
            "SolarFlare" => new List<PatchTarget>
            {
                new(typeof(ShortCircuitUtility), nameof(ShortCircuitUtility.DoShortCircuit)),
                new(typeof(ShortCircuitUtility), nameof(ShortCircuitUtility.TryShortCircuitInRain))
            },
            "Raid" => new List<PatchTarget>
            {
                new(typeof(IncidentWorker_Raid), "TryExecuteWorker", "SendStandardLetter")
            },
            "TraderCaravan" => new List<PatchTarget>
            {
                new(typeof(IncidentWorker_TraderCaravanArrival), "SendLetter", "SendStandardLetter")
            },
            "TraderOrbital" => new List<PatchTarget>
            {
                new(typeof(IncidentWorker_OrbitalTraderArrival), "TryExecuteWorker", "SendStandardLetter")
            },
            "PsychicDrone" => new List<PatchTarget>
            {
                new(typeof(IncidentWorker_PsychicDrone), "DoConditionAndLetter", "SendStandardLetter")
            },
            "PsychicSoothe" => new List<PatchTarget>
            {
                new(typeof(IncidentWorker_PsychicSoothe), "DoConditionAndLetter", "SendStandardLetter")
            },
            "DiseaseDiscoverable" => new List<PatchTarget>
            {
                new(typeof(HediffComp_Discoverable), "CheckDiscovered")
            },
            "DiseaseOrganic" => new List<PatchTarget>
            {
                new(typeof(HediffComp_GiveHediffLungRot), nameof(HediffComp_GiveHediffLungRot.CompPostTickInterval)),
            },
            "Addiction" => new List<PatchTarget>
            {
                new(typeof(CompDrug), nameof(CompDrug.PrePostIngested)),
                new(typeof(MentalState_BingingDrug), nameof(MentalState_BingingDrug.PostStart))
            },
            "AreaRevealed" => new List<PatchTarget>
            {
                new(typeof(FogGrid), "NotifyAreaRevealed")
            },
            "Birthday" => new List<PatchTarget>
            {
                new(typeof(Pawn_AgeTracker), "BirthdayBiological")
            },
            "Kidnapped" => new List<PatchTarget>
            {
                new(typeof(KidnappedPawnsTracker), nameof(KidnappedPawnsTracker.Kidnap))
            },
            "Joiner" => new List<PatchTarget>
            {
                new(typeof(Pawn_MindState), nameof(Pawn_MindState.JoinColonyBecauseRescuedBy)),
                new(typeof(QuestPart_PawnsArrive), nameof(QuestPart_PawnsArrive.Notify_QuestSignalReceived)),
                new(typeof(QuestPart_GiveNearPawn), nameof(QuestPart_GiveNearPawn.Notify_QuestSignalReceived)),
                new(typeof(Pawn_GuestTracker), "Notify_PawnUndowned"),
            },
            "ResourcePodCrash" => new List<PatchTarget>
            {
                new(typeof(QuestPart_DropPods), nameof(QuestPart_DropPods.Notify_QuestSignalReceived)),
            },
            "Manhunter" => new List<PatchTarget>
            {
                new(typeof(Pawn_MindState), "StartManhunterBecauseOfPawnAction"),
                new(typeof(JobDriver_PredatorHunt), "CheckWarnPlayerInterval"),
            },
            "FriendlyTrapSprung" => new List<PatchTarget>
            {
                new(typeof(Building_Trap), "CheckSpring")
            },
            "HealthComplications" => new List<PatchTarget>
            {
                new(typeof(HediffGiver), "SendLetter"),
            },
            "SignalAction" => new List<PatchTarget>
            {
                new(typeof(SignalAction_Letter), "DoAction")
            },
            "Royalty" => new List<PatchTarget>
            {
                new(typeof(Pawn_RoyaltyTracker), "OnPreTitleChanged"),
                new(typeof(RitualOutcomeEffectWorker_Bestowing), "Apply"),
                new(typeof(CompBladelinkWeapon), nameof(CompBladelinkWeapon.CodeFor)),
                new(typeof(ResearchManager), nameof(ResearchManager.ApplyTechprint)),
                new(typeof(RitualOutcomeEffectWorker_Bestowing), nameof(RitualOutcomeEffectWorker_Bestowing.Apply)),
            },
            "Ideology" => new List<PatchTarget>
            {
                new(typeof(Precept_RoleSingle), "Assign"),
                new(typeof(RitualOutcomeEffectWorker_RoleChange), "Apply"),
            },
            "Anomaly" => new List<PatchTarget>
            {
                new(typeof(CompDreadmeld), "Notify_Killed"),
                new(typeof(CompHoldingPlatformTarget), "Escape"),
                new(typeof(Building_VoidMonolith), "Tick"),
                new(typeof(GameComponent_Anomaly), "GameComponentTick"),
                new(typeof(CompStudyUnlocks), "RegisterStudyLevel"),
            },
            _ => null
        };
    }
}
