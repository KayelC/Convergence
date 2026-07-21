using Convergence.Content;
using Convergence.Battle;
using Convergence.Runtime;

namespace Convergence.Execution;

public enum SkillExecutionStatus
{
    Executed,
    Rejected,
    Interrupted
}

public enum EffectExecutionOutcome
{
    Success,
    Failure,
    Skipped,
    Interrupted
}

/// <summary>Explains why an ordered effect was not dispatched.</summary>
public enum EffectExecutionSkipReason
{
    DependencyUnsatisfied,
    TargetLifeStateIneligible,
    ConditionUnsatisfied
}

/// <summary>Explains why an authored effect dependency did or did not pass.</summary>
public enum EffectDependencyEvaluationReason
{
    Satisfied,
    SourceResultMissing,
    SourceNotSuccessful,
    PositiveDamageNotDealt
}

/// <summary>Captures the typed dependency decision made before an effect executes.</summary>
public sealed record EffectDependencyEvaluation
{
    public EffectDependencyEvaluation(
        EffectLocalId sourceEffectId,
        int sourceEffectIndex,
        EffectDependencyRequirement requirement,
        EffectDependencyScope scope,
        RuntimeInstanceId? targetId,
        bool satisfied,
        EffectDependencyEvaluationReason reason)
    {
        if (!sourceEffectId.IsValid)
        {
            throw new ArgumentException("Dependency evaluation requires a valid source effect ID.", nameof(sourceEffectId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(sourceEffectIndex);
        if (!Enum.IsDefined(requirement))
        {
            throw new ArgumentOutOfRangeException(nameof(requirement));
        }
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }
        if (targetId is RuntimeInstanceId target && !target.IsValid)
        {
            throw new ArgumentException("Dependency target ID must be valid when supplied.", nameof(targetId));
        }
        if (!Enum.IsDefined(reason) || satisfied != (reason == EffectDependencyEvaluationReason.Satisfied))
        {
            throw new ArgumentException(
                "Dependency satisfaction and reason must describe the same decision.",
                nameof(reason));
        }

        SourceEffectId = sourceEffectId;
        SourceEffectIndex = sourceEffectIndex;
        Requirement = requirement;
        Scope = scope;
        TargetId = targetId;
        Satisfied = satisfied;
        Reason = reason;
    }

    public EffectLocalId SourceEffectId { get; }
    public int SourceEffectIndex { get; }
    public EffectDependencyRequirement Requirement { get; }
    public EffectDependencyScope Scope { get; }
    public RuntimeInstanceId? TargetId { get; }
    public bool Satisfied { get; }
    public EffectDependencyEvaluationReason Reason { get; }
}

public enum TurnEconomyOutcome
{
    Normal,
    Critical,
    Weakness,
    Miss,
    Null,
    Repel,
    Absorb
}

public enum SkillExecutionDiagnosticCode
{
    SkillMustBeActive,
    SkillHasNoEffects,
    ContextUnavailable,
    TargetingInvalid,
    TargetSelectionInvalid,
    ResourceMissing,
    InsufficientResource,
    EffectExecutorMissing,
    FormulaHandlerMissing,
    CustomEffectHandlerMissing,
    CustomConditionHandlerMissing,
    EscapeRuleHandlerMissing,
    AilmentMissing,
    NoApplicableEffect,
    AssessmentInvalid,
    ExecutionFailed,
    AuthoredPercentageOutOfRange,
    DuplicateResourceCost
}

public sealed record SkillExecutionDiagnostic(
    SkillExecutionDiagnosticCode Code,
    string Message,
    int? EffectIndex = null,
    RuntimeInstanceId? TargetId = null);

/// <summary>Describes one committed, signed change to an actor resource.</summary>
public sealed record ExecutionResourceChange
{
    public ExecutionResourceChange(
        RuntimeInstanceId actorId,
        ContentId resourceId,
        decimal delta)
    {
        if (!actorId.IsValid)
        {
            throw new ArgumentException("Resource changes require a valid actor ID.", nameof(actorId));
        }

        if (!resourceId.IsValid)
        {
            throw new ArgumentException("Resource changes require a valid resource ID.", nameof(resourceId));
        }

        if (delta == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Resource changes must be nonzero.");
        }

        ActorId = actorId;
        ResourceId = resourceId;
        Delta = delta;
    }

    public RuntimeInstanceId ActorId { get; }
    public ContentId ResourceId { get; }
    public decimal Delta { get; }
}

/// <summary>Captures one attempted damage hit and its staged runtime mutation.</summary>
public sealed class DamageHitExecutionEvidence
{
    public DamageHitExecutionEvidence(
        ContentId sourceActionId,
        RuntimeInstanceId actorId,
        RuntimeInstanceId targetId,
        int effectIndex,
        DamageHitResolution resolution,
        ElementalAffinity resolvedAffinity,
        RuntimeInstanceId? affectedActorId = null,
        ContentId? affectedResourceId = null,
        decimal appliedResourceDelta = 0m)
        : this(
            sourceActionId,
            actorId,
            targetId,
            effectIndex,
            resolution,
            resolvedAffinity,
            affectedActorId,
            affectedResourceId,
            appliedResourceDelta,
            DamageContactMode.Independent,
            contactSourceEffectId: null,
            contactSourceEffectIndex: null)
    {
    }

    public DamageHitExecutionEvidence(
        ContentId sourceActionId,
        RuntimeInstanceId actorId,
        RuntimeInstanceId targetId,
        int effectIndex,
        DamageHitResolution resolution,
        ElementalAffinity resolvedAffinity,
        RuntimeInstanceId? affectedActorId,
        ContentId? affectedResourceId,
        decimal appliedResourceDelta,
        DamageContactMode contactMode,
        EffectLocalId? contactSourceEffectId,
        int? contactSourceEffectIndex)
    {
        if (!sourceActionId.IsValid)
        {
            throw new ArgumentException("Damage-hit evidence requires a valid source action ID.", nameof(sourceActionId));
        }
        if (!actorId.IsValid)
        {
            throw new ArgumentException("Damage-hit evidence requires a valid acting actor ID.", nameof(actorId));
        }
        if (!targetId.IsValid)
        {
            throw new ArgumentException("Damage-hit evidence requires a valid target actor ID.", nameof(targetId));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(effectIndex);
        Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        if (!Enum.IsDefined(resolvedAffinity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolvedAffinity),
                resolvedAffinity,
                "Resolved affinity must be defined.");
        }
        if (affectedActorId is RuntimeInstanceId affectedActor && !affectedActor.IsValid)
        {
            throw new ArgumentException("Affected actor ID must be valid when supplied.", nameof(affectedActorId));
        }
        if (affectedResourceId is ContentId resourceId && !resourceId.IsValid)
        {
            throw new ArgumentException("Affected resource ID must be valid when supplied.", nameof(affectedResourceId));
        }
        if ((affectedActorId is null) != (affectedResourceId is null))
        {
            throw new ArgumentException("Affected actor and resource IDs must be supplied together.");
        }
        if (appliedResourceDelta != 0m && affectedActorId is null)
        {
            throw new ArgumentException("A nonzero resource delta requires an affected actor and resource.");
        }
        if (!Enum.IsDefined(contactMode))
        {
            throw new ArgumentOutOfRangeException(nameof(contactMode), contactMode, "Damage contact mode must be defined.");
        }
        if (contactMode == DamageContactMode.SharedContact)
        {
            if (contactSourceEffectId is not EffectLocalId sourceId || !sourceId.IsValid)
            {
                throw new ArgumentException(
                    "Shared-contact evidence requires a valid source effect ID.",
                    nameof(contactSourceEffectId));
            }
            if (contactSourceEffectIndex is not int sourceIndex || sourceIndex < 0 || sourceIndex >= effectIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contactSourceEffectIndex),
                    contactSourceEffectIndex,
                    "Shared-contact evidence requires an earlier source effect index.");
            }
        }
        else if (contactSourceEffectId is not null || contactSourceEffectIndex is not null)
        {
            throw new ArgumentException("Independent damage cannot declare shared-contact source evidence.");
        }

        SourceActionId = sourceActionId;
        ActorId = actorId;
        TargetId = targetId;
        EffectIndex = effectIndex;
        ResolvedAffinity = resolvedAffinity;
        AffectedActorId = affectedActorId;
        AffectedResourceId = affectedResourceId;
        AppliedResourceDelta = appliedResourceDelta;
        ContactMode = contactMode;
        ContactSourceEffectId = contactSourceEffectId;
        ContactSourceEffectIndex = contactSourceEffectIndex;
    }

