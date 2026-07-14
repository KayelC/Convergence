namespace Convergence.Content;

public sealed record TargetCountDefinition(int Minimum, int Maximum);

public sealed record TargetingDefinition(
    TargetRelation Relation,
    TargetSelection Selection,
    TargetLifeState LifeState,
    bool AllowSelf,
    TargetCountDefinition? Count = null);

public abstract record AmountDefinition(AmountKind Kind);

public sealed record FlatAmountDefinition(decimal Value)
    : AmountDefinition(AmountKind.Flat);

public sealed record PercentMaximumAmountDefinition(decimal Value)
    : AmountDefinition(AmountKind.PercentMaximum);

public sealed record PercentCurrentAmountDefinition(decimal Value)
    : AmountDefinition(AmountKind.PercentCurrent);

public sealed record FullAmountDefinition()
    : AmountDefinition(AmountKind.Full);

public sealed record PowerAmountDefinition(int Power)
    : AmountDefinition(AmountKind.Power);

public sealed record FormulaAmountDefinition : AmountDefinition
{
    public FormulaAmountDefinition(
        ContentId formulaId,
        IEnumerable<KeyValuePair<string, object?>>? parameters = null)
        : base(AmountKind.Formula)
    {
        FormulaId = formulaId;
        Parameters = DefinitionCollections.SnapshotParameters(parameters);
    }

    public ContentId FormulaId { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}

public abstract record DurationDefinition(DurationKind Kind);

public sealed record InstantDurationDefinition()
    : DurationDefinition(DurationKind.Instant);

public sealed record TurnDurationDefinition(
    int Value,
    ContentId TickEventId,
    bool SuspendWhileReserve)
    : DurationDefinition(DurationKind.Turns);

public sealed record PhaseDurationDefinition(ContentId PhaseId)
    : DurationDefinition(DurationKind.Phase);

public sealed record BattleDurationDefinition()
    : DurationDefinition(DurationKind.Battle);

public sealed record PermanentDurationDefinition()
    : DurationDefinition(DurationKind.Permanent);

public abstract record CriticalDefinition(CriticalMode Mode);

public sealed record NeverCriticalDefinition()
    : CriticalDefinition(CriticalMode.Never);

public sealed record ChanceCriticalDefinition(int Chance)
    : CriticalDefinition(CriticalMode.Chance);

public sealed record HitCountDefinition(
    int Minimum,
    int Maximum,
    HitDistribution Distribution = HitDistribution.Fixed);

public abstract record InstantDeathResistanceCheckDefinition(InstantDeathResistanceMode Mode);

public sealed record ChannelInstantDeathResistanceCheckDefinition(InstantDeathChannel Channel)
    : InstantDeathResistanceCheckDefinition(InstantDeathResistanceMode.Channel);

public sealed record NoInstantDeathResistanceCheckDefinition()
    : InstantDeathResistanceCheckDefinition(InstantDeathResistanceMode.None);

public sealed record SkillCostDefinition(
    ContentId ResourceId,
    AmountDefinition Amount,
    bool CanReduceToZero = false);

public sealed record SkillInheritanceDefinition : IEquatable<SkillInheritanceDefinition>
{
    public SkillInheritanceDefinition(
        bool isInheritable,
        IEnumerable<ContentId>? exclusiveOwnerEntityIds = null)
    {
        IsInheritable = isInheritable;
        ExclusiveOwnerEntityIds = DefinitionCollections.Snapshot(exclusiveOwnerEntityIds);
    }

    public bool IsInheritable { get; }
    public IReadOnlyList<ContentId> ExclusiveOwnerEntityIds { get; }

    public bool Equals(SkillInheritanceDefinition? other)
    {
        return other is not null &&
               IsInheritable == other.IsInheritable &&
               ExclusiveOwnerEntityIds.SequenceEqual(other.ExclusiveOwnerEntityIds);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IsInheritable);
        foreach (ContentId ownerId in ExclusiveOwnerEntityIds)
        {
            hash.Add(ownerId);
        }

        return hash.ToHashCode();
    }
}

public sealed record SkillMutationDefinition(ContentId FamilyId, int Tier);

public sealed record SkillAvailabilityDefinition
{
    public SkillAvailabilityDefinition(IEnumerable<ContentId> contextIds)
    {
        ContextIds = DefinitionCollections.Snapshot(contextIds);
    }

    public IReadOnlyList<ContentId> ContextIds { get; }
}
