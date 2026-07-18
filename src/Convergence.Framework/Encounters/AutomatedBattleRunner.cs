using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Knowledge;
using Convergence.TurnEconomy;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Encounters;

public enum BattleActionSelectionStatus
{
    Selected,
    Pass
}

public sealed record BattleActionSelection
{
    public BattleActionSelection(
        BattleActionSelectionStatus status,
        SkillDefinition? skill = null,
        IEnumerable<RuntimeInstanceId>? selectedTargetIds = null,
        SkillExecutionAssessment? assessment = null)
    {
        Status = status;
        Skill = skill;
        SelectedTargetIds = Array.AsReadOnly(selectedTargetIds?.ToArray() ?? []);
        Assessment = assessment;
    }

    public BattleActionSelectionStatus Status { get; }
    public SkillDefinition? Skill { get; }
    public IReadOnlyList<RuntimeInstanceId> SelectedTargetIds { get; }
    public SkillExecutionAssessment? Assessment { get; }

    public static BattleActionSelection Pass() => new(BattleActionSelectionStatus.Pass);
}

public sealed record BattleActionSelectionRequest(
    CatalogBattleActor Actor,
    IReadOnlyList<CatalogBattleActor> Participants,
    ContentId ContextId,
    ContentId BattleKindId,
    ContentId? MoonPhaseId,
    ElementalAffinityKnowledge Knowledge)
{
    public BattleActionSelectionRequest(
        CatalogBattleActor actor,
        IReadOnlyList<CatalogBattleActor> participants,
        ContentId contextId,
        ContentId battleKindId,
        ContentId? moonPhaseId,
        ElementalAffinityKnowledge knowledge,
        IEnumerable<StatModifierLifecycleBoundary>? activeStatModifierBoundaries)
        : this(actor, participants, contextId, battleKindId, moonPhaseId, knowledge)
    {
        ActiveStatModifierBoundaries = new EffectExecutionEnvironment(
            contextId,
            battleKindId,
            moonPhaseId,
            activeStatModifierBoundaries).ActiveStatModifierBoundaries;
    }

    public IReadOnlyList<StatModifierLifecycleBoundary> ActiveStatModifierBoundaries { get; private init; } =
        Array.Empty<StatModifierLifecycleBoundary>();
}

public interface IBattleActionSelector
{
    BattleActionSelection Select(BattleActionSelectionRequest request);
}

public sealed class DeterministicBattleActionSelector : IBattleActionSelector
{
    private readonly ISkillExecutor _executor;

