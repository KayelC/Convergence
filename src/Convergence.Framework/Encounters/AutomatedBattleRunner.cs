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
    IBattleKnowledgeView Knowledge)
{
    public BattleActionSelectionRequest(
        CatalogBattleActor actor,
        IReadOnlyList<CatalogBattleActor> participants,
        ContentId contextId,
        ContentId battleKindId,
        ContentId? moonPhaseId,
        IBattleKnowledgeView knowledge,
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
        IBattleKnowledgeView knowledge)
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
                if (!knowledge.TryGetElementalAffinity(
                        target.State.InstanceId,
                        target.State.CombatProfileIdentity,
                        element,
                        out ElementalAffinity affinity,
                        out _,
                        out BattleDefenseInfluence temporaryInfluences))
                {
                    continue;
                }
                if (temporaryInfluences != BattleDefenseInfluence.None)
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
    Victory = 0,
    Draw = 1,
    Faulted = 2,
    Defeat = 3,
    Escape = 4,
    Cancelled = 5
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
        : this(
            participants,
            contextId,
            battleKindId,
            moonPhaseId,
            roundLimit,
            teamKnowledgeSeeds: null)
    {
    }

    public AutomatedBattleRequest(
        IEnumerable<CatalogBattleActor> participants,
        ContentId contextId,
        ContentId battleKindId,
        ContentId? moonPhaseId,
        int roundLimit,
        IEnumerable<KeyValuePair<ContentId, RuntimeEncounterKnowledgeSnapshot>>? teamKnowledgeSeeds)
    {
        CatalogBattleActor[] participantSnapshot =
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants));
        if (participantSnapshot.Length == 0)
        {
            throw new ArgumentException(
                "An automated battle requires at least one participant.",
                nameof(participants));
        }

        if (participantSnapshot.Any(participant => participant is null))
        {
            throw new ArgumentException(
                "Automated battle participants cannot contain null entries.",
                nameof(participants));
        }

        if (!contextId.IsValid)
        {
            throw new ArgumentException(
                "Automated battle context ID must be valid.",
                nameof(contextId));
        }

        if (!battleKindId.IsValid)
        {
            throw new ArgumentException(
                "Automated battle kind ID must be valid.",
                nameof(battleKindId));
        }

        if (moonPhaseId is ContentId moonPhase && !moonPhase.IsValid)
        {
            throw new ArgumentException(
                "Automated battle moon-phase ID must be valid when supplied.",
                nameof(moonPhaseId));
        }

        if (roundLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(roundLimit),
                "Automated battle round limit must be positive.");
        }

        Participants = Array.AsReadOnly(participantSnapshot);
        ContextId = contextId;
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        RoundLimit = roundLimit;
        TeamKnowledgeSeeds = ValidateKnowledgeSeeds(Participants, teamKnowledgeSeeds);
    }

    public IReadOnlyList<CatalogBattleActor> Participants { get; }
    public ContentId ContextId { get; }
    public ContentId BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public int RoundLimit { get; }
    public IReadOnlyDictionary<ContentId, RuntimeEncounterKnowledgeSnapshot> TeamKnowledgeSeeds { get; }

    private static IReadOnlyDictionary<ContentId, RuntimeEncounterKnowledgeSnapshot> ValidateKnowledgeSeeds(
        IReadOnlyList<CatalogBattleActor> participants,
        IEnumerable<KeyValuePair<ContentId, RuntimeEncounterKnowledgeSnapshot>>? seeds)
    {
        var result = new Dictionary<ContentId, RuntimeEncounterKnowledgeSnapshot>();
        IReadOnlyDictionary<RuntimeInstanceId, CatalogBattleActor> actors = participants
            .GroupBy(participant => participant.State.InstanceId)
            .ToDictionary(group => group.Key, group => group.First());
        HashSet<ContentId> teams = participants.Select(participant => participant.State.TeamId).ToHashSet();
        foreach ((ContentId teamId, RuntimeEncounterKnowledgeSnapshot snapshot) in seeds ?? [])
        {
            if (!teamId.IsValid || !teams.Contains(teamId) || snapshot is null || !result.TryAdd(teamId, snapshot))
            {
                throw new ArgumentException(
                    "Automated battle knowledge seeds require unique participating team IDs and non-null snapshots.",
                    nameof(seeds));
            }

            RuntimeEncounterKnowledgeSnapshot.RequireNoStoredIntrinsicElement(snapshot, nameof(seeds));

            foreach ((RuntimeInstanceId instanceId, RuntimeCombatProfileIdentitySnapshot profile) in
                     TargetProfiles(snapshot))
            {
                if (!actors.TryGetValue(instanceId, out CatalogBattleActor? actor) ||
                    actor.State.CombatProfileIdentity != profile)
                {
                    throw new ArgumentException(
                        $"Knowledge seed target '{instanceId}' does not match the participant's current combat profile.",
                        nameof(seeds));
                }
            }
        }

        return new ReadOnlyDictionary<ContentId, RuntimeEncounterKnowledgeSnapshot>(result);
    }

    private static IEnumerable<(
        RuntimeInstanceId InstanceId,
        RuntimeCombatProfileIdentitySnapshot Profile)> TargetProfiles(
        RuntimeEncounterKnowledgeSnapshot snapshot) =>
        snapshot.Elemental.Select(entry => (entry.TargetInstanceId, entry.TargetProfileIdentity))
            .Concat(snapshot.Ailments.Select(entry => (entry.TargetInstanceId, entry.TargetProfileIdentity)))
            .Concat(snapshot.InstantDeath.Select(entry => (entry.TargetInstanceId, entry.TargetProfileIdentity)))
            .Concat(snapshot.Analysis.Select(entry => (entry.TargetInstanceId, entry.TargetProfileIdentity)))
            .Distinct();
}