    public ContentId SourceActionId { get; }
    public RuntimeInstanceId ActorId { get; }
    public RuntimeInstanceId TargetId { get; }
    public int EffectIndex { get; }
    public int HitIndex => Resolution.HitIndex;
    public bool Hit => Resolution.Hit;
    public decimal ResolvedDamage => Resolution.Damage;
    public bool Critical => Resolution.Critical;
    public int? AuthoredAccuracy => Resolution.AuthoredAccuracy;
    public int? FinalAccuracy => Resolution.FinalAccuracy;
    public decimal? AccuracyRoll => Resolution.AccuracyRoll;
    public bool? CriticalEligible => Resolution.CriticalEligible;
    public CriticalEligibilityReason? CriticalEligibilityReason => Resolution.CriticalEligibilityReason;
    public int? CriticalChance => Resolution.CriticalChance;
    public decimal? CriticalRoll => Resolution.CriticalRoll;
    public ElementalAffinity ResolvedAffinity { get; }
    public ChargeKind? ChargeKind => Resolution.ChargeKind;
    public decimal ChargeMultiplier => Resolution.ChargeMultiplier;
    public RuntimeInstanceId? AffectedActorId { get; }
    public ContentId? AffectedResourceId { get; }
    public decimal AppliedResourceDelta { get; }
    public DamageContactMode ContactMode { get; }
    public EffectLocalId? ContactSourceEffectId { get; }
    public int? ContactSourceEffectIndex { get; }
    private DamageHitResolution Resolution { get; }
}