    public DeterministicBattleActionSelector(ISkillExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public BattleActionSelection Select(BattleActionSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Candidate? best = null;
        RuntimeActorState[] states = request.Participants.Select(participant => participant.State).ToArray();

        for (int loadoutIndex = 0; loadoutIndex < request.Actor.ActiveSkills.Count; loadoutIndex++)
        {
            SkillDefinition skill = request.Actor.ActiveSkills[loadoutIndex];
            if (skill.Availability is null || !skill.Availability.ContextIds.Contains(request.ContextId))
            {
                continue;
            }

            IReadOnlyList<RuntimeInstanceId> targetIds = SelectTargets(request.Actor.State, states, skill.Targeting);
            var executionRequest = new SkillExecutionRequest(
                skill,
                request.Actor.State,
                states,
                new EffectExecutionEnvironment(
                    request.ContextId,
                    request.BattleKindId,
                    request.MoonPhaseId,
                    request.ActiveStatModifierBoundaries),
                targetIds);
            SkillExecutionAssessment assessment = _executor.Assess(executionRequest);
            if (!assessment.CanExecute)
            {
                continue;
            }

            int score = Score(
                request.Actor.State,
                skill,
                assessment.TargetIds,
                request.Participants,
                request.Knowledge);
            if (score == int.MinValue)
            {
                continue;
            }

            var candidate = new Candidate(skill, assessment.TargetIds, assessment, score, loadoutIndex);
            if (best is null || candidate.Score > best.Score ||
                candidate.Score == best.Score && candidate.LoadoutIndex < best.LoadoutIndex)
            {
                best = candidate;
            }
        }

        return best is null
            ? BattleActionSelection.Pass()
            : new BattleActionSelection(
                BattleActionSelectionStatus.Selected,
                best.Skill,
                best.TargetIds,
                best.Assessment);
    }

    private static IReadOnlyList<RuntimeInstanceId> SelectTargets(
        RuntimeActorState actor,
        IReadOnlyList<RuntimeActorState> participants,
        TargetingDefinition? targeting)
    {
        if (targeting is null || targeting.Selection is TargetSelection.None or TargetSelection.All or TargetSelection.Random)
        {
            return [];
        }

        RuntimeActorState? target = participants.FirstOrDefault(candidate =>
            candidate.IsDeployed &&
            RelationMatches(actor, candidate, targeting.Relation) &&
            (targeting.AllowSelf || targeting.Relation == TargetRelation.Self || candidate.InstanceId != actor.InstanceId) &&
            LifeMatches(candidate, targeting.LifeState));
        return target is null ? [] : [target.InstanceId];
    }

    private static int Score(
        RuntimeActorState actor,
        SkillDefinition skill,
        IReadOnlyList<RuntimeInstanceId> targetIds,
        IReadOnlyList<CatalogBattleActor> participants,
        ElementalAffinityKnowledge knowledge)
    {
        CatalogBattleActor[] targets;
        if (skill.Targeting?.Selection == TargetSelection.Random)
        {
            TargetingDefinition targeting = skill.Targeting;
            targets = participants.Where(participant =>
                participant.State.IsDeployed &&
                RelationMatches(actor, participant.State, targeting.Relation) &&
                (targeting.AllowSelf || targeting.Relation == TargetRelation.Self ||
                    participant.State.InstanceId != actor.InstanceId) &&
                LifeMatches(participant.State, targeting.LifeState)).ToArray();
        }
        else
        {
            HashSet<RuntimeInstanceId> selectedTargetIds = targetIds.ToHashSet();
            targets = participants
                .Where(participant => selectedTargetIds.Contains(participant.State.InstanceId))
                .ToArray();
        }
        if (targets.Length == 0)
        {
            return 0;
        }

        int score = 0;
        DamageElement[] elements = skill.Effects
            .OfType<DamageEffectDefinition>()
            .Select(effect => effect.Element)
            .Distinct()
            .ToArray();
        foreach (CatalogBattleActor target in targets)
        {
            foreach (DamageElement element in elements)
            {
                if (!knowledge.TryGet(target.Entity.Id, element, out ElementalAffinity affinity))
                {
                    continue;
                }

                if (affinity is ElementalAffinity.Null or ElementalAffinity.Repel or ElementalAffinity.Absorb)
                {
                    return int.MinValue;
                }

                score = affinity switch
                {
                    ElementalAffinity.Weak => (int)Math.Min(int.MaxValue, (long)score + 100L),
                    ElementalAffinity.Resist => (int)Math.Max((long)int.MinValue + 1L, (long)score - 25L),
                    _ => score
                };
            }
        }

        return score;
    }

    private static bool RelationMatches(RuntimeActorState actor, RuntimeActorState candidate, TargetRelation relation) => relation switch
    {
        TargetRelation.Self => actor.InstanceId == candidate.InstanceId,
        TargetRelation.Ally => actor.TeamId == candidate.TeamId,
        TargetRelation.Enemy => actor.TeamId != candidate.TeamId,
        TargetRelation.Any => true,
        TargetRelation.None => false,
        _ => false
    };

    private static bool LifeMatches(RuntimeActorState actor, TargetLifeState lifeState) => lifeState switch
    {
        TargetLifeState.Alive => !actor.IsDefeated,
        TargetLifeState.Dead => actor.IsDefeated,
        TargetLifeState.Any => true,
        _ => false
    };

    private sealed record Candidate(
        SkillDefinition Skill,
        IReadOnlyList<RuntimeInstanceId> TargetIds,
        SkillExecutionAssessment Assessment,
        int Score,
        int LoadoutIndex);
}

public enum AutomatedBattleOutcome
{
    Victory,
    Draw,
    Faulted
}

public enum BattleRuntimeEventKind
{
    ActorCreated,
    BattleStarted,
    RoundStarted,
    PhaseStarted,
    TurnRestricted,
    SkillSelected,
    SkillPassed,
    EffectResolved,
    PassiveActivated,
    StatusChanged,
    TurnEconomyChanged,
    ResourceChanged,
    ActorDefeated,
    BattleFaulted,
    BattleEnded,
    EncounterPresenceChanged,
    HostActionRequested
}

public sealed record BattleRuntimeEvent(
    int Sequence,
    BattleRuntimeEventKind Kind,
    string Message,
    RuntimeInstanceId? ActorId = null,
    RuntimeInstanceId? TargetId = null,
    ContentId? SkillId = null,
    decimal? Value = null,
    BattleTurnEconomySnapshot? TurnEconomyState = null,
    bool? IsDeployed = null)
{
    public BattleEncounterFaultCode? FaultCode { get; internal init; }
}

public sealed record BattleActorFinalSnapshot
{
    internal BattleActorFinalSnapshot(BattleEncounterParticipantSnapshot participant)
    {
        InstanceId = participant.InstanceId;
        EntityId = participant.EntityId;
        TeamId = participant.TeamId;
        IsDeployed = participant.IsDeployed;
        IsDefeated = participant.IsDefeated;
        Resources = new ReadOnlyDictionary<ContentId, decimal>(
            participant.State.Resources.ToDictionary(resource => resource.ResourceId, resource => resource.Current));
    }

