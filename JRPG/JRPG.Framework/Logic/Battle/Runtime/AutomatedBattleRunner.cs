using System.Collections.ObjectModel;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Runtime;

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
    ElementalAffinityKnowledge Knowledge);

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
                request.ContextId,
                request.BattleKindId,
                request.MoonPhaseId,
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
            candidate.IsActive &&
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
                participant.State.IsActive &&
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
    SkillSelected,
    SkillPassed,
    EffectResolved,
    PassiveActivated,
    PressTurnChanged,
    ResourceChanged,
    ActorDefeated,
    BattleFaulted,
    BattleEnded
}

public sealed record BattleRuntimeEvent(
    int Sequence,
    BattleRuntimeEventKind Kind,
    string Message,
    RuntimeInstanceId? ActorId = null,
    RuntimeInstanceId? TargetId = null,
    ContentId? SkillId = null,
    decimal? Value = null);

public sealed record BattleActorFinalSnapshot
{
    internal BattleActorFinalSnapshot(BattleEncounterParticipantSnapshot participant)
    {
        InstanceId = participant.InstanceId;
        EntityId = participant.EntityId;
        TeamId = participant.TeamId;
        IsDefeated = participant.IsDefeated;
        Resources = new ReadOnlyDictionary<ContentId, decimal>(
            participant.State.Resources.ToDictionary(resource => resource.ResourceId, resource => resource.Current));
    }

