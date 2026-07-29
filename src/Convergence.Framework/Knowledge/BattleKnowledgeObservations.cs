using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Knowledge;

/// <summary>Identifies the defense domain established by one execution observation.</summary>
public enum BattleKnowledgeObservationKind
{
    ElementalAffinity,
    AilmentResistance,
    InstantDeathResistance
}

/// <summary>Identifies what the action actually established about the target.</summary>
public enum BattleKnowledgeObservationOutcome
{
    Contacted,
    Missed,
    Applied,
    Immune,
    Blocked,
    Failed,
    Defeated
}

/// <summary>Identifies temporary battle state that participated in defense resolution.</summary>
[Flags]
public enum BattleDefenseInfluence
{
    None = 0,
    Guard = 1 << 0,
    Shield = 1 << 1,
    AffinityBreak = 1 << 2,
    AffinityOverride = 1 << 3,
    PassiveModifier = 1 << 4
}

/// <summary>
/// Carries validated, serializer-neutral evidence about one defense resolution.
/// It records facts; later knowledge policies decide which facts enter encounter or persistent knowledge.
/// </summary>
public sealed class BattleKnowledgeObservation
{
    private const BattleDefenseInfluence AllInfluences =
        BattleDefenseInfluence.Guard |
        BattleDefenseInfluence.Shield |
        BattleDefenseInfluence.AffinityBreak |
        BattleDefenseInfluence.AffinityOverride |
        BattleDefenseInfluence.PassiveModifier;