public sealed record EffectExecutionResult
{
    private EffectLocalId? _effectId;
    private readonly IReadOnlyList<PassiveTriggerExecutionResult> _passiveActivations =
        Array.Empty<PassiveTriggerExecutionResult>();
    private readonly IReadOnlyList<ContentId> _hostActionRequestIds = Array.Empty<ContentId>();
    private readonly IReadOnlyList<ExecutionResourceChange> _resourceChanges =
        Array.Empty<ExecutionResourceChange>();
    private readonly IReadOnlyList<StatModifierTransitionResult> _statModifierTransitions =
        Array.Empty<StatModifierTransitionResult>();
    private readonly IReadOnlyList<DamageHitExecutionEvidence> _damageHits =
        Array.Empty<DamageHitExecutionEvidence>();

    public EffectExecutionResult(
        int EffectIndex,
        RuntimeInstanceId? TargetId,
        EffectExecutionOutcome Outcome,
        TurnEconomyOutcome TurnEconomyOutcome = TurnEconomyOutcome.Normal,
        bool IsCritical = false,
        decimal? Value = null,
        ContentId? RelatedId = null,
        string? Detail = null,
        bool EscapeRequested = false,
        IReadOnlyList<PassiveTriggerExecutionResult>? PassiveActivations = null,
        ElementalAffinity? ResolvedAffinity = null,
        IReadOnlyList<ContentId>? HostActionRequestIds = null,
        IReadOnlyList<StatModifierTransitionResult>? StatModifierTransitions = null,
        IReadOnlyList<DamageHitExecutionEvidence>? DamageHits = null)
    {
        this.EffectIndex = EffectIndex;
        this.TargetId = TargetId;
        this.Outcome = Outcome;
        this.TurnEconomyOutcome = TurnEconomyOutcome;
        this.IsCritical = IsCritical;
        this.Value = Value;
        this.RelatedId = RelatedId;
        this.Detail = Detail;
        this.EscapeRequested = EscapeRequested;
        this.PassiveActivations = Array.AsReadOnly(PassiveActivations?.ToArray() ?? []);
        this.ResolvedAffinity = ResolvedAffinity;
        this.HostActionRequestIds = Array.AsReadOnly(HostActionRequestIds?.ToArray() ?? []);
        this.ResourceChanges = [];
        this.StatModifierTransitions = StatModifierTransitions ?? [];
        this.DamageHits = DamageHits ?? [];
    }