public sealed record AutomatedBattleResult
{
    internal AutomatedBattleResult(
        AutomatedBattleOutcome outcome,
        ContentId? winningTeamId,
        IEnumerable<BattleEncounterParticipantSnapshot> participants,
        IEnumerable<BattleEncounterEvent> events,
        IEnumerable<KeyValuePair<ContentId, RuntimeEncounterKnowledgeSnapshot>> teamKnowledge,
        string? faultMessage = null,
        BattleEncounterFaultCode? faultCode = null)
    {
        Outcome = outcome;
        WinningTeamId = winningTeamId;
        FinalActors = Array.AsReadOnly(participants.Select(actor => new BattleActorFinalSnapshot(actor)).ToArray());
        Events = Array.AsReadOnly(events.ToArray());
        TeamKnowledge = new ReadOnlyDictionary<ContentId, RuntimeEncounterKnowledgeSnapshot>(
            teamKnowledge.ToDictionary(pair => pair.Key, pair => pair.Value));
        FaultMessage = faultMessage;
        FaultCode = faultCode;
    }

    public AutomatedBattleOutcome Outcome { get; }
    public ContentId? WinningTeamId { get; }
    public IReadOnlyList<BattleActorFinalSnapshot> FinalActors { get; }
    public IReadOnlyList<BattleEncounterEvent> Events { get; }
    public IReadOnlyDictionary<ContentId, RuntimeEncounterKnowledgeSnapshot> TeamKnowledge { get; }
    public string? FaultMessage { get; }
    public BattleEncounterFaultCode? FaultCode { get; }
}

public interface IAutomatedBattleRunner
{
    AutomatedBattleResult Run(AutomatedBattleRequest request);

    ValueTask<AutomatedBattleResult> RunAsync(
        AutomatedBattleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AutomatedBattleRunner : IAutomatedBattleRunner
{
    private readonly ISkillExecutor _executor;
    private readonly IBattleActionSelector _selector;
    private readonly BattleExecutionServices _services;
    private readonly IBattleEncounterLifecyclePort _lifecycle;
    private readonly BattleTurnEconomyRuleset _turnEconomy;
    private readonly IAutomatedBattleTurnRestrictionResolver _restrictionResolver;
    private readonly BattleEncounterProgressPolicy _encounterProgress;

    public AutomatedBattleRunner(
        ISkillExecutor executor,
        IBattleActionSelector selector,
        BattleExecutionServices services,
        IBattleEncounterLifecyclePort lifecycle,
        BattleTurnEconomyRuleset turnEconomy,
        IAutomatedBattleTurnRestrictionResolver restrictionResolver,
        BattleEncounterProgressPolicy encounterProgress)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _turnEconomy = turnEconomy ?? throw new ArgumentNullException(nameof(turnEconomy));
        _restrictionResolver = restrictionResolver ?? throw new ArgumentNullException(nameof(restrictionResolver));
        _encounterProgress = encounterProgress ?? throw new ArgumentNullException(nameof(encounterProgress));
    }

    /// <summary>
    /// Compatibility-only synchronous entry point for non-UI callers that do not require
    /// synchronization-context affinity. Engine and UI hosts must await <see cref="RunAsync"/>.
    /// </summary>
    public AutomatedBattleResult Run(AutomatedBattleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        SynchronizationContext? callerContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            return RunAsync(request).AsTask().GetAwaiter().GetResult();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(callerContext);
        }
    }

