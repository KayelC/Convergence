namespace JRPGPrototype.Data.Definitions;

public abstract record ConditionDefinition;

public sealed record AllConditionDefinition : ConditionDefinition
{
    public AllConditionDefinition(IEnumerable<ConditionDefinition> conditions)
    {
        Conditions = DefinitionCollections.Snapshot(conditions);
    }

    public IReadOnlyList<ConditionDefinition> Conditions { get; }
}

public sealed record AnyConditionDefinition : ConditionDefinition
{
    public AnyConditionDefinition(IEnumerable<ConditionDefinition> conditions)
    {
        Conditions = DefinitionCollections.Snapshot(conditions);
    }

    public IReadOnlyList<ConditionDefinition> Conditions { get; }
}

public sealed record NotConditionDefinition(ConditionDefinition Condition)
    : ConditionDefinition;

public sealed record ResourcePercentageConditionDefinition(
    ConditionSubject Subject,
    ContentId ResourceId,
    NumericComparison Comparison,
    decimal Value)
    : ConditionDefinition;

public sealed record HasAilmentConditionDefinition : ConditionDefinition
{
    public HasAilmentConditionDefinition(
        ConditionSubject subject,
        IEnumerable<ContentId> ailmentIds)
    {
        Subject = subject;
        AilmentIds = DefinitionCollections.Snapshot(ailmentIds);
    }

    public ConditionSubject Subject { get; }
    public IReadOnlyList<ContentId> AilmentIds { get; }
}

public sealed record HasSkillConditionDefinition(
    ConditionSubject Subject,
    ContentId SkillId)
    : ConditionDefinition;

public sealed record HasBuffConditionDefinition(
    ConditionSubject Subject,
    ContentId ModifierTrackId)
    : ConditionDefinition;

public sealed record HasAffinityConditionDefinition(
    ConditionSubject Subject,
    DamageElement Element,
    ElementalAffinity Affinity)
    : ConditionDefinition;

public sealed record HasCapabilityConditionDefinition(
    ConditionSubject Subject,
    ContentId CapabilityId)
    : ConditionDefinition;

public sealed record LifeStateConditionDefinition(
    ConditionSubject Subject,
    TargetLifeState LifeState)
    : ConditionDefinition;

public sealed record BattleKindConditionDefinition : ConditionDefinition
{
    public BattleKindConditionDefinition(IEnumerable<ContentId> allowedBattleKindIds)
    {
        AllowedBattleKindIds = DefinitionCollections.Snapshot(allowedBattleKindIds);
    }

    public IReadOnlyList<ContentId> AllowedBattleKindIds { get; }
}

public sealed record MoonPhaseConditionDefinition : ConditionDefinition
{
    public MoonPhaseConditionDefinition(IEnumerable<ContentId> allowedMoonPhaseIds)
    {
        AllowedMoonPhaseIds = DefinitionCollections.Snapshot(allowedMoonPhaseIds);
    }

    public IReadOnlyList<ContentId> AllowedMoonPhaseIds { get; }
}

public sealed record PartySizeConditionDefinition(
    NumericComparison Comparison,
    int Value)
    : ConditionDefinition;

public sealed record ChanceConditionDefinition(int Chance)
    : ConditionDefinition;

public sealed record EffectElementConditionDefinition(DamageElement Element)
    : ConditionDefinition;

public sealed record CustomConditionDefinition : ConditionDefinition
{
    public CustomConditionDefinition(
        ContentId handlerId,
        IEnumerable<KeyValuePair<string, object?>>? parameters = null)
    {
        HandlerId = handlerId;
        Parameters = DefinitionCollections.SnapshotParameters(parameters);
    }

    public ContentId HandlerId { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}
