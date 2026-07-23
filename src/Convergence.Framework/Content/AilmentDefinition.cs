namespace Convergence.Content;

public sealed record AilmentModifiersDefinition(
    decimal EvasionMultiplier,
    int CriticalChanceTakenBonus,
    decimal DamageTakenMultiplier,
    decimal DamageDealtMultiplier,
    bool IsRigidBody);

public abstract record AilmentTurnBehaviorDefinition(AilmentTurnBehaviorKind Kind);

public sealed record NormalAilmentTurnBehaviorDefinition()
    : AilmentTurnBehaviorDefinition(AilmentTurnBehaviorKind.Normal);

public sealed record SkipAilmentTurnBehaviorDefinition()
    : AilmentTurnBehaviorDefinition(AilmentTurnBehaviorKind.Skip);

public sealed record LimitedActionsAilmentTurnBehaviorDefinition : AilmentTurnBehaviorDefinition
{
    public LimitedActionsAilmentTurnBehaviorDefinition(IEnumerable<ContentId> allowedActionIds)
        : base(AilmentTurnBehaviorKind.LimitedActions)
    {
        AllowedActionIds = DefinitionCollections.Snapshot(allowedActionIds);
    }

    public IReadOnlyList<ContentId> AllowedActionIds { get; }
}

public sealed record ChanceSkipAilmentTurnBehaviorDefinition(int SkipChance)
    : AilmentTurnBehaviorDefinition(AilmentTurnBehaviorKind.ChanceSkip);

public sealed record ChanceSkipOrFleeAilmentTurnBehaviorDefinition(
    int SkipChance,
    int FleeChance,
    CompanionFleeOutcome CompanionFleeOutcome)
    : AilmentTurnBehaviorDefinition(AilmentTurnBehaviorKind.ChanceSkipOrFlee);

public sealed record ForcedBasicAttackAilmentTurnBehaviorDefinition()
    : AilmentTurnBehaviorDefinition(AilmentTurnBehaviorKind.ForcedBasicAttack);

public sealed record ConfusedActionAilmentTurnBehaviorDefinition()
    : AilmentTurnBehaviorDefinition(AilmentTurnBehaviorKind.ConfusedAction);

public sealed record CustomAilmentTurnBehaviorDefinition : AilmentTurnBehaviorDefinition
{
    public CustomAilmentTurnBehaviorDefinition(
        ContentId handlerId,
        IEnumerable<KeyValuePair<string, object?>>? parameters = null)
        : base(AilmentTurnBehaviorKind.Custom)
    {
        HandlerId = handlerId;
        Parameters = DefinitionCollections.SnapshotParameters(parameters);
    }

    public ContentId HandlerId { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}

public sealed record NaturalAilmentRecoveryDefinition(
    int BaseChance,
    ContentId StatId,
    decimal StatMultiplier);

public sealed record AilmentRecoveryDefinition
{
    public AilmentRecoveryDefinition(
        NaturalAilmentRecoveryDefinition? natural = null,
        IEnumerable<ContentId>? removeOnEventIds = null)
    {
        Natural = natural;
        RemoveOnEventIds = DefinitionCollections.Snapshot(removeOnEventIds);
    }

    public NaturalAilmentRecoveryDefinition? Natural { get; }
    public IReadOnlyList<ContentId> RemoveOnEventIds { get; }
}

public sealed record AilmentDefinition
{
    public AilmentDefinition(
        ContentId id,
        string displayName,
        string description,
        StatusLifetimeDefinition defaultLifetime,
        AilmentTurnBehaviorDefinition turnBehavior,
        AilmentModifiersDefinition modifiers,
        AilmentRecoveryDefinition recovery,
        IEnumerable<ContentId>? groupIds = null,
        ContentId? exclusivityGroupId = null,
        IEnumerable<PassiveTriggerDefinition>? triggers = null)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        GroupIds = DefinitionCollections.Snapshot(groupIds);
        ExclusivityGroupId = exclusivityGroupId;
        DefaultLifetime = defaultLifetime ?? throw new ArgumentNullException(nameof(defaultLifetime));
        TurnBehavior = turnBehavior;
        Modifiers = modifiers;
        Triggers = DefinitionCollections.Snapshot(triggers);
        Recovery = recovery;
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<ContentId> GroupIds { get; }
    public ContentId? ExclusivityGroupId { get; }
    public StatusLifetimeDefinition DefaultLifetime { get; }
    public AilmentTurnBehaviorDefinition TurnBehavior { get; }
    public AilmentModifiersDefinition Modifiers { get; }
    public IReadOnlyList<PassiveTriggerDefinition> Triggers { get; }
    public AilmentRecoveryDefinition Recovery { get; }
}