    public RuntimeInstanceId InstanceId { get; }
    public ContentId EntityId { get; }
    public ContentId TeamId { get; }
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
        string? faultMessage = null)
    {
        Outcome = outcome;
        WinningTeamId = winningTeamId;
        FinalActors = Array.AsReadOnly(participants.Select(actor => new BattleActorFinalSnapshot(actor)).ToArray());
        Events = Array.AsReadOnly(events.ToArray());
        FaultMessage = faultMessage;
    }

    public AutomatedBattleOutcome Outcome { get; }
    public ContentId? WinningTeamId { get; }
    public IReadOnlyList<BattleActorFinalSnapshot> FinalActors { get; }
    public IReadOnlyList<BattleRuntimeEvent> Events { get; }
    public string? FaultMessage { get; }
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

    public AutomatedBattleRunner(
        ISkillExecutor executor,
        IBattleActionSelector selector,
        BattleExecutionServices services)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _services = services ?? throw new ArgumentNullException(nameof(services));
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
            new AutomatedBattleLifecyclePort(_services),
            new AutomatedBattleTurnHandler(_executor, _selector, _services, request.Participants),
            new LastTeamStandingCompletionPolicy(),
            () => new PressTurnEngine(),
            new BattlePhaseProgressPolicy(
                maximumCommands: 256,
                maximumConsecutiveFreeActions: 32));
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
            result.FaultMessage);
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
                BattleEncounterEventKind.CommandSelected => BattleRuntimeEventKind.SkillSelected,
                BattleEncounterEventKind.CommandPassed => BattleRuntimeEventKind.SkillPassed,
                BattleEncounterEventKind.EffectResolved => BattleRuntimeEventKind.EffectResolved,
                BattleEncounterEventKind.PassiveActivated => BattleRuntimeEventKind.PassiveActivated,
                BattleEncounterEventKind.TurnEconomyChanged
                    when battleEvent.TurnEconomyState is PressTurnEconomySnapshot =>
                    BattleRuntimeEventKind.PressTurnChanged,
                BattleEncounterEventKind.ResourceChanged => BattleRuntimeEventKind.ResourceChanged,
                BattleEncounterEventKind.ActorDefeated => BattleRuntimeEventKind.ActorDefeated,
                BattleEncounterEventKind.BattleFaulted => BattleRuntimeEventKind.BattleFaulted,
                BattleEncounterEventKind.BattleEnded => BattleRuntimeEventKind.BattleEnded,
                _ => null
            };
            if (kind is null)
            {
                continue;
            }

            mapped.Add(new BattleRuntimeEvent(
                mapped.Count + 1,
                kind.Value,
                battleEvent.Message,
                battleEvent.ActorId,
                battleEvent.TargetId,
                battleEvent.SourceId,
                battleEvent.Value));
        }

        return Array.AsReadOnly(mapped.ToArray());
    }

    private sealed class AutomatedBattleLifecyclePort : IBattleEncounterLifecyclePort
    {
        private static readonly ContentId BattleStart = ContentId.Parse("battle_start");
        private static readonly ContentId OwnerTurnEnd = ContentId.Parse("owner_turn_end");
        private readonly BattleExecutionServices _services;
        private readonly IBattleDurationLifecycleService _durationLifecycle =
            new BattleDurationLifecycleService();

        public AutomatedBattleLifecyclePort(BattleExecutionServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
            BattleEncounterLifecycleRequest request,
            CancellationToken cancellationToken = default) =>
            new(Dispatch(BattleStart, request.Participants, request.Encounter));

        public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default) =>
            new(new BattleTurnStartLifecycleResult(BattleTurnStartOutcome.CanAct, []));

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default) =>
            new(Dispatch(OwnerTurnEnd, [request.Actor], request.Encounter));

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
            BattleEncounterLifecycleRequest request,
            ContentId teamId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BattleStatusLifecycleResult result = _durationLifecycle.ProcessPhaseEnd(
                new BattlePhaseEndLifecycleRequest(
                    request.Participants.Select(participant => participant.State),
                    teamId));
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(MapDurationEvents(result.Events));
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
            BattleEncounterLifecycleRequest request,
            BattleEncounterOutcome outcome,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var events = new List<BattleStatusLifecycleEvent>();
            foreach (BattleEncounterParticipant participant in request.Participants)
            {
                events.AddRange(_durationLifecycle.Cleanup(new BattleStatusCleanupRequest(
                    participant.State,
                    BattleStatusCleanupScope.BattleEnd)).Events);
            }

            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(MapDurationEvents(events));
        }

        private IReadOnlyList<BattleEncounterEvent> Dispatch(
            ContentId eventId,
            IEnumerable<BattleEncounterParticipant> actors,
            BattleEncounterRequest request)
        {
            var events = new List<BattleEncounterEvent>();
            RuntimeActorState[] states = request.Participants.Select(participant => participant.State).ToArray();
            foreach (BattleEncounterParticipant actor in actors)
            {
                PassiveTriggerDispatchResult dispatch = _services.PassiveTriggers.Dispatch(
                    new PassiveTriggerDispatchRequest(
                        eventId,
                        actor.State,
                        states,
                        [actor.State],
                        request.ContextId,
                        request.BattleKindId,
                        request.MoonPhaseId),
                    _services);
                RecordPassives(events, actor.State.InstanceId, dispatch);
            }

            return Array.AsReadOnly(events.ToArray());
        }

        private static IReadOnlyList<BattleEncounterEvent> MapDurationEvents(
            IEnumerable<BattleStatusLifecycleEvent> events) =>
            Array.AsReadOnly(events.Select(statusEvent => new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.StatusChanged,
                $"Duration lifecycle: {statusEvent.Kind} {statusEvent.RelatedId}.",
                statusEvent.ActorId,
                SourceId: statusEvent.RelatedId,
                Value: statusEvent.Value)).ToArray());
    }

    private sealed class AutomatedBattleTurnHandler : IBattleEncounterTurnHandler
    {
        private readonly ISkillExecutor _executor;
        private readonly IBattleActionSelector _selector;
        private readonly IReadOnlyList<CatalogBattleActor> _actors;
        private readonly Dictionary<ContentId, ElementalAffinityKnowledge> _knowledge;

        public AutomatedBattleTurnHandler(
            ISkillExecutor executor,
            IBattleActionSelector selector,
            BattleExecutionServices services,
            IReadOnlyList<CatalogBattleActor> actors)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            ArgumentNullException.ThrowIfNull(services);
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
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
                return new ValueTask<BattleEncounterCommandResult>(
                    BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));
            }

            var selectionRequest = new BattleActionSelectionRequest(
                actor,
                _actors,
                request.Encounter.ContextId,
                request.Encounter.BattleKindId,
                request.Encounter.MoonPhaseId,
                _knowledge[actor.State.TeamId]);
            BattleActionSelection selection = _selector.Select(selectionRequest);
            if (selection.Status == BattleActionSelectionStatus.Pass || selection.Skill is null)
            {
                events.Add(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.CommandPassed,
                    $"{actor.State.InstanceId} passed.",
                    actor.State.InstanceId));
                return new ValueTask<BattleEncounterCommandResult>(
                    BattleEncounterCommandResult.Executed(ActionTurnConsumption.Pass, events));
            }

            events.Add(new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.CommandSelected,
                $"{actor.State.InstanceId} selected {selection.Skill.DisplayName}.",
                actor.State.InstanceId,
                selection.SelectedTargetIds.FirstOrDefault(),
                selection.Skill.Id));

            if (selection.Assessment is not SkillExecutionAssessment prepared)
            {
                const string fault = "The selected automated action has no prepared assessment.";
                events.Add(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.BattleFaulted,
                    fault,
                    actor.State.InstanceId,
                    SourceId: selection.Skill.Id));
                return new ValueTask<BattleEncounterCommandResult>(
                    BattleEncounterCommandResult.Faulted(fault, events));
            }

            SkillExecutionResult execution = _executor.Execute(
                prepared.Preparation.Request,
                prepared);
            if (execution.Status == SkillExecutionStatus.Rejected)
            {
                string fault = $"Selected skill '{selection.Skill.Id}' was rejected: " +
                               string.Join("; ", execution.Diagnostics.Select(diagnostic => diagnostic.Message));
                events.Add(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.BattleFaulted,
                    fault,
                    actor.State.InstanceId,
                    SourceId: selection.Skill.Id));
                return new ValueTask<BattleEncounterCommandResult>(
                    BattleEncounterCommandResult.Faulted(fault, events));
            }

            RecordExecution(events, actor, selection.Skill, execution, _actors, _knowledge[actor.State.TeamId]);
            return new ValueTask<BattleEncounterCommandResult>(
                BattleEncounterCommandResult.Executed(
                    ActionTurnConsumption.FromPressTurn(execution.PressTurn),
                    events));
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
        foreach (EffectExecutionResult effect in execution.Effects)
        {
            events.Add(new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.EffectResolved,
                $"Effect {effect.EffectIndex} resolved as {effect.Outcome} ({effect.PressTurnOutcome}).",
                actor.State.InstanceId,
                effect.TargetId,
                skill.Id,
                effect.Value));
            if (effect.Value is decimal value)
            {
                events.Add(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.ResourceChanged,
                    $"Resource changed by {value}.",
                    actor.State.InstanceId,
                    effect.TargetId,
                    skill.Id,
                    value));
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
                    $"Passive {passive.SkillId} resolved as {passive.Outcome}.",
                    passive.TargetId,
                    passive.TargetId,
                    passive.SkillId));
            }
        }
    }

    private static void RecordPassives(
        List<BattleEncounterEvent> events,
        RuntimeInstanceId actorId,
        PassiveTriggerDispatchResult dispatch)
    {
        foreach (PassiveTriggerExecutionResult activation in dispatch.Activations)
        {
            events.Add(new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.PassiveActivated,
                $"Passive {activation.SkillId} resolved as {activation.Outcome}.",
                actorId,
                activation.TargetId,
                activation.SkillId));
            foreach (EffectExecutionResult effect in activation.Effects)
            {
                events.Add(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.EffectResolved,
                    $"Passive effect {effect.EffectIndex} resolved as {effect.Outcome}.",
                    actorId,
                    effect.TargetId,
                    activation.SkillId,
                    effect.Value));
                if (effect.Value is decimal value)
                {
                    events.Add(new BattleEncounterEvent(
                        0,
                        BattleEncounterEventKind.ResourceChanged,
                        $"Resource changed by {value}.",
                        actorId,
                        effect.TargetId,
                        activation.SkillId,
                        value));
                }
            }
        }
    }
}
