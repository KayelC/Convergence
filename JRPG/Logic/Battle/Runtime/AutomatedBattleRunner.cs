using System.Collections.ObjectModel;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Execution;

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
        IEnumerable<ContentId>? selectedTargetIds = null,
        SkillExecutionAssessment? assessment = null)
    {
        Status = status;
        Skill = skill;
        SelectedTargetIds = Array.AsReadOnly(selectedTargetIds?.ToArray() ?? []);
        Assessment = assessment;
    }

    public BattleActionSelectionStatus Status { get; }
    public SkillDefinition? Skill { get; }
    public IReadOnlyList<ContentId> SelectedTargetIds { get; }
    public SkillExecutionAssessment? Assessment { get; }

    public static BattleActionSelection Pass() => new(BattleActionSelectionStatus.Pass);
}

public sealed record BattleActionSelectionRequest(
    CatalogBattleActor Actor,
    IReadOnlyList<CatalogBattleActor> Participants,
    ContentId ContextId,
    ContentId BattleKindId,
    ContentId MoonPhaseId,
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
        BattleActorState[] states = request.Participants.Select(participant => participant.State).ToArray();

        for (int loadoutIndex = 0; loadoutIndex < request.Actor.ActiveSkills.Count; loadoutIndex++)
        {
            SkillDefinition skill = request.Actor.ActiveSkills[loadoutIndex];
            if (skill.Availability is null || !skill.Availability.ContextIds.Contains(request.ContextId))
            {
                continue;
            }

            IReadOnlyList<ContentId> targetIds = SelectTargets(request.Actor.State, states, skill.Targeting);
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

            int score = Score(skill, assessment.TargetIds, request.Participants, request.Knowledge);
            if (score == int.MinValue)
            {
                continue;
            }

            var candidate = new Candidate(skill, targetIds, assessment, score, loadoutIndex);
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

    private static IReadOnlyList<ContentId> SelectTargets(
        BattleActorState actor,
        IReadOnlyList<BattleActorState> participants,
        TargetingDefinition? targeting)
    {
        if (targeting is null || targeting.Selection is TargetSelection.None or TargetSelection.All or TargetSelection.Random)
        {
            return [];
        }

        BattleActorState? target = participants.FirstOrDefault(candidate =>
            candidate.IsActive &&
            RelationMatches(actor, candidate, targeting.Relation) &&
            (targeting.AllowSelf || targeting.Relation == TargetRelation.Self || candidate.InstanceId != actor.InstanceId) &&
            LifeMatches(candidate, targeting.LifeState));
        return target is null ? [] : [target.InstanceId];
    }

    private static int Score(
        SkillDefinition skill,
        IReadOnlyList<ContentId> targetIds,
        IReadOnlyList<CatalogBattleActor> participants,
        ElementalAffinityKnowledge knowledge)
    {
        CatalogBattleActor? target = participants.FirstOrDefault(participant => targetIds.Contains(participant.State.InstanceId));
        if (target is null)
        {
            return 0;
        }

        int score = 0;
        foreach (DamageElement element in skill.Effects.OfType<DamageEffectDefinition>().Select(effect => effect.Element).Distinct())
        {
            if (!knowledge.TryGet(target.Entity.Id, element, out ElementalAffinity affinity))
            {
                continue;
            }

            if (affinity is ElementalAffinity.Null or ElementalAffinity.Repel or ElementalAffinity.Absorb)
            {
                return int.MinValue;
            }

            score += affinity switch
            {
                ElementalAffinity.Weak => 100,
                ElementalAffinity.Resist => -25,
                _ => 0
            };
        }

        return score;
    }

    private static bool RelationMatches(BattleActorState actor, BattleActorState candidate, TargetRelation relation) => relation switch
    {
        TargetRelation.Self => actor.InstanceId == candidate.InstanceId,
        TargetRelation.Ally => actor.TeamId == candidate.TeamId,
        TargetRelation.Enemy => actor.TeamId != candidate.TeamId,
        TargetRelation.Any => true,
        TargetRelation.None => false,
        _ => false
    };

    private static bool LifeMatches(BattleActorState actor, TargetLifeState lifeState) => lifeState switch
    {
        TargetLifeState.Alive => !actor.IsDefeated,
        TargetLifeState.Dead => actor.IsDefeated,
        TargetLifeState.Any => true,
        _ => false
    };

    private sealed record Candidate(
        SkillDefinition Skill,
        IReadOnlyList<ContentId> TargetIds,
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
    ContentId? ActorId = null,
    ContentId? TargetId = null,
    ContentId? SkillId = null,
    decimal? Value = null);

public sealed record BattleActorFinalSnapshot
{
    internal BattleActorFinalSnapshot(CatalogBattleActor actor)
    {
        InstanceId = actor.State.InstanceId;
        EntityId = actor.Entity.Id;
        TeamId = actor.State.TeamId;
        IsDefeated = actor.State.IsDefeated;
        Resources = new ReadOnlyDictionary<ContentId, decimal>(
            actor.State.Resources.ToDictionary(pair => pair.Key, pair => pair.Value.Current));
    }

    public ContentId InstanceId { get; }
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
        ContentId moonPhaseId,
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
    public ContentId MoonPhaseId { get; }
    public int RoundLimit { get; }
}

public sealed record AutomatedBattleResult
{
    internal AutomatedBattleResult(
        AutomatedBattleOutcome outcome,
        ContentId? winningTeamId,
        IEnumerable<CatalogBattleActor> participants,
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

        var events = new List<BattleRuntimeEvent>();
        int sequence = 0;
        void Add(BattleRuntimeEventKind kind, string message, ContentId? actor = null,
            ContentId? target = null, ContentId? skill = null, decimal? value = null) =>
            events.Add(new BattleRuntimeEvent(++sequence, kind, message, actor, target, skill, value));

        foreach (CatalogBattleActor actor in request.Participants)
        {
            actor.State.Passives.ResetBattleActivations();
            Add(BattleRuntimeEventKind.ActorCreated,
                $"Created {actor.Entity.DisplayName} as {actor.State.InstanceId} on {actor.State.TeamId}.",
                actor.State.InstanceId);
        }

        var knowledge = request.Participants.Select(actor => actor.State.TeamId).Distinct()
            .ToDictionary(team => team, _ => new ElementalAffinityKnowledge());
        Add(BattleRuntimeEventKind.BattleStarted, "Battle started.");
        DispatchEventToAll(ContentId.Parse("battle_start"), request, Add);

        ContentId[] teamOrder = request.Participants.Select(actor => actor.State.TeamId).Distinct().ToArray();
        for (int round = 1; round <= request.RoundLimit; round++)
        {
            Add(BattleRuntimeEventKind.RoundStarted, $"Round {round} started.");
            foreach (ContentId teamId in teamOrder)
            {
                CatalogBattleActor[] phaseActors = request.Participants
                    .Where(actor => actor.State.TeamId == teamId && actor.State.IsActive && !actor.State.IsDefeated)
                    .ToArray();
                if (phaseActors.Length == 0) continue;

                var pressTurn = new PressTurnEngine();
                pressTurn.StartPhase(phaseActors.Length);
                Add(BattleRuntimeEventKind.PhaseStarted,
                    $"Team {teamId} started a phase with {pressTurn.GetTotalIconCount()} icon(s).");
                int actorIndex = 0;
                while (pressTurn.HasTurnsRemaining())
                {
                    phaseActors = phaseActors.Where(actor => !actor.State.IsDefeated && actor.State.IsActive).ToArray();
                    if (phaseActors.Length == 0) break;
                    CatalogBattleActor actor = phaseActors[actorIndex++ % phaseActors.Length];
                    var selectionRequest = new BattleActionSelectionRequest(
                        actor,
                        request.Participants,
                        request.ContextId,
                        request.BattleKindId,
                        request.MoonPhaseId,
                        knowledge[teamId]);
                    BattleActionSelection selection = _selector.Select(selectionRequest);
                    if (selection.Status == BattleActionSelectionStatus.Pass || selection.Skill is null)
                    {
                        pressTurn.Pass();
                        Add(BattleRuntimeEventKind.SkillPassed, $"{actor.State.InstanceId} passed.", actor.State.InstanceId);
                        DispatchOwnerTurnEnd(actor, request, Add);
                        Add(BattleRuntimeEventKind.PressTurnChanged,
                            $"Press Turn: {pressTurn.FullIcons} full, {pressTurn.BlinkingIcons} blinking.");
                    }
                    else
                    {
                        Add(BattleRuntimeEventKind.SkillSelected,
                            $"{actor.State.InstanceId} selected {selection.Skill.DisplayName}.",
                            actor.State.InstanceId,
                            selection.SelectedTargetIds.FirstOrDefault(),
                            selection.Skill.Id);
                        var executionRequest = new SkillExecutionRequest(
                            selection.Skill,
                            actor.State,
                            request.Participants.Select(participant => participant.State),
                            request.ContextId,
                            request.BattleKindId,
                            request.MoonPhaseId,
                            selection.SelectedTargetIds);
                        SkillExecutionResult execution = _executor.Execute(executionRequest);
                        if (execution.Status == SkillExecutionStatus.Rejected)
                        {
                            string fault = $"Selected skill '{selection.Skill.Id}' was rejected: " +
                                           string.Join("; ", execution.Diagnostics.Select(diagnostic => diagnostic.Message));
                            Add(BattleRuntimeEventKind.BattleFaulted, fault, actor.State.InstanceId, skill: selection.Skill.Id);
                            return Finish(AutomatedBattleOutcome.Faulted, null, request, events, fault);
                        }

                        RecordExecution(actor, selection.Skill, execution, request, knowledge[teamId], Add);
                        pressTurn.ConsumeAction(execution.PressTurn);
                        Add(BattleRuntimeEventKind.PressTurnChanged,
                            $"Press Turn: {pressTurn.FullIcons} full, {pressTurn.BlinkingIcons} blinking.",
                            actor.State.InstanceId,
                            skill: selection.Skill.Id);
                        DispatchOwnerTurnEnd(actor, request, Add);
                    }

                    ContentId? winner = FindWinner(request.Participants);
                    if (winner is ContentId winningTeam)
                    {
                        Add(BattleRuntimeEventKind.BattleEnded, $"Team {winningTeam} won.");
                        return Finish(AutomatedBattleOutcome.Victory, winningTeam, request, events);
                    }
                }
            }
        }

        Add(BattleRuntimeEventKind.BattleEnded, $"Battle ended in a draw after {request.RoundLimit} round(s).");
        return Finish(AutomatedBattleOutcome.Draw, null, request, events);
    }

    private void DispatchEventToAll(
        ContentId eventId,
        AutomatedBattleRequest request,
        Action<BattleRuntimeEventKind, string, ContentId?, ContentId?, ContentId?, decimal?> add)
    {
        foreach (CatalogBattleActor actor in request.Participants)
        {
            PassiveTriggerDispatchResult dispatch = _services.PassiveTriggers.Dispatch(
                new PassiveTriggerDispatchRequest(
                    eventId,
                    actor.State,
                    request.Participants.Select(participant => participant.State),
                    [actor.State],
                    request.ContextId,
                    request.BattleKindId,
                    request.MoonPhaseId),
                _services);
            RecordPassives(actor, dispatch, add);
        }
    }

    private void DispatchOwnerTurnEnd(
        CatalogBattleActor actor,
        AutomatedBattleRequest request,
        Action<BattleRuntimeEventKind, string, ContentId?, ContentId?, ContentId?, decimal?> add)
    {
        PassiveTriggerDispatchResult dispatch = _services.PassiveTriggers.Dispatch(
            new PassiveTriggerDispatchRequest(
                ContentId.Parse("owner_turn_end"),
                actor.State,
                request.Participants.Select(participant => participant.State),
                [actor.State],
                request.ContextId,
                request.BattleKindId,
                request.MoonPhaseId),
            _services);
        RecordPassives(actor, dispatch, add);
    }

    private static void RecordExecution(
        CatalogBattleActor actor,
        SkillDefinition skill,
        SkillExecutionResult execution,
        AutomatedBattleRequest request,
        ElementalAffinityKnowledge knowledge,
        Action<BattleRuntimeEventKind, string, ContentId?, ContentId?, ContentId?, decimal?> add)
    {
        foreach (EffectExecutionResult effect in execution.Effects)
        {
            add(BattleRuntimeEventKind.EffectResolved,
                $"Effect {effect.EffectIndex} resolved as {effect.Outcome} ({effect.PressTurnOutcome}).",
                actor.State.InstanceId,
                effect.TargetId,
                skill.Id,
                effect.Value);
            if (effect.Value is decimal value)
            {
                add(BattleRuntimeEventKind.ResourceChanged,
                    $"Resource changed by {value}.", actor.State.InstanceId, effect.TargetId, skill.Id, value);
            }

            if (effect.TargetId is ContentId targetId && effect.ResolvedAffinity is ElementalAffinity affinity &&
                skill.Effects.ElementAtOrDefault(effect.EffectIndex) is DamageEffectDefinition damage)
            {
                CatalogBattleActor? target = request.Participants.FirstOrDefault(candidate => candidate.State.InstanceId == targetId);
                if (target is not null) knowledge.Learn(target.Entity.Id, damage.Element, affinity);
            }

            foreach (PassiveTriggerExecutionResult passive in effect.PassiveActivations ?? [])
            {
                add(BattleRuntimeEventKind.PassiveActivated,
                    $"Passive {passive.SkillId} resolved as {passive.Outcome}.",
                    passive.TargetId,
                    passive.TargetId,
                    passive.SkillId,
                    null);
            }
        }

        foreach (CatalogBattleActor target in request.Participants.Where(candidate => candidate.State.IsDefeated))
        {
            add(BattleRuntimeEventKind.ActorDefeated,
                $"{target.State.InstanceId} was defeated.", target.State.InstanceId, null, null, null);
        }
    }

    private static void RecordPassives(
        CatalogBattleActor actor,
        PassiveTriggerDispatchResult dispatch,
        Action<BattleRuntimeEventKind, string, ContentId?, ContentId?, ContentId?, decimal?> add)
    {
        foreach (PassiveTriggerExecutionResult activation in dispatch.Activations)
        {
            add(BattleRuntimeEventKind.PassiveActivated,
                $"Passive {activation.SkillId} resolved as {activation.Outcome}.",
                actor.State.InstanceId,
                activation.TargetId,
                activation.SkillId,
                null);
            foreach (EffectExecutionResult effect in activation.Effects)
            {
                add(BattleRuntimeEventKind.EffectResolved,
                    $"Passive effect {effect.EffectIndex} resolved as {effect.Outcome}.",
                    actor.State.InstanceId,
                    effect.TargetId,
                    activation.SkillId,
                    effect.Value);
                if (effect.Value is decimal value)
                {
                    add(BattleRuntimeEventKind.ResourceChanged,
                        $"Resource changed by {value}.", actor.State.InstanceId, effect.TargetId, activation.SkillId, value);
                }
            }
        }
    }

    private static ContentId? FindWinner(IReadOnlyList<CatalogBattleActor> participants)
    {
        ContentId[] livingTeams = participants
            .Where(actor => actor.State.IsActive && !actor.State.IsDefeated)
            .Select(actor => actor.State.TeamId)
            .Distinct()
            .ToArray();
        return livingTeams.Length == 1 ? livingTeams[0] : null;
    }

    private static AutomatedBattleResult Finish(
        AutomatedBattleOutcome outcome,
        ContentId? winningTeam,
        AutomatedBattleRequest request,
        IEnumerable<BattleRuntimeEvent> events,
        string? fault = null) =>
        new(outcome, winningTeam, request.Participants, events, fault);
}
