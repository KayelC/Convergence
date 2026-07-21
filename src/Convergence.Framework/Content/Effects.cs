namespace Convergence.Content;

/// <summary>Identifies one effect within a single authored effect sequence.</summary>
public readonly record struct EffectLocalId
{
    private readonly string? _value;

    public EffectLocalId(string value)
    {
        ContentId parsed = ContentId.Parse(value);
        if (parsed.IsQualified)
        {
            throw new ArgumentException(
                "Effect-local IDs cannot contain a content-pack qualifier.",
                nameof(value));
        }

        _value = parsed.Value;
    }

    /// <summary>Gets the normalized lower-snake-case value.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Gets whether this value was constructed as a valid local ID.</summary>
    public bool IsValid => _value is not null;

    /// <summary>Creates a normalized effect-local ID.</summary>
    public static EffectLocalId Parse(string value) => new(value);

    /// <summary>Attempts to create a normalized effect-local ID.</summary>
    public static bool TryParse(string? value, out EffectLocalId effectId)
    {
        if (value is null)
        {
            effectId = default;
            return false;
        }

        try
        {
            effectId = new EffectLocalId(value);
            return true;
        }
        catch (ArgumentException)
        {
            effectId = default;
            return false;
        }
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies the source fact required by a dependent effect.</summary>
public enum EffectDependencyRequirement
{
    Succeeded,
    PositiveDamage
}

/// <summary>Defines which source targets may satisfy an effect dependency.</summary>
public enum EffectDependencyScope
{
    SameTarget,
    AnyTarget
}

/// <summary>Declares an explicit dependency on an earlier effect in the same sequence.</summary>
public sealed record EffectDependencyDefinition
{
    public EffectDependencyDefinition(
        EffectLocalId sourceEffectId,
        EffectDependencyRequirement requirement,
        EffectDependencyScope scope)
    {
        if (!sourceEffectId.IsValid)
        {
            throw new ArgumentException(
                "Effect dependencies require a valid source effect ID.",
                nameof(sourceEffectId));
        }

        if (!Enum.IsDefined(requirement))
        {
            throw new ArgumentOutOfRangeException(nameof(requirement));
        }

        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        SourceEffectId = sourceEffectId;
        Requirement = requirement;
        Scope = scope;
    }

    /// <summary>Gets the earlier effect whose result supplies the dependency fact.</summary>
    public EffectLocalId SourceEffectId { get; }

    /// <summary>Gets the fact that must be established by the source effect.</summary>
    public EffectDependencyRequirement Requirement { get; }

    /// <summary>Gets whether the fact must belong to the same target or any target.</summary>
    public EffectDependencyScope Scope { get; }
}

public abstract record EffectDefinition(
    ConditionDefinition? When = null,
    EffectFailurePolicy OnFailure = EffectFailurePolicy.Continue)
{
    private EffectLocalId? _effectId;

    /// <summary>Gets the optional ID used by later effects in this sequence.</summary>
    public EffectLocalId? EffectId
    {
        get => _effectId;
        init
        {
            if (value.HasValue && !value.Value.IsValid)
            {
                throw new ArgumentException("Effect ID must be valid when supplied.", nameof(value));
            }

            _effectId = value;
        }
    }

    /// <summary>Gets the optional dependency on an earlier effect in this sequence.</summary>
    public EffectDependencyDefinition? Dependency { get; init; }
}

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

public sealed record BreakAffinityEffectDefinition : EffectDefinition
{
    public BreakAffinityEffectDefinition(
        IEnumerable<DamageElement> elements,
        DurationDefinition duration,
        ConditionDefinition? when = null,
        EffectFailurePolicy onFailure = EffectFailurePolicy.Continue)
        : base(when, onFailure)
    {
        Elements = DefinitionCollections.Snapshot(elements);
        Duration = duration ?? throw new ArgumentNullException(nameof(duration));
    }

    public IReadOnlyList<DamageElement> Elements { get; }
    public DurationDefinition Duration { get; }
}

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
