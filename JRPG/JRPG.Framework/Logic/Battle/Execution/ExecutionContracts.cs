using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Battle.Execution;

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

public enum PressTurnOutcome
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
    AilmentMissing
}

public sealed record SkillExecutionDiagnostic(
    SkillExecutionDiagnosticCode Code,
    string Message,
    int? EffectIndex = null,
    ContentId? TargetId = null);

public sealed record EffectExecutionResult
{
    public EffectExecutionResult(
        int EffectIndex,
        ContentId? TargetId,
        EffectExecutionOutcome Outcome,
        PressTurnOutcome PressTurnOutcome = PressTurnOutcome.Normal,
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
        this.PressTurnOutcome = PressTurnOutcome;
        this.IsCritical = IsCritical;
        this.Value = Value;
        this.RelatedId = RelatedId;
        this.Detail = Detail;
        this.EscapeRequested = EscapeRequested;
        this.PassiveActivations = Array.AsReadOnly(PassiveActivations?.ToArray() ?? []);
        this.ResolvedAffinity = ResolvedAffinity;
        this.HostActionRequestIds = Array.AsReadOnly(HostActionRequestIds?.ToArray() ?? []);
    }

    public int EffectIndex { get; init; }
    public ContentId? TargetId { get; init; }
    public EffectExecutionOutcome Outcome { get; init; }
    public PressTurnOutcome PressTurnOutcome { get; init; }
    public bool IsCritical { get; init; }
    public decimal? Value { get; init; }
    public ContentId? RelatedId { get; init; }
    public string? Detail { get; init; }
    public bool EscapeRequested { get; init; }
    public IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations { get; init; }
    public ElementalAffinity? ResolvedAffinity { get; init; }
    public IReadOnlyList<ContentId> HostActionRequestIds { get; init; }
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
        IEnumerable<ContentId>? selectedTargetIds = null,
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
    public IReadOnlyList<ContentId> SelectedTargetIds { get; }
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
        IEnumerable<ResolvedSkillCost> costs)
    {
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        TargetIds = Array.AsReadOnly(targets?.Targets.Select(target => target.InstanceId).ToArray() ?? []);
        Targets = targets;
        Costs = Array.AsReadOnly(costs.ToArray());
    }

    public bool CanExecute => Diagnostics.Count == 0 && Targets is not null;
    public IReadOnlyList<SkillExecutionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<ContentId> TargetIds { get; }
    internal ResolvedTargetSet? Targets { get; }
    internal IReadOnlyList<ResolvedSkillCost> Costs { get; }
}

internal sealed record ResolvedSkillCost(ContentId ResourceId, decimal Amount);

public sealed record PressTurnResolution(PressTurnOutcome Outcome, bool AnyCritical, bool TerminatesPhase);

public sealed record SkillExecutionResult
{
    internal SkillExecutionResult(
        SkillExecutionStatus status,
        IEnumerable<EffectExecutionResult> effects,
        IEnumerable<SkillExecutionDiagnostic>? diagnostics = null,
        bool costsCommitted = false)
    {
        Status = status;
        Effects = Array.AsReadOnly(effects.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics?.ToArray() ?? []);
        CostsCommitted = costsCommitted;
        EscapeRequested = Effects.Any(effect => effect.EscapeRequested);
        PassiveActivations = Array.AsReadOnly(
            Effects.SelectMany(effect => effect.PassiveActivations ?? []).ToArray());
        HostActionRequestIds = Array.AsReadOnly(
            Effects.SelectMany(effect => effect.HostActionRequestIds ?? []).ToArray());
        PressTurn = AggregatePressTurn(Effects);
    }

    public SkillExecutionStatus Status { get; }
    public IReadOnlyList<EffectExecutionResult> Effects { get; }
    public IReadOnlyList<SkillExecutionDiagnostic> Diagnostics { get; }
    public bool CostsCommitted { get; }
    public bool EscapeRequested { get; }
    public IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations { get; }
    public IReadOnlyList<ContentId> HostActionRequestIds { get; }
    public PressTurnResolution PressTurn { get; }

    public static SkillExecutionResult Rejected(IEnumerable<SkillExecutionDiagnostic> diagnostics) =>
        new(SkillExecutionStatus.Rejected, [], diagnostics);

    private static PressTurnResolution AggregatePressTurn(IReadOnlyList<EffectExecutionResult> effects)
    {
        EffectExecutionResult? interruption = effects.FirstOrDefault(effect =>
            effect.PressTurnOutcome is PressTurnOutcome.Repel or PressTurnOutcome.Absorb);
        if (interruption is not null)
        {
            return new PressTurnResolution(interruption.PressTurnOutcome, effects.Any(effect => effect.IsCritical), true);
        }

        PressTurnOutcome outcome = effects.Any(effect => effect.PressTurnOutcome == PressTurnOutcome.Null)
            ? PressTurnOutcome.Null
            : effects.Any(effect => effect.PressTurnOutcome == PressTurnOutcome.Miss)
                ? PressTurnOutcome.Miss
                : effects.Any(effect => effect.PressTurnOutcome == PressTurnOutcome.Weakness)
                    ? PressTurnOutcome.Weakness
                    : effects.Any(effect => effect.IsCritical)
                        ? PressTurnOutcome.Critical
                        : PressTurnOutcome.Normal;

        return new PressTurnResolution(outcome, effects.Any(effect => effect.IsCritical), false);
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
        IEnumerable<ContentId>? selectedTargetIds = null)
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
    public IReadOnlyList<ContentId> SelectedTargetIds { get; }

    public SkillExecutionRequest(
        SkillDefinition skill,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        EffectExecutionEnvironment environment,
        IEnumerable<ContentId>? selectedTargetIds = null)
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
        .Register(new OverrideAffinityEffectExecutor())
        .Register(new RemoveStatusEffectExecutor())
        .Register(new ReduceResourceEffectExecutor())
        .Register(new SetResourceEffectExecutor())
        .Register(new AnalyzeEffectExecutor())
        .Register(new EscapeEffectExecutor())
        .Register(new CustomEffectExecutor());
}