    public RuntimeInstanceId InstanceId { get; }
    public ContentId EntityId { get; }
    public ContentId TeamId { get; }
    public bool IsDeployed { get; }
    public bool IsDefeated { get; }
    public IReadOnlyDictionary<ContentId, decimal> Resources { get; }
}

public sealed record AutomatedBattleRequest
{
    public AutomatedBattleRequest(
        IEnumerable<CatalogBattleActor> participants,
        ContentId contextId,
        ContentId battleKindId,
        ContentId? moonPhaseId,
        int roundLimit)
    {
        Participants = Array.AsReadOnly(participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        ContextId = contextId;
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        RoundLimit = roundLimit;
    }

    public IReadOnlyList<CatalogBattleActor> Participants { get; }
    public ContentId ContextId { get; }
    public ContentId BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public int RoundLimit { get; }
}

public sealed record AutomatedBattleResult
{
    internal AutomatedBattleResult(
        AutomatedBattleOutcome outcome,
        ContentId? winningTeamId,
        IEnumerable<BattleEncounterParticipantSnapshot> participants,
        IEnumerable<BattleRuntimeEvent> events,
        string? faultMessage = null,
        BattleEncounterFaultCode? faultCode = null)
    {
        Outcome = outcome;
        WinningTeamId = winningTeamId;
        FinalActors = Array.AsReadOnly(participants.Select(actor => new BattleActorFinalSnapshot(actor)).ToArray());
        Events = Array.AsReadOnly(events.ToArray());
        FaultMessage = faultMessage;
        FaultCode = faultCode;
    }

    public AutomatedBattleOutcome Outcome { get; }
    public ContentId? WinningTeamId { get; }
    public IReadOnlyList<BattleActorFinalSnapshot> FinalActors { get; }
    public IReadOnlyList<BattleRuntimeEvent> Events { get; }
    public string? FaultMessage { get; }
    public BattleEncounterFaultCode? FaultCode { get; }
}

public interface IAutomatedBattleRunner
{
    AutomatedBattleResult Run(AutomatedBattleRequest request);
}

public sealed class AutomatedBattleRunner : IAutomatedBattleRunner
{
    private readonly ISkillExecutor _executor;
    private readonly IBattleActionSelector _selector;
    private readonly BattleExecutionServices _services;
    private readonly IBattleEncounterLifecyclePort _lifecycle;
    private readonly BattleTurnEconomyRuleset _turnEconomy;
    private readonly IAutomatedBattleTurnRestrictionResolver _restrictionResolver;

    public AutomatedBattleRunner(
        ISkillExecutor executor,
        IBattleActionSelector selector,
        BattleExecutionServices services,
        IBattleEncounterLifecyclePort lifecycle,
        BattleTurnEconomyRuleset turnEconomy,
        IAutomatedBattleTurnRestrictionResolver restrictionResolver)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _turnEconomy = turnEconomy ?? throw new ArgumentNullException(nameof(turnEconomy));
        _restrictionResolver = restrictionResolver ?? throw new ArgumentNullException(nameof(restrictionResolver));
    }