    public int EffectIndex { get; init; }
    public RuntimeInstanceId? TargetId { get; init; }
    public EffectExecutionOutcome Outcome { get; init; }
    public TurnEconomyOutcome TurnEconomyOutcome { get; init; }
    public bool IsCritical { get; init; }
    public decimal? Value { get; init; }
    public ContentId? RelatedId { get; init; }
    public string? Detail { get; init; }
    public bool EscapeRequested { get; init; }
    /// <summary>Gets the typed reason for a skipped effect, when applicable.</summary>
    public EffectExecutionSkipReason? SkipReason { get; init; }
    /// <summary>Gets the life state required when a target was dynamically ineligible.</summary>
    public TargetLifeState? RequiredTargetLifeState { get; init; }
    /// <summary>Gets the optional authored local ID of this effect.</summary>
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
    /// <summary>Gets the dependency decision made before this effect was considered.</summary>
    public EffectDependencyEvaluation? DependencyEvaluation { get; init; }
    public IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations
    {
        get => _passiveActivations;
        init => _passiveActivations = Array.AsReadOnly(value?.ToArray() ?? []);
    }
    public ElementalAffinity? ResolvedAffinity { get; init; }
    public IReadOnlyList<ContentId> HostActionRequestIds
    {
        get => _hostActionRequestIds;
        init => _hostActionRequestIds = Array.AsReadOnly(value?.ToArray() ?? []);
    }
    /// <summary>Gets the exact resource mutations committed while resolving this effect.</summary>
    public IReadOnlyList<ExecutionResourceChange> ResourceChanges
    {
        get => _resourceChanges;
        init => _resourceChanges = Array.AsReadOnly(value?.ToArray() ?? []);
    }
    /// <summary>Gets the canonical modifier transitions committed by this effect.</summary>
    public IReadOnlyList<StatModifierTransitionResult> StatModifierTransitions
    {
        get => _statModifierTransitions;
        init => _statModifierTransitions = Array.AsReadOnly(value?.ToArray() ?? []);
    }
    /// <summary>Gets ordered immutable evidence for every attempted damage hit.</summary>
    public IReadOnlyList<DamageHitExecutionEvidence> DamageHits
    {
        get => _damageHits;
        init => _damageHits = Array.AsReadOnly(value?.ToArray() ?? []);
    }
}

public sealed record EffectExecutionEnvironment
{
    public EffectExecutionEnvironment(
        ContentId contextId,
        ContentId? battleKindId = null,
        ContentId? moonPhaseId = null,
        IEnumerable<StatModifierLifecycleBoundary>? activeStatModifierBoundaries = null)
    {
        StatModifierLifecycleBoundary[] boundaries =
            (activeStatModifierBoundaries ?? []).ToArray();
        if (boundaries.Any(boundary => boundary is null) ||
            boundaries.Any(boundary => !boundary.EventId.IsValid || boundary.Sequence <= 0) ||
            boundaries.Select(boundary => boundary.EventId).Distinct().Count() != boundaries.Length)
        {
            throw new ArgumentException(
                "Active stat-modifier boundaries must be valid and unique by event ID.",
                nameof(activeStatModifierBoundaries));
        }

        ContextId = contextId;
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        ActiveStatModifierBoundaries = Array.AsReadOnly(boundaries);
    }

