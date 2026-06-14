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

public sealed record EffectExecutionResult(
    int EffectIndex,
    ContentId? TargetId,
    EffectExecutionOutcome Outcome,
    PressTurnOutcome PressTurnOutcome = PressTurnOutcome.Normal,
    bool IsCritical = false,
    decimal? Value = null,
    ContentId? RelatedId = null,
    string? Detail = null,
    bool EscapeRequested = false,
    IReadOnlyList<PassiveTriggerExecutionResult>? PassiveActivations = null);

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
        PressTurn = AggregatePressTurn(Effects);
    }

    public SkillExecutionStatus Status { get; }
    public IReadOnlyList<EffectExecutionResult> Effects { get; }
    public IReadOnlyList<SkillExecutionDiagnostic> Diagnostics { get; }
    public bool CostsCommitted { get; }
    public bool EscapeRequested { get; }
    public IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations { get; }
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
        BattleActorState actor,
        IEnumerable<BattleActorState> participants,
        ContentId contextId,
        ContentId battleKindId,
        ContentId moonPhaseId,
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
    }

    public SkillDefinition Skill { get; }
    public BattleActorState Actor { get; }
    public IReadOnlyList<BattleActorState> Participants { get; }
    public ContentId ContextId { get; }
    public ContentId BattleKindId { get; }
    public ContentId MoonPhaseId { get; }
    public IReadOnlyList<ContentId> SelectedTargetIds { get; }
}

public sealed record ResolvedTargetSet
{
    internal ResolvedTargetSet(IEnumerable<BattleActorState> targets, bool isUntargeted = false)
    {
        Targets = Array.AsReadOnly(targets.ToArray());
        IsUntargeted = isUntargeted;
    }

    public IReadOnlyList<BattleActorState> Targets { get; }
    public bool IsUntargeted { get; }
}

public sealed record EffectExecutionContext(
    SkillExecutionRequest Request,
    BattleExecutionServices Services,
    int EffectIndex,
    EffectDefinition Effect,
    BattleActorState? Target,
    DamageElement? EffectElement = null)
{
    public BattleActorState Actor => Request.Actor;
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