    public AutomatedBattleResult Run(AutomatedBattleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RoundLimit <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Round limit must be positive.");
        if (request.Participants.Count == 0) throw new ArgumentException("A battle requires participants.", nameof(request));

        BattleEncounterParticipant[] participants = request.Participants
            .Select(actor => new BattleEncounterParticipant(actor.State, actor.Entity.DisplayName))
            .ToArray();
        var services = new BattleEncounterServices(
            new ParticipantOrderInitiativePolicy(),
            _lifecycle,
            new AutomatedBattleTurnHandler(
                _executor,
                _selector,
                _services,
                request.Participants,
                _restrictionResolver),
            new LastTeamStandingCompletionPolicy(),
            _turnEconomy.CreateEconomy,
            _turnEconomy.PhaseProgress);
        BattleEncounterResult result = new BattleEncounterRunner().Run(
            new BattleEncounterRequest(
                participants,
                request.ContextId,
                request.BattleKindId,
                request.MoonPhaseId,
                request.RoundLimit),
            services);

        return new AutomatedBattleResult(
            result.Outcome switch
            {
                BattleEncounterOutcome.Victory => AutomatedBattleOutcome.Victory,
                BattleEncounterOutcome.Faulted => AutomatedBattleOutcome.Faulted,
                _ => AutomatedBattleOutcome.Draw
            },
            result.WinningTeamId,
            result.Participants,
            ToRuntimeEvents(result.Events),
            result.FaultMessage,
            result.FaultCode);
    }

    private static IReadOnlyList<BattleRuntimeEvent> ToRuntimeEvents(IEnumerable<BattleEncounterEvent> events)
    {
        var mapped = new List<BattleRuntimeEvent>();
        foreach (BattleEncounterEvent battleEvent in events)
        {
            BattleRuntimeEventKind? kind = battleEvent.Kind switch
            {
                BattleEncounterEventKind.ActorCreated => BattleRuntimeEventKind.ActorCreated,
                BattleEncounterEventKind.BattleStarted => BattleRuntimeEventKind.BattleStarted,
                BattleEncounterEventKind.RoundStarted => BattleRuntimeEventKind.RoundStarted,
                BattleEncounterEventKind.PhaseStarted => BattleRuntimeEventKind.PhaseStarted,
                BattleEncounterEventKind.TurnRestricted => BattleRuntimeEventKind.TurnRestricted,
                BattleEncounterEventKind.CommandSelected => BattleRuntimeEventKind.SkillSelected,
                BattleEncounterEventKind.CommandPassed => BattleRuntimeEventKind.SkillPassed,
                BattleEncounterEventKind.EffectResolved => BattleRuntimeEventKind.EffectResolved,
                BattleEncounterEventKind.PassiveActivated => BattleRuntimeEventKind.PassiveActivated,
                BattleEncounterEventKind.StatusChanged => BattleRuntimeEventKind.StatusChanged,
                BattleEncounterEventKind.TurnEconomyChanged => BattleRuntimeEventKind.TurnEconomyChanged,
                BattleEncounterEventKind.ResourceChanged => BattleRuntimeEventKind.ResourceChanged,
                BattleEncounterEventKind.EncounterPresenceChanged => BattleRuntimeEventKind.EncounterPresenceChanged,
                BattleEncounterEventKind.HostActionRequested => BattleRuntimeEventKind.HostActionRequested,
                BattleEncounterEventKind.ActorDefeated => BattleRuntimeEventKind.ActorDefeated,
                BattleEncounterEventKind.BattleFaulted => BattleRuntimeEventKind.BattleFaulted,
                BattleEncounterEventKind.BattleEnded => BattleRuntimeEventKind.BattleEnded,
                _ => null
            };
            if (kind is null)
            {
                continue;
            }

            var runtimeEvent = new BattleRuntimeEvent(
                mapped.Count + 1,
                kind.Value,
                battleEvent.DebugText ?? battleEvent.Kind.ToString(),
                battleEvent.ActorId,
                battleEvent.TargetId,
                battleEvent.SourceId,
                battleEvent.Value,
                battleEvent.TurnEconomyState,
                battleEvent.Payload is BattleEncounterPresenceChangedEventPayload presence
                    ? presence.IsDeployed
                    : null)
            {
                FaultCode = battleEvent.FaultCode
            };
            mapped.Add(runtimeEvent);
        }

        return Array.AsReadOnly(mapped.ToArray());
    }

    private sealed class AutomatedBattleTurnHandler : IBattleEncounterTurnHandler
    {
        private readonly ISkillExecutor _executor;
        private readonly IBattleActionSelector _selector;
        private readonly IReadOnlyList<CatalogBattleActor> _actors;
        private readonly Dictionary<ContentId, ElementalAffinityKnowledge> _knowledge;
        private readonly IAutomatedBattleTurnRestrictionResolver _restrictionResolver;