    public async ValueTask<AutomatedBattleResult> RunAsync(
        AutomatedBattleRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (request.RoundLimit <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Round limit must be positive.");
        if (request.Participants.Count == 0) throw new ArgumentException("A battle requires participants.", nameof(request));

        BattleEncounterParticipant[] participants = request.Participants
            .Select(actor => new BattleEncounterParticipant(actor.State, actor.Entity.DisplayName))
            .ToArray();
        var turnHandler = new AutomatedBattleTurnHandler(
            _executor,
            _selector,
            _services,
            request.Participants,
            _restrictionResolver,
            request.TeamKnowledgeSeeds);
        var services = new BattleEncounterServices(
            new ParticipantOrderInitiativePolicy(),
            new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
            _lifecycle,
            turnHandler,
            new LastTeamStandingCompletionPolicy(),
            _turnEconomy.CreateEconomy,
            _turnEconomy.PhaseProgress,
            _encounterProgress);
        BattleEncounterResult result = await new BattleEncounterRunner().RunAsync(
                new BattleEncounterRequest(
                    participants,
                    request.ContextId,
                    request.BattleKindId,
                    request.MoonPhaseId,
                    request.RoundLimit),
                services,
                cancellationToken)
            .ConfigureAwait(false);

        return new AutomatedBattleResult(
            result.Outcome switch
            {
                BattleEncounterOutcome.Victory => AutomatedBattleOutcome.Victory,
                BattleEncounterOutcome.Defeat => AutomatedBattleOutcome.Defeat,
                BattleEncounterOutcome.Escape => AutomatedBattleOutcome.Escape,
                BattleEncounterOutcome.Draw => AutomatedBattleOutcome.Draw,
                BattleEncounterOutcome.Faulted => AutomatedBattleOutcome.Faulted,
                BattleEncounterOutcome.Cancelled => AutomatedBattleOutcome.Cancelled,
                _ => throw new InvalidOperationException(
                    $"Unsupported encounter outcome '{result.Outcome}'.")
            },
            result.WinningTeamId,
            result.Participants,
            result.Events,
            turnHandler.KnowledgeSnapshots,
            result.FaultMessage,
            result.FaultCode);
    }

    private sealed class AutomatedBattleTurnHandler : IBattleEncounterTurnHandler
    {
        private static readonly RuntimeKnowledgeSnapshot NoPersistentKnowledge = new();
        private readonly ISkillExecutor _executor;
        private readonly IBattleActionSelector _selector;
        private readonly IReadOnlyList<CatalogBattleActor> _actors;
        private readonly Dictionary<ContentId, RuntimeEncounterKnowledgeSnapshot> _knowledge;
        private readonly IBattleKnowledgeExecutionTransitionService _knowledgeTransitions =
            new BattleKnowledgeExecutionTransitionService();
        private readonly IAutomatedBattleTurnRestrictionResolver _restrictionResolver;

        public AutomatedBattleTurnHandler(
            ISkillExecutor executor,
            IBattleActionSelector selector,
            BattleExecutionServices services,
            IReadOnlyList<CatalogBattleActor> actors,
            IAutomatedBattleTurnRestrictionResolver restrictionResolver,
            IReadOnlyDictionary<ContentId, RuntimeEncounterKnowledgeSnapshot> knowledgeSeeds)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            ArgumentNullException.ThrowIfNull(services);
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _restrictionResolver = restrictionResolver ?? throw new ArgumentNullException(nameof(restrictionResolver));
            ArgumentNullException.ThrowIfNull(knowledgeSeeds);
            _knowledge = _actors.Select(actor => actor.State.TeamId).Distinct()
                .ToDictionary(
                    team => team,
                    team => knowledgeSeeds.TryGetValue(team, out RuntimeEncounterKnowledgeSnapshot? seed)
                        ? seed
                        : RuntimeEncounterKnowledgeSnapshot.Empty);
        }

        public IReadOnlyDictionary<ContentId, RuntimeEncounterKnowledgeSnapshot> KnowledgeSnapshots =>
            new ReadOnlyDictionary<ContentId, RuntimeEncounterKnowledgeSnapshot>(
                new Dictionary<ContentId, RuntimeEncounterKnowledgeSnapshot>(_knowledge));

        public ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CatalogBattleActor actor = _actors.Single(actor => actor.State.InstanceId == request.Actor.InstanceId);
            var events = new List<BattleEncounterEvent>();

            if (request.TurnStartOutcome != BattleTurnStartOutcome.CanAct)
            {
                return ResolveRestrictedTurnAsync(
                    new AutomatedBattleTurnRestrictionRequest(
                        request,
                        actor,
                        _actors,
                        KnowledgeView(actor.State.TeamId)),
                    cancellationToken);
            }

            var selectionRequest = new BattleActionSelectionRequest(
                actor,
                _actors,
                request.Encounter.ContextId,
                request.Encounter.BattleKindId,
                request.Encounter.MoonPhaseId,
                KnowledgeView(actor.State.TeamId),
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

            RecordExecution(events, actor, selection.Skill, execution);
            if (!TryApplyKnowledge(actor, selection.Skill.Id, execution.Effects, out string? knowledgeFault))
            {
                return FaultedAutomatedAction(actor, knowledgeFault!, events);
            }
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
                BattleEncounterEventKind.ActionRejected,
                new BattleActionRejectedEventPayload(
                    actor.State.InstanceId,
                    BattleEncounterCommandStatus.Faulted),
                fault));
            return new ValueTask<BattleEncounterCommandResult>(
                BattleEncounterCommandResult.Faulted(fault, events));
        }

        private IBattleKnowledgeView KnowledgeView(ContentId teamId) =>
            new BattleKnowledgeView(NoPersistentKnowledge, _knowledge[teamId]);

        private async ValueTask<BattleEncounterCommandResult> ResolveRestrictedTurnAsync(
            AutomatedBattleTurnRestrictionRequest request,
            CancellationToken cancellationToken)
        {
            BattleEncounterCommandResult result = await _restrictionResolver.ResolveAsync(
                request,
                cancellationToken).ConfigureAwait(false);
            if (result.Status != BattleEncounterCommandStatus.Executed)
            {
                return result;
            }

            BattleEffectResolvedEventPayload[] resolvedEffects = result.Events
                .Select(battleEvent => battleEvent.Payload)
                .OfType<BattleEffectResolvedEventPayload>()
                .ToArray();
            if (resolvedEffects.Length == 0)
            {
                return result;
            }

            ContentId[] selectedActionIds = result.Events
                .Select(battleEvent => battleEvent.Payload)
                .OfType<BattleCommandSelectedEventPayload>()
                .Where(payload => payload.ActorId == request.Actor.State.InstanceId)
                .Select(payload => payload.ActionId)
                .Distinct()
                .ToArray();
            if (selectedActionIds.Length != 1)
            {
                var events = result.Events.ToList();
                return await FaultedAutomatedAction(
                    request.Actor,
                    "Automated restricted-action evidence did not identify one selected action.",
                    events).ConfigureAwait(false);
            }

            ContentId sourceActionId = selectedActionIds[0];
            BattleEffectResolvedEventPayload[] actionEffects = resolvedEffects
                .Where(payload =>
                    payload.SourceActorId == request.Actor.State.InstanceId &&
                    payload.SourceId == sourceActionId)
                .ToArray();
            if (actionEffects.Length == 0)
            {
                return result;
            }
            if (TryApplyKnowledge(
                    request.Actor,
                    sourceActionId,
                    actionEffects.Select(payload => payload.Result),
                    out string? fault))
            {
                return result;
            }

            var faultEvents = result.Events.ToList();
            return await FaultedAutomatedAction(request.Actor, fault!, faultEvents).ConfigureAwait(false);
        }

        private bool TryApplyKnowledge(
            CatalogBattleActor actor,
            ContentId sourceActionId,
            IEnumerable<EffectExecutionResult> effects,
            out string? fault)
        {
            RuntimeEncounterKnowledgeSnapshot current = _knowledge[actor.State.TeamId];
            BattleKnowledgeExecutionTransitionResult transition = _knowledgeTransitions.Apply(
                new BattleKnowledgeExecutionTransitionRequest(
                    NoPersistentKnowledge,
                    current,
                    new BattleKnowledgeExecutionAuthority(
                        sourceActionId,
                        actor.State.InstanceId,
                        _actors.Select(participant =>
                            KeyValuePair.Create(
                                participant.State.InstanceId,
                                participant.State.CombatProfileIdentity))),
                    effects,
                    BattleKnowledgePersistenceScope.EncounterOnly));
            if (transition.Status == BattleKnowledgeTransitionStatus.Rejected)
            {
                fault = "Automated battle knowledge was rejected: " +
                    string.Join("; ", transition.Diagnostics.Select(item => item.Message));
                return false;
            }

            _knowledge[actor.State.TeamId] = transition.EncounterAfter;
            fault = null;
            return true;
        }
    }

    private static void RecordExecution(
        List<BattleEncounterEvent> events,
        CatalogBattleActor actor,
        SkillDefinition skill,
        SkillExecutionResult execution)
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
                        passive.EventId,
                        passive),
                    $"Passive {passive.SkillId} resolved as {passive.Outcome}."));
            }
        }

        events.AddRange(BattleStatusLifecycleEventMapper.MapAll(
            execution.LifecycleEvents,
            statusEvent => $"Action lifecycle transition: {statusEvent.Kind}."));
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