    public ContentId ContextId { get; }
    public ContentId? BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public IReadOnlyList<StatModifierLifecycleBoundary> ActiveStatModifierBoundaries { get; }

    public StatModifierLifecycleBoundary? FindStatModifierBoundary(DurationDefinition? duration) =>
        duration is TurnDurationDefinition turns
            ? ActiveStatModifierBoundaries.FirstOrDefault(boundary =>
                boundary.EventId == turns.TickEventId)
            : null;
}

internal enum EffectExecutionPurpose
{
    Standard,
    DefeatPrevention
}

public sealed record EffectActionExecutionRequest
{
    public EffectActionExecutionRequest(
        ContentId sourceId,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        EffectExecutionEnvironment environment,
        TargetingDefinition targeting,
        IEnumerable<RuntimeInstanceId>? selectedTargetIds = null,
        SkillDefinition? skill = null,
        ItemDefinition? item = null)
        : this(
            sourceId,
            actor,
            participants,
            environment,
            targeting,
            selectedTargetIds,
            skill,
            item,
            EffectExecutionPurpose.Standard)
    {
    }

    internal EffectActionExecutionRequest(
        ContentId sourceId,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        EffectExecutionEnvironment environment,
        TargetingDefinition targeting,
        IEnumerable<RuntimeInstanceId>? selectedTargetIds,
        SkillDefinition? skill,
        ItemDefinition? item,
        EffectExecutionPurpose purpose)
    {
        SourceId = sourceId;
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Participants = Array.AsReadOnly(
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        SelectedTargetIds = Array.AsReadOnly(selectedTargetIds?.ToArray() ?? []);
        Skill = skill;
        Item = item;
        Purpose = purpose;
    }

    public ContentId SourceId { get; }
    public RuntimeActorState Actor { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public EffectExecutionEnvironment Environment { get; }
    public TargetingDefinition Targeting { get; }
    public IReadOnlyList<RuntimeInstanceId> SelectedTargetIds { get; }
    public SkillDefinition? Skill { get; }
    public ItemDefinition? Item { get; }
    internal EffectExecutionPurpose Purpose { get; }
    public ContentId ContextId => Environment.ContextId;
    public ContentId? BattleKindId => Environment.BattleKindId;
    public ContentId? MoonPhaseId => Environment.MoonPhaseId;
}

public sealed record SkillExecutionAssessment
{
    internal SkillExecutionAssessment(
        IEnumerable<SkillExecutionDiagnostic> diagnostics,
        ResolvedTargetSet? targets,
        IEnumerable<ResolvedSkillCost> costs,
        object authority,
        SkillExecutionRequest request)
    {
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        TargetIds = Array.AsReadOnly(targets?.Targets.Select(target => target.InstanceId).ToArray() ?? []);
        HasResolvedTargets = targets is not null;
        IsUntargeted = targets?.IsUntargeted == true;
        Costs = Array.AsReadOnly(costs.ToArray());
        Preparation = new ExecutionAssessmentToken<SkillExecutionRequest>(authority, request);
    }

    public bool CanExecute => Diagnostics.Count == 0 && HasResolvedTargets;
    public IReadOnlyList<SkillExecutionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<RuntimeInstanceId> TargetIds { get; }
    internal bool HasResolvedTargets { get; }
    internal bool IsUntargeted { get; }
    internal IReadOnlyList<ResolvedSkillCost> Costs { get; }
    internal ExecutionAssessmentToken<SkillExecutionRequest> Preparation { get; }
}

internal sealed record ResolvedSkillCost(
    ContentId ResourceId,
    decimal Amount,
    bool CanReduceToZero);

public sealed record TurnEconomyResolution
{
    public TurnEconomyResolution(
        TurnEconomyOutcome Outcome,
        bool AnyCritical,
        bool TerminatesPhase)
    {
        if (!Enum.IsDefined(Outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(Outcome));
        }

        this.Outcome = Outcome;
        this.AnyCritical = AnyCritical;
        this.TerminatesPhase = TerminatesPhase;
    }

    public TurnEconomyOutcome Outcome { get; }
    public bool AnyCritical { get; }
    public bool TerminatesPhase { get; }

    public void Deconstruct(
        out TurnEconomyOutcome Outcome,
        out bool AnyCritical,
        out bool TerminatesPhase)
    {
        Outcome = this.Outcome;
        AnyCritical = this.AnyCritical;
        TerminatesPhase = this.TerminatesPhase;
    }
}

public sealed record SkillExecutionResult
{
    internal SkillExecutionResult(
        SkillExecutionStatus status,
        IEnumerable<EffectExecutionResult> effects,
        IEnumerable<SkillExecutionDiagnostic>? diagnostics = null,
        bool costsCommitted = false,
        IEnumerable<ExecutionResourceChange>? committedCostChanges = null,
        TurnEconomyResolution? turnEconomy = null)
    {
        Status = status;
        Effects = Array.AsReadOnly(effects.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics?.ToArray() ?? []);
        CostsCommitted = costsCommitted;
        CommittedCostChanges = Array.AsReadOnly(committedCostChanges?.ToArray() ?? []);
        EscapeRequested = Effects.Any(effect => effect.EscapeRequested);
        PassiveActivations = Array.AsReadOnly(
            Effects.SelectMany(effect => effect.PassiveActivations ?? []).ToArray());
        HostActionRequestIds = Array.AsReadOnly(
            Effects.SelectMany(effect => effect.HostActionRequestIds ?? []).ToArray());
        TurnEconomy = turnEconomy ?? new TurnEconomyResolution(TurnEconomyOutcome.Normal, false, false);
    }

    public SkillExecutionStatus Status { get; }
    public IReadOnlyList<EffectExecutionResult> Effects { get; }
    public IReadOnlyList<SkillExecutionDiagnostic> Diagnostics { get; }
    public bool CostsCommitted { get; }
    /// <summary>Gets the exact resource mutations committed as skill costs before effect execution.</summary>
    public IReadOnlyList<ExecutionResourceChange> CommittedCostChanges { get; }
    public bool EscapeRequested { get; }
    public IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations { get; }
    public IReadOnlyList<ContentId> HostActionRequestIds { get; }
    public TurnEconomyResolution TurnEconomy { get; }

    public static SkillExecutionResult Rejected(IEnumerable<SkillExecutionDiagnostic> diagnostics) =>
        new(SkillExecutionStatus.Rejected, [], diagnostics);

}

public sealed record SkillExecutionRequest
{
    public SkillExecutionRequest(
        SkillDefinition skill,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        ContentId contextId,
        ContentId? battleKindId,
        ContentId? moonPhaseId,
        IEnumerable<RuntimeInstanceId>? selectedTargetIds = null)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(participants);

        Skill = skill;
        Actor = actor;
        Participants = Array.AsReadOnly(participants.ToArray());
        ContextId = contextId;
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        SelectedTargetIds = Array.AsReadOnly(selectedTargetIds?.ToArray() ?? []);
        Environment = new EffectExecutionEnvironment(contextId, battleKindId, moonPhaseId);
    }

    public SkillDefinition Skill { get; }
    public RuntimeActorState Actor { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public ContentId ContextId { get; }
    public ContentId? BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public IReadOnlyList<RuntimeInstanceId> SelectedTargetIds { get; }

    public SkillExecutionRequest(
        SkillDefinition skill,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        EffectExecutionEnvironment environment,
        IEnumerable<RuntimeInstanceId>? selectedTargetIds = null)
        : this(
            skill,
            actor,
            participants,
            environment.ContextId,
            environment.BattleKindId,
            environment.MoonPhaseId,
            selectedTargetIds)
    {
        Environment = environment;
    }

    public EffectExecutionEnvironment Environment { get; private init; }

    internal EffectActionExecutionRequest ToEffectActionRequest() =>
        new(
            Skill.Id,
            Actor,
            Participants,
            Environment,
            Skill.Targeting ?? throw new InvalidOperationException("Active skill targeting is missing."),
            SelectedTargetIds,
            skill: Skill);
}

internal sealed record ResolvedRuntimeTargetSet
{
    public ResolvedRuntimeTargetSet(IEnumerable<RuntimeActorState> targets, bool isUntargeted = false)
    {
        Targets = Array.AsReadOnly(targets.ToArray());
        IsUntargeted = isUntargeted;
    }

    public IReadOnlyList<RuntimeActorState> Targets { get; }
    public bool IsUntargeted { get; }
}

public sealed record ResolvedTargetSet
{
    internal ResolvedTargetSet(IEnumerable<RuntimeActorState> targets, bool isUntargeted = false)
    {
        Targets = Array.AsReadOnly(targets.ToArray());
        IsUntargeted = isUntargeted;
    }

    public IReadOnlyList<RuntimeActorState> Targets { get; }
    public bool IsUntargeted { get; }
}

public sealed record EffectExecutionContext(
    EffectActionExecutionRequest Request,
    BattleExecutionServices Services,
    int EffectIndex,
    EffectDefinition Effect,
    RuntimeActorState? Target,
    DamageElement? EffectElement = null)
{
    public RuntimeActorState Actor => Request.Actor;

    internal EffectDependencyEvaluation? DependencyEvaluation { get; init; }
}

public interface IEffectExecutor<in TDefinition> where TDefinition : EffectDefinition
{
    EffectExecutionResult Execute(TDefinition definition, EffectExecutionContext context);
}

internal interface IEffectExecutorAdapter
{
    Type DefinitionType { get; }
    EffectExecutionResult Execute(EffectDefinition definition, EffectExecutionContext context);
}

internal sealed class EffectExecutorAdapter<TDefinition>(IEffectExecutor<TDefinition> executor)
    : IEffectExecutorAdapter
    where TDefinition : EffectDefinition
{
    public Type DefinitionType => typeof(TDefinition);
    public EffectExecutionResult Execute(EffectDefinition definition, EffectExecutionContext context) =>
        executor.Execute((TDefinition)definition, context);
}

public sealed class EffectExecutorRegistry
{
    private readonly Dictionary<Type, IEffectExecutorAdapter> _executors = [];

    public EffectExecutorRegistry Register<TDefinition>(IEffectExecutor<TDefinition> executor)
        where TDefinition : EffectDefinition
    {
        ArgumentNullException.ThrowIfNull(executor);
        _executors.Add(typeof(TDefinition), new EffectExecutorAdapter<TDefinition>(executor));
        return this;
    }

    public bool Supports(Type definitionType) => _executors.ContainsKey(definitionType);

    internal EffectExecutionResult Execute(EffectDefinition definition, EffectExecutionContext context) =>
        _executors.TryGetValue(definition.GetType(), out IEffectExecutorAdapter? executor)
            ? executor.Execute(definition, context)
            : throw new InvalidOperationException($"No effect executor is registered for '{definition.GetType().Name}'.");

    public static EffectExecutorRegistry CreateDefault() => new EffectExecutorRegistry()
        .Register(new DamageEffectExecutor())
        .Register(new InstantKillEffectExecutor())
        .Register(new ApplyAilmentEffectExecutor())
        .Register(new RestoreResourceEffectExecutor())
        .Register(new RemoveAilmentEffectExecutor())
        .Register(new ReviveEffectExecutor())
        .Register(new ModifyStatStageEffectExecutor())
        .Register(new GrantChargeEffectExecutor())
        .Register(new GrantShieldEffectExecutor())
        .Register(new BreakAffinityEffectExecutor())
        .Register(new OverrideAffinityEffectExecutor())
        .Register(new RemoveStatusEffectExecutor())
        .Register(new ReduceResourceEffectExecutor())
        .Register(new SetResourceEffectExecutor())
        .Register(new AnalyzeEffectExecutor())
        .Register(new EscapeEffectExecutor())
        .Register(new CustomEffectExecutor());
}