        public AutomatedBattleTurnHandler(
            ISkillExecutor executor,
            IBattleActionSelector selector,
            BattleExecutionServices services,
            IReadOnlyList<CatalogBattleActor> actors,
            IAutomatedBattleTurnRestrictionResolver restrictionResolver)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            ArgumentNullException.ThrowIfNull(services);
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _restrictionResolver = restrictionResolver ?? throw new ArgumentNullException(nameof(restrictionResolver));
            _knowledge = _actors.Select(actor => actor.State.TeamId).Distinct()
                .ToDictionary(team => team, _ => new ElementalAffinityKnowledge());
        }

        public ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CatalogBattleActor actor = _actors.Single(actor => actor.State.InstanceId == request.Actor.InstanceId);
            var events = new List<BattleEncounterEvent>();

            if (request.TurnStartOutcome != BattleTurnStartOutcome.CanAct)
            {
                return _restrictionResolver.ResolveAsync(
                    new AutomatedBattleTurnRestrictionRequest(
                        request,
                        actor,
                        _actors,
                        _knowledge[actor.State.TeamId]),
                    cancellationToken);
            }

            var selectionRequest = new BattleActionSelectionRequest(
                actor,
                _actors,
                request.Encounter.ContextId,
                request.Encounter.BattleKindId,
                request.Encounter.MoonPhaseId,
                _knowledge[actor.State.TeamId],
                request.ActiveStatModifierBoundaries);
            BattleActionSelection selection = _selector.Select(selectionRequest);
            if (selection.Status == BattleActionSelectionStatus.Pass)
            {
                events.Add(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.CommandPassed,
                    new BattleCommandPassedEventPayload(actor.State.InstanceId),
                    $"{actor.State.InstanceId} passed."));
                return new ValueTask<BattleEncounterCommandResult>(
                    BattleEncounterCommandResult.Executed(ActionTurnConsumption.Pass, events));
            }

            if (selection.Status != BattleActionSelectionStatus.Selected || selection.Skill is null)
            {
                return FaultedAutomatedAction(
                    actor,
                    "The selected automated action does not identify a skill.",
                    events);
            }

            if (selection.Assessment is not SkillExecutionAssessment prepared)
            {
                return FaultedAutomatedAction(
                    actor,
                    "The selected automated action has no prepared assessment.",
                    events);
            }

            if (!TryValidatePreparedSelection(request, actor, selection, prepared, out string? validationDiagnostic))
            {
                return FaultedAutomatedAction(actor, validationDiagnostic!, events);
            }

            BattleActionAuthorizationResult authorization = actor.AuthorizeSkill(selection.Skill);
            if (!authorization.IsAuthorized)
            {
                string diagnostic = string.Join(
                    "; ",
                    authorization.Diagnostics.Select(item => item.Message));
                return FaultedAutomatedAction(
                    actor,
                    $"Selected skill '{selection.Skill.Id}' is not authorized: {diagnostic}",
                    events);
            }

            if (!prepared.Preparation.Request.Environment.ActiveStatModifierBoundaries.SequenceEqual(
                    request.ActiveStatModifierBoundaries))
            {
                return FaultedAutomatedAction(
                    actor,
                    "The selected automated action was prepared for another stat-modifier lifecycle boundary.",
                    events);
            }

