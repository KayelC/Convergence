using Convergence.Content;
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
    AssessmentInvalid,
    ExecutionFailed
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

public sealed record EffectExecutionResult
{
    private readonly IReadOnlyList<PassiveTriggerExecutionResult> _passiveActivations =
        Array.Empty<PassiveTriggerExecutionResult>();
    private readonly IReadOnlyList<ContentId> _hostActionRequestIds = Array.Empty<ContentId>();
    private readonly IReadOnlyList<ExecutionResourceChange> _resourceChanges =
        Array.Empty<ExecutionResourceChange>();

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
        IReadOnlyList<ContentId>? HostActionRequestIds = null)
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
}

public sealed record EffectExecutionEnvironment
{
    public EffectExecutionEnvironment(
        ContentId contextId,
        ContentId? battleKindId = null,
        ContentId? moonPhaseId = null)
    {
        ContextId = contextId;
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
    }

    public ContentId ContextId { get; }
    public ContentId? BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
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
    }

    public ContentId SourceId { get; }
    public RuntimeActorState Actor { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public EffectExecutionEnvironment Environment { get; }
    public TargetingDefinition Targeting { get; }
    public IReadOnlyList<RuntimeInstanceId> SelectedTargetIds { get; }
    public SkillDefinition? Skill { get; }
    public ItemDefinition? Item { get; }
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

public sealed record TurnEconomyResolution(TurnEconomyOutcome Outcome, bool AnyCritical, bool TerminatesPhase);

public sealed record SkillExecutionResult
{
    internal SkillExecutionResult(
        SkillExecutionStatus status,
        IEnumerable<EffectExecutionResult> effects,
        IEnumerable<SkillExecutionDiagnostic>? diagnostics = null,
        bool costsCommitted = false,
        IEnumerable<ExecutionResourceChange>? committedCostChanges = null)
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
        TurnEconomy = AggregateTurnEconomy(Effects);
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

    private static TurnEconomyResolution AggregateTurnEconomy(IReadOnlyList<EffectExecutionResult> effects)
    {
        EffectExecutionResult? interruption = effects.FirstOrDefault(effect =>
            effect.TurnEconomyOutcome is TurnEconomyOutcome.Repel or TurnEconomyOutcome.Absorb);
        if (interruption is not null)
        {
            return new TurnEconomyResolution(interruption.TurnEconomyOutcome, effects.Any(effect => effect.IsCritical), true);
        }

        TurnEconomyOutcome outcome = effects.Any(effect => effect.TurnEconomyOutcome == TurnEconomyOutcome.Null)
            ? TurnEconomyOutcome.Null
            : effects.Any(effect => effect.TurnEconomyOutcome == TurnEconomyOutcome.Miss)
                ? TurnEconomyOutcome.Miss
                : effects.Any(effect => effect.TurnEconomyOutcome == TurnEconomyOutcome.Weakness)
                    ? TurnEconomyOutcome.Weakness
                    : effects.Any(effect => effect.IsCritical)
                        ? TurnEconomyOutcome.Critical
                        : TurnEconomyOutcome.Normal;

        return new TurnEconomyResolution(outcome, effects.Any(effect => effect.IsCritical), false);
    }
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