    private BattleKnowledgeObservation(
        BattleKnowledgeObservationKind kind,
        BattleKnowledgeObservationOutcome outcome,
        ContentId sourceActionId,
        RuntimeInstanceId actorId,
        RuntimeInstanceId targetId,
        ContentId targetEntityId,
        int effectIndex,
        BattleDefenseInfluence temporaryInfluences,
        DamageElement? element = null,
        ContentId? ailmentId = null,
        InstantDeathChannel? instantDeathChannel = null,
        ElementalAffinity? authoredAffinity = null,
        ElementalAffinity? effectiveAffinity = null,
        ResistanceLevel? authoredResistance = null,
        ResistanceLevel? effectiveResistance = null,
        bool resistanceBypassed = false,
        bool resistanceBlockConfirmed = false)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Observation kind must be defined.");
        }
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Observation outcome must be defined.");
        }
        if (!sourceActionId.IsValid)
        {
            throw new ArgumentException("Source action ID must be valid.", nameof(sourceActionId));
        }
        if (!actorId.IsValid)
        {
            throw new ArgumentException("Actor runtime ID must be valid.", nameof(actorId));
        }
        if (!targetId.IsValid)
        {
            throw new ArgumentException("Target runtime ID must be valid.", nameof(targetId));
        }
        if (!targetEntityId.IsValid)
        {
            throw new ArgumentException("Target entity ID must be valid.", nameof(targetEntityId));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(effectIndex);
        if ((temporaryInfluences & ~AllInfluences) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temporaryInfluences),
                temporaryInfluences,
                "Temporary defense influences must contain only defined flags.");
        }

        Kind = kind;
        Outcome = outcome;
        SourceActionId = sourceActionId;
        ActorId = actorId;
        TargetId = targetId;
        TargetEntityId = targetEntityId;
        EffectIndex = effectIndex;
        TemporaryInfluences = temporaryInfluences;
        Element = element;
        AilmentId = ailmentId;
        InstantDeathChannel = instantDeathChannel;
        AuthoredAffinity = authoredAffinity;
        EffectiveAffinity = effectiveAffinity;
        AuthoredResistance = authoredResistance;
        EffectiveResistance = effectiveResistance;
        ResistanceBypassed = resistanceBypassed;
        ResistanceBlockConfirmed = resistanceBlockConfirmed;
    }

    public BattleKnowledgeObservationKind Kind { get; }
    public BattleKnowledgeObservationOutcome Outcome { get; }
    public ContentId SourceActionId { get; }
    public RuntimeInstanceId ActorId { get; }
    public RuntimeInstanceId TargetId { get; }
    public ContentId TargetEntityId { get; }
    public int EffectIndex { get; }
    public BattleDefenseInfluence TemporaryInfluences { get; }
    public DamageElement? Element { get; }
    public ContentId? AilmentId { get; }
    public InstantDeathChannel? InstantDeathChannel { get; }
    public ElementalAffinity? AuthoredAffinity { get; }
    public ElementalAffinity? EffectiveAffinity { get; }
    public ResistanceLevel? AuthoredResistance { get; }
    public ResistanceLevel? EffectiveResistance { get; }
    public bool ResistanceBypassed { get; }
    public bool ResistanceBlockConfirmed { get; }
    public bool HasTemporaryInfluence => TemporaryInfluences != BattleDefenseInfluence.None;

    /// <summary>Creates evidence for an attempted typed damage effect.</summary>
    public static BattleKnowledgeObservation Elemental(
        ContentId sourceActionId,
        RuntimeInstanceId actorId,
        RuntimeInstanceId targetId,
        ContentId targetEntityId,
        int effectIndex,
        DamageElement element,
        bool contacted,
        ElementalAffinity authoredAffinity,
        ElementalAffinity effectiveAffinity,
        BattleDefenseInfluence temporaryInfluences = BattleDefenseInfluence.None)
    {
        RequireDefined(element, nameof(element));
        RequireDefined(authoredAffinity, nameof(authoredAffinity));
        RequireDefined(effectiveAffinity, nameof(effectiveAffinity));
        return new BattleKnowledgeObservation(
            BattleKnowledgeObservationKind.ElementalAffinity,
            contacted ? BattleKnowledgeObservationOutcome.Contacted : BattleKnowledgeObservationOutcome.Missed,
            sourceActionId,
            actorId,
            targetId,
            targetEntityId,
            effectIndex,
            temporaryInfluences,
            element: element,
            authoredAffinity: authoredAffinity,
            effectiveAffinity: effectiveAffinity);
    }

    /// <summary>Creates evidence for an attempted typed ailment effect.</summary>
    public static BattleKnowledgeObservation Ailment(
        ContentId sourceActionId,
        RuntimeInstanceId actorId,
        RuntimeInstanceId targetId,
        ContentId targetEntityId,
        int effectIndex,
        ContentId ailmentId,
        BattleAilmentApplicationStatus applicationStatus,
        ResistanceLevel authoredResistance,
        ResistanceLevel? effectiveResistance,
        BattleDefenseInfluence temporaryInfluences = BattleDefenseInfluence.None)
    {
        if (!ailmentId.IsValid)
        {
            throw new ArgumentException("Ailment ID must be valid.", nameof(ailmentId));
        }
        RequireDefined(applicationStatus, nameof(applicationStatus));
        RequireDefined(authoredResistance, nameof(authoredResistance));
        if (effectiveResistance is ResistanceLevel resolved)
        {
            RequireDefined(resolved, nameof(effectiveResistance));
        }

        BattleKnowledgeObservationOutcome outcome = applicationStatus switch
        {
            BattleAilmentApplicationStatus.Applied => BattleKnowledgeObservationOutcome.Applied,
            BattleAilmentApplicationStatus.Immune => BattleKnowledgeObservationOutcome.Immune,
            BattleAilmentApplicationStatus.Missed => BattleKnowledgeObservationOutcome.Missed,
            BattleAilmentApplicationStatus.GuardBlocked => BattleKnowledgeObservationOutcome.Blocked,
            _ => BattleKnowledgeObservationOutcome.Failed
        };
        return new BattleKnowledgeObservation(
            BattleKnowledgeObservationKind.AilmentResistance,
            outcome,
            sourceActionId,
            actorId,
            targetId,
            targetEntityId,
            effectIndex,
            temporaryInfluences,
            ailmentId: ailmentId,
            authoredResistance: authoredResistance,
            effectiveResistance: effectiveResistance);
    }

    /// <summary>Creates evidence for an attempted typed instant-defeat effect.</summary>
    public static BattleKnowledgeObservation InstantDeath(
        ContentId sourceActionId,
        RuntimeInstanceId actorId,
        RuntimeInstanceId targetId,
        ContentId targetEntityId,
        int effectIndex,
        InstantDeathChannel? channel,
        bool resistanceBypassed,
        bool defeated,
        ResistanceLevel? authoredResistance,
        ResistanceLevel? effectiveResistance,
        bool resistanceBlockConfirmed = false,
        BattleDefenseInfluence temporaryInfluences = BattleDefenseInfluence.None)
    {
        if (channel is InstantDeathChannel instantDeathChannel)
        {
            RequireDefined(instantDeathChannel, nameof(channel));
        }
        if (authoredResistance is ResistanceLevel authored)
        {
            RequireDefined(authored, nameof(authoredResistance));
        }
        if (effectiveResistance is ResistanceLevel effective)
        {
            RequireDefined(effective, nameof(effectiveResistance));
        }
        bool hasNoResistanceTuple =
            channel is null && authoredResistance is null && effectiveResistance is null;
        bool hasCompleteResistanceTuple =
            channel is not null && authoredResistance is not null && effectiveResistance is not null;
        if ((resistanceBypassed && !hasNoResistanceTuple) ||
            (!resistanceBypassed && !hasCompleteResistanceTuple))
        {
            throw new ArgumentException(
                "A bypassed resistance check must omit its channel and resistances, while a checked resistance must provide all three values.",
                nameof(resistanceBypassed));
        }
        if (resistanceBlockConfirmed &&
            (resistanceBypassed || defeated || effectiveResistance != ResistanceLevel.Immune))
        {
            throw new ArgumentException(
                "A confirmed resistance block requires a checked immune resistance and a failed attempt.",
                nameof(resistanceBlockConfirmed));
        }

        BattleKnowledgeObservationOutcome outcome = defeated
            ? BattleKnowledgeObservationOutcome.Defeated
            : resistanceBlockConfirmed
                ? BattleKnowledgeObservationOutcome.Immune
                : BattleKnowledgeObservationOutcome.Failed;
        return new BattleKnowledgeObservation(
            BattleKnowledgeObservationKind.InstantDeathResistance,
            outcome,
            sourceActionId,
            actorId,
            targetId,
            targetEntityId,
            effectIndex,
            temporaryInfluences,
            instantDeathChannel: channel,
            authoredResistance: authoredResistance,
            effectiveResistance: effectiveResistance,
            resistanceBypassed: resistanceBypassed,
            resistanceBlockConfirmed: resistanceBlockConfirmed);
    }

    private static void RequireDefined<T>(T value, string parameterName) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Observation values must be defined.");
        }
    }
}