            events.Add(new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.CommandSelected,
                new BattleCommandSelectedEventPayload(
                    actor.State.InstanceId,
                    selection.Skill.Id,
                    selection.SelectedTargetIds.FirstOrDefault()),
                $"{actor.State.InstanceId} selected {selection.Skill.DisplayName}."));

            SkillExecutionResult execution = _executor.Execute(
                prepared.Preparation.Request,
                prepared);
            if (execution.Status == SkillExecutionStatus.Rejected)
            {
                string fault = $"Selected skill '{selection.Skill.Id}' was rejected: " +
                               string.Join("; ", execution.Diagnostics.Select(diagnostic => diagnostic.Message));
                return FaultedAutomatedAction(actor, fault, events);
            }

            RecordExecution(events, actor, selection.Skill, execution, _actors, _knowledge[actor.State.TeamId]);
            return new ValueTask<BattleEncounterCommandResult>(
                BattleEncounterCommandResult.Executed(
                    ActionTurnConsumption.FromTurnEconomy(execution.TurnEconomy),
                    events));
        }

        private bool TryValidatePreparedSelection(
            BattleEncounterTurnRequest turn,
            CatalogBattleActor actor,
            BattleActionSelection selection,
            SkillExecutionAssessment prepared,
            out string? diagnostic)
        {
            SkillExecutionRequest preparedRequest = prepared.Preparation.Request;
            if (!prepared.CanExecute)
            {
                diagnostic = "The selected automated action assessment is not executable.";
                return false;
            }

            if (!ReferenceEquals(preparedRequest.Skill, selection.Skill))
            {
                diagnostic = "The selected automated skill does not match its prepared assessment.";
                return false;
            }

            if (!ReferenceEquals(preparedRequest.Actor, actor.State))
            {
                diagnostic = "The selected automated action was prepared for another actor.";
                return false;
            }

            RuntimeActorState[] currentParticipants = _actors
                .Select(participant => participant.State)
                .ToArray();
            if (!preparedRequest.Participants.SequenceEqual(currentParticipants))
            {
                diagnostic = "The selected automated action was prepared for another participant set.";
                return false;
            }

            if (preparedRequest.ContextId != turn.Encounter.ContextId ||
                preparedRequest.BattleKindId != turn.Encounter.BattleKindId ||
                preparedRequest.MoonPhaseId != turn.Encounter.MoonPhaseId)
            {
                diagnostic = "The selected automated action was prepared for another encounter environment.";
                return false;
            }

            if (!prepared.TargetIds.SequenceEqual(selection.SelectedTargetIds))
            {
                diagnostic = "The selected automated targets do not match the prepared assessment.";
                return false;
            }

            diagnostic = null;
            return true;
        }

        private static ValueTask<BattleEncounterCommandResult> FaultedAutomatedAction(
            CatalogBattleActor actor,
            string fault,
            ICollection<BattleEncounterEvent> events)
        {
            events.Add(new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.BattleFaulted,
                new BattleFaultedEventPayload(
                    BattleEncounterFaultCode.CommandExecutionFaulted,
                    actor.State.InstanceId,
                    actor.State.TeamId,
                    "automated-action"),
                fault));
            return new ValueTask<BattleEncounterCommandResult>(
                BattleEncounterCommandResult.Faulted(fault, events));
        }
    }

    private static void RecordExecution(
        List<BattleEncounterEvent> events,
        CatalogBattleActor actor,
        SkillDefinition skill,
        SkillExecutionResult execution,
        IReadOnlyList<CatalogBattleActor> participants,
        ElementalAffinityKnowledge knowledge)
    {
        foreach (ExecutionResourceChange change in execution.CommittedCostChanges)
        {
            RecordResourceChange(events, actor.State.InstanceId, skill.Id, change);
        }

        foreach (EffectExecutionResult effect in execution.Effects)
        {
            events.Add(new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.EffectResolved,
                new BattleEffectResolvedEventPayload(actor.State.InstanceId, skill.Id, effect),
                $"Effect {effect.EffectIndex} resolved as {effect.Outcome} ({effect.TurnEconomyOutcome})."));
            foreach (ExecutionResourceChange change in effect.ResourceChanges)
            {
                RecordResourceChange(events, actor.State.InstanceId, skill.Id, change);
            }

            if (effect.TargetId is RuntimeInstanceId targetId &&
                effect.ResolvedAffinity is ElementalAffinity affinity &&
                skill.Effects.ElementAtOrDefault(effect.EffectIndex) is DamageEffectDefinition damage)
            {
                CatalogBattleActor? target = participants.FirstOrDefault(candidate => candidate.State.InstanceId == targetId);
                if (target is not null) knowledge.Learn(target.Entity.Id, damage.Element, affinity);
            }

            foreach (PassiveTriggerExecutionResult passive in effect.PassiveActivations ?? [])
            {
                events.Add(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.PassiveActivated,
                    new BattlePassiveActivatedEventPayload(
                        passive.TargetId,
                        passive.SkillId,
                        passive.Outcome,
                        passive.TriggerIndex,
                        passive.EventId),
                    $"Passive {passive.SkillId} resolved as {passive.Outcome}."));
            }
        }
    }

    private static void RecordResourceChange(
        ICollection<BattleEncounterEvent> events,
        RuntimeInstanceId sourceActorId,
        ContentId sourceId,
        ExecutionResourceChange change) =>
        events.Add(new BattleEncounterEvent(
            0,
            BattleEncounterEventKind.ResourceChanged,
            new BattleResourceChangedEventPayload(
                sourceActorId,
                change.ActorId,
                change.Delta,
                change.ResourceId,
                sourceId),
            $"Resource {change.ResourceId} changed by {change.Delta}."));

}
