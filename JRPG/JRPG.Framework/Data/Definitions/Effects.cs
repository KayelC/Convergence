namespace JRPGPrototype.Data.Definitions;

public abstract record EffectDefinition(
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue);

public sealed record DamageEffectDefinition(
    DamageElement Element,
    int Power,
    int Accuracy,
    CriticalDefinition Critical,
    HitCountDefinition Hits,
    DamageDrainMode Drain = DamageDrainMode.None,
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue)
    : EffectDefinition(When, OnFailure);

public sealed record InstantKillEffectDefinition(
    int Chance,
    InstantDeathResistanceCheckDefinition ResistanceCheck,
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue)
    : EffectDefinition(When, OnFailure);

public sealed record ApplyAilmentEffectDefinition(
    ContentId AilmentId,
    int Chance,
    DurationDefinition? Duration = null,
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue)
    : EffectDefinition(When, OnFailure);

public sealed record RestoreResourceEffectDefinition(
    ContentId ResourceId,
    AmountDefinition Amount,
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue)
    : EffectDefinition(When, OnFailure);

public sealed record RemoveAilmentEffectDefinition : EffectDefinition
{
    public RemoveAilmentEffectDefinition(
        AilmentRemovalScope scope,
        IEnumerable<ContentId>? ailmentIds = null,
        IEnumerable<ContentId>? ailmentGroupIds = null,
        ConditionDefinition? when = null,
        EffectFailurePolicy onFailure = EffectFailurePolicy.Continue)
        : base(when, onFailure)
    {
        Scope = scope;
        AilmentIds = DefinitionCollections.Snapshot(ailmentIds);
        AilmentGroupIds = DefinitionCollections.Snapshot(ailmentGroupIds);
    }

    public AilmentRemovalScope Scope { get; }
    public IReadOnlyList<ContentId> AilmentIds { get; }
    public IReadOnlyList<ContentId> AilmentGroupIds { get; }
}

public sealed record ReviveEffectDefinition(
    ContentId ResourceId,
    AmountDefinition Amount,
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue)
    : EffectDefinition(When, OnFailure);

public sealed record ModifyStatStageEffectDefinition : EffectDefinition
{
    public ModifyStatStageEffectDefinition(
        IEnumerable<ContentId> modifierTrackIds,
        int stageDelta,
        DurationDefinition? duration = null,
        ConditionDefinition? when = null,
        EffectFailurePolicy onFailure = EffectFailurePolicy.Continue)
        : base(when, onFailure)
    {
        ModifierTrackIds = DefinitionCollections.Snapshot(modifierTrackIds);
        StageDelta = stageDelta;
        Duration = duration;
    }

    public IReadOnlyList<ContentId> ModifierTrackIds { get; }
    public int StageDelta { get; }
    public DurationDefinition? Duration { get; }
}

public sealed record GrantChargeEffectDefinition(
    ChargeKind Charge,
    decimal Multiplier,
    DurationDefinition? Duration = null,
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue)
    : EffectDefinition(When, OnFailure);

public sealed record GrantShieldEffectDefinition(
    ShieldKind Shield,
    DurationDefinition? Duration = null,
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue)
    : EffectDefinition(When, OnFailure);

public sealed record OverrideAffinityEffectDefinition : EffectDefinition
{
    public OverrideAffinityEffectDefinition(
        IEnumerable<DamageElement> elements,
        ElementalAffinity affinity,
        DurationDefinition duration,
        ConditionDefinition? when = null,
        EffectFailurePolicy onFailure = EffectFailurePolicy.Continue)
        : base(when, onFailure)
    {
        Elements = DefinitionCollections.Snapshot(elements);
        Affinity = affinity;
        Duration = duration;
    }

    public IReadOnlyList<DamageElement> Elements { get; }
    public ElementalAffinity Affinity { get; }
    public DurationDefinition Duration { get; }
}

public sealed record RemoveStatusEffectDefinition : EffectDefinition
{
    public RemoveStatusEffectDefinition(
        IEnumerable<StatusEffectKind> statusKinds,
        IEnumerable<ContentId>? statusIds = null,
        ConditionDefinition? when = null,
        EffectFailurePolicy onFailure = EffectFailurePolicy.Continue)
        : base(when, onFailure)
    {
        StatusKinds = DefinitionCollections.Snapshot(statusKinds);
        StatusIds = DefinitionCollections.Snapshot(statusIds);
    }

    public IReadOnlyList<StatusEffectKind> StatusKinds { get; }
    public IReadOnlyList<ContentId> StatusIds { get; }
}

public sealed record ReduceResourceEffectDefinition(
    ContentId ResourceId,
    AmountDefinition Amount,
    bool CanReduceToZero,
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue)
    : EffectDefinition(When, OnFailure);

public sealed record SetResourceEffectDefinition(
    ContentId ResourceId,
    AmountDefinition Amount,
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue)
    : EffectDefinition(When, OnFailure);

public sealed record AnalyzeEffectDefinition : EffectDefinition
{
    public AnalyzeEffectDefinition(
        IEnumerable<AnalysisLayer> layers,
        ConditionDefinition? when = null,
        EffectFailurePolicy onFailure = EffectFailurePolicy.Continue)
        : base(when, onFailure)
    {
        Layers = DefinitionCollections.Snapshot(layers);
    }

    public IReadOnlyList<AnalysisLayer> Layers { get; }
}

public sealed record EscapeEffectDefinition(
    ContentId EligibilityRuleId,
    int? Chance = null,
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue)
    : EffectDefinition(When, OnFailure);

public sealed record CustomEffectDefinition : EffectDefinition
{
    public CustomEffectDefinition(
        ContentId handlerId,
        IEnumerable<KeyValuePair<string, object?>>? parameters = null,
        ConditionDefinition? when = null,
        EffectFailurePolicy onFailure = EffectFailurePolicy.Continue)
        : base(when, onFailure)
    {
        HandlerId = handlerId;
        Parameters = DefinitionCollections.SnapshotParameters(parameters);
    }

    public ContentId HandlerId { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}
