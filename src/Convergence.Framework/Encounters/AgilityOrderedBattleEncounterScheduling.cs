using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Encounters;

/// <summary>Provides one immutable set of equal-stat participants to an injected tie-break policy.</summary>
public sealed class BattleEncounterScheduleTieBreakRequest
{
    public BattleEncounterScheduleTieBreakRequest(
        IEnumerable<BattleEncounterScheduleParticipantSnapshot> participants,
        ContentId orderingStatId,
        decimal orderingStatValue,
        int roundNumber)
    {
        Participants = BattleEncounterScheduleStartRequest.SnapshotParticipants(
            participants ?? throw new ArgumentNullException(nameof(participants)));
        if (!orderingStatId.IsValid)
        {
            throw new ArgumentException("Ordering stat ID must be valid.", nameof(orderingStatId));
        }

        if (orderingStatValue < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orderingStatValue),
                "Ordering stat value cannot be negative.");
        }

        if (roundNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(roundNumber),
                "Tie-break round number must be positive.");
        }

        OrderingStatId = orderingStatId;
        OrderingStatValue = orderingStatValue;
        RoundNumber = roundNumber;
    }

    public IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> Participants { get; }
    public ContentId OrderingStatId { get; }
    public decimal OrderingStatValue { get; }
    public int RoundNumber { get; }
}

/// <summary>Orders participants whose primary scheduling stat is equal.</summary>
public interface IBattleEncounterScheduleTieBreakPolicy
{
    ContentId PolicyId { get; }

    IReadOnlyList<RuntimeInstanceId> Order(BattleEncounterScheduleTieBreakRequest request);
}

/// <summary>Resolves equal-stat participants by their stable encounter participant order.</summary>
public sealed class EncounterOrderBattleEncounterScheduleTieBreakPolicy :
    IBattleEncounterScheduleTieBreakPolicy
{
    public static ContentId TieBreakPolicyId { get; } = ContentId.Parse("encounter_order_tie_break");

    public ContentId PolicyId => TieBreakPolicyId;

    public IReadOnlyList<RuntimeInstanceId> Order(BattleEncounterScheduleTieBreakRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Array.AsReadOnly(
            request.Participants
                .Select(participant => participant.InstanceId)
                .ToArray());
    }
}

/// <summary>
/// Supplied scheduler that freezes one descending-stat actor order per round.
/// Each actor receives a distinct one-actor phase.
/// </summary>
public sealed class AgilityOrderedBattleEncounterSchedulePolicy :
    IBattleEncounterSchedulePolicy
{
    public static ContentId ScheduleId { get; } = ContentId.Parse("agility_ordered");

    public AgilityOrderedBattleEncounterSchedulePolicy(
        ContentId agilityStatId,
        IBattleEncounterScheduleTieBreakPolicy tieBreakPolicy)
    {
        if (!agilityStatId.IsValid)
        {
            throw new ArgumentException("Agility stat ID must be valid.", nameof(agilityStatId));
        }

        AgilityStatId = agilityStatId;
        TieBreakPolicy = tieBreakPolicy ?? throw new ArgumentNullException(nameof(tieBreakPolicy));
    }

    public ContentId PolicyId => ScheduleId;
    public ContentId AgilityStatId { get; }
    public IBattleEncounterScheduleTieBreakPolicy TieBreakPolicy { get; }

    public BattleEncounterScheduleTransitionResult Start(
        BattleEncounterScheduleStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var state = new AgilityOrderedScheduleState(
            revision: 0,
            currentRound: 1,
            completedRounds: 0,
            nextStepSequence: 0,
            request.Participants.Select(participant => participant.InstanceId),
            request.TeamOrder,
            request.RoundLimit,
            roundActorOrder: [],
            nextActorIndex: 0,
            currentActorId: null,
            currentTeamId: null);
        return BattleEncounterScheduleTransitionResult.Start(
            state,
            new BattleEncounterRoundStartedScheduleStep(PolicyId, 0, 1));
    }

    public BattleEncounterScheduleTransitionResult Advance(
        BattleEncounterScheduleAdvanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.State is not AgilityOrderedScheduleState state)
        {
            return Reject(
                request.State,
                BattleEncounterScheduleDiagnosticCode.InvalidState,
                $"Scheduling policy '{PolicyId}' cannot advance state type " +
                $"'{request.State.GetType().Name}'.");
        }

        return request.CompletedStep switch
        {
            BattleEncounterRoundStartedScheduleStep =>
                ResolveRoundOrder(state, request.Participants),
            BattleEncounterPhaseStartedScheduleStep phase =>
                AdvancePhaseStart(state, phase, request.Outcome),
            BattleEncounterCommandWindowScheduleStep command =>
                AdvanceCommand(state, command, request.Outcome),
            BattleEncounterPhaseEndedScheduleStep phase =>
                AdvancePhaseEnd(state, phase, request.Participants),
            BattleEncounterRoundEndedScheduleStep =>
                AdvanceRound(state),
            _ => Reject(
                state,
                BattleEncounterScheduleDiagnosticCode.InvalidStep,
                $"Scheduling policy '{PolicyId}' does not support step type " +
                $"'{request.CompletedStep.GetType().Name}'.")
        };
    }

    private BattleEncounterScheduleTransitionResult ResolveRoundOrder(
        AgilityOrderedScheduleState state,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants)
    {
        BattleEncounterScheduleParticipantSnapshot[] available = participants
            .Where(participant => participant.IsAvailable)
            .ToArray();
        foreach (BattleEncounterScheduleParticipantSnapshot participant in available)
        {
            if (!participant.EffectiveStats.TryGetValue(AgilityStatId, out decimal agility))
            {
                return Reject(
                    state,
                    BattleEncounterScheduleDiagnosticCode.MissingOrderingStat,
                    $"Participant '{participant.InstanceId}' has no resolved " +
                    $"'{AgilityStatId}' scheduling stat.");
            }

            if (agility < 0m)
            {
                return Reject(
                    state,
                    BattleEncounterScheduleDiagnosticCode.InvalidOrderingStat,
                    $"Participant '{participant.InstanceId}' has a negative " +
                    $"'{AgilityStatId}' scheduling stat.");
            }
        }

        if (!TieBreakPolicy.PolicyId.IsValid)
        {
            return Reject(
                state,
                BattleEncounterScheduleDiagnosticCode.InvalidTieBreakOrder,
                "The injected encounter tie-break policy returned an invalid policy ID.");
        }

        var actorOrder = new List<RuntimeInstanceId>(available.Length);
        foreach (IGrouping<decimal, BattleEncounterScheduleParticipantSnapshot> group in
                 available
                     .GroupBy(participant => participant.EffectiveStats[AgilityStatId])
                     .OrderByDescending(group => group.Key))
        {
            BattleEncounterScheduleParticipantSnapshot[] tied = group.ToArray();
            IReadOnlyList<RuntimeInstanceId>? proposed = TieBreakPolicy.Order(
                new BattleEncounterScheduleTieBreakRequest(
                    tied,
                    AgilityStatId,
                    group.Key,
                    state.CurrentRound));
            if (!IsExactPermutation(
                    proposed,
                    tied.Select(participant => participant.InstanceId).ToArray()))
            {
                return Reject(
                    state,
                    BattleEncounterScheduleDiagnosticCode.InvalidTieBreakOrder,
                    $"Tie-break policy '{TieBreakPolicy.PolicyId}' did not return every " +
                    "tied participant exactly once.");
            }

            actorOrder.AddRange(proposed!);
        }

        AgilityOrderedScheduleState orderedState = state.Advance(
            roundActorOrder: actorOrder,
            nextActorIndex: 0,
            currentActorId: null,
            currentTeamId: null);
        return SelectNextActorOrRoundEnd(orderedState, participants, transitionBefore: state);
    }

    private static BattleEncounterScheduleTransitionResult AdvancePhaseStart(
        AgilityOrderedScheduleState state,
        BattleEncounterPhaseStartedScheduleStep phase,
        BattleEncounterScheduleStepOutcome outcome)
    {
        if (state.CurrentActorId is not RuntimeInstanceId actorId ||
            state.CurrentTeamId is not ContentId teamId ||
            phase.TeamId != teamId)
        {
            return Reject(
                state,
                BattleEncounterScheduleDiagnosticCode.InvalidStep,
                "The phase-start step does not match the current agility-order actor.");
        }

        AgilityOrderedScheduleState after = state.Advance(
            roundActorOrder: state.RoundActorOrder,
            nextActorIndex: state.NextActorIndex,
            currentActorId: actorId,
            currentTeamId: teamId);
        BattleEncounterScheduleStep nextStep = outcome.HasRemainingOpportunities == true
            ? new BattleEncounterCommandWindowScheduleStep(
                ScheduleId,
                after.NextStepSequence,
                after.CurrentRound,
                actorId,
                teamId)
            : new BattleEncounterPhaseEndedScheduleStep(
                ScheduleId,
                after.NextStepSequence,
                after.CurrentRound,
                teamId);
        return BattleEncounterScheduleTransitionResult.Advance(state, after, nextStep);
    }

    private static BattleEncounterScheduleTransitionResult AdvanceCommand(
        AgilityOrderedScheduleState state,
        BattleEncounterCommandWindowScheduleStep command,
        BattleEncounterScheduleStepOutcome outcome)
    {
        if (state.CurrentActorId is not RuntimeInstanceId actorId ||
            state.CurrentTeamId is not ContentId teamId ||
            command.ActorId != actorId ||
            command.TeamId != teamId)
        {
            return Reject(
                state,
                BattleEncounterScheduleDiagnosticCode.InvalidStep,
                "The command-window step does not match the current agility-order actor.");
        }

        bool retainActor =
            outcome.Kind == BattleEncounterScheduleStepOutcomeKind.CommandCommitted &&
            outcome.HasRemainingOpportunities == true;
        AgilityOrderedScheduleState after = state.Advance(
            roundActorOrder: state.RoundActorOrder,
            nextActorIndex: state.NextActorIndex,
            currentActorId: actorId,
            currentTeamId: teamId);
        BattleEncounterScheduleStep nextStep = retainActor
            ? new BattleEncounterCommandWindowScheduleStep(
                ScheduleId,
                after.NextStepSequence,
                after.CurrentRound,
                actorId,
                teamId)
            : new BattleEncounterPhaseEndedScheduleStep(
                ScheduleId,
                after.NextStepSequence,
                after.CurrentRound,
                teamId);
        return BattleEncounterScheduleTransitionResult.Advance(state, after, nextStep);
    }

    private static BattleEncounterScheduleTransitionResult AdvancePhaseEnd(
        AgilityOrderedScheduleState state,
        BattleEncounterPhaseEndedScheduleStep phase,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants)
    {
        if (state.CurrentTeamId is not ContentId teamId || phase.TeamId != teamId)
        {
            return Reject(
                state,
                BattleEncounterScheduleDiagnosticCode.InvalidStep,
                "The phase-end step does not match the current agility-order actor.");
        }

        AgilityOrderedScheduleState phaseEnded = state.Advance(
            roundActorOrder: state.RoundActorOrder,
            nextActorIndex: state.NextActorIndex,
            currentActorId: null,
            currentTeamId: null);
        return SelectNextActorOrRoundEnd(phaseEnded, participants, transitionBefore: state);
    }

    private static BattleEncounterScheduleTransitionResult SelectNextActorOrRoundEnd(
        AgilityOrderedScheduleState state,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants,
        AgilityOrderedScheduleState transitionBefore)
    {
        int nextActorIndex = state.NextActorIndex;
        while (nextActorIndex < state.RoundActorOrder.Count)
        {
            RuntimeInstanceId actorId = state.RoundActorOrder[nextActorIndex++];
            BattleEncounterScheduleParticipantSnapshot? participant = participants
                .SingleOrDefault(candidate => candidate.InstanceId == actorId);
            if (participant is null || !participant.IsAvailable)
            {
                continue;
            }

            AgilityOrderedScheduleState actorState = state.WithoutRevisionAdvance(
                nextActorIndex,
                actorId,
                participant.TeamId);
            return BattleEncounterScheduleTransitionResult.Advance(
                transitionBefore,
                actorState,
                new BattleEncounterPhaseStartedScheduleStep(
                    ScheduleId,
                    actorState.NextStepSequence,
                    actorState.CurrentRound,
                    participant.TeamId,
                    new BattleEncounterTurnEconomyStart(1)));
        }

        AgilityOrderedScheduleState roundEndState = state.WithoutRevisionAdvance(
            nextActorIndex,
            currentActorId: null,
            currentTeamId: null);
        return BattleEncounterScheduleTransitionResult.Advance(
            transitionBefore,
            roundEndState,
            new BattleEncounterRoundEndedScheduleStep(
                ScheduleId,
                roundEndState.NextStepSequence,
                roundEndState.CurrentRound));
    }

    private static BattleEncounterScheduleTransitionResult AdvanceRound(
        AgilityOrderedScheduleState state)
    {
        if (state.CurrentRound >= state.RoundLimit)
        {
            AgilityOrderedScheduleState completed = state.Advance(
                currentRound: state.CurrentRound,
                completedRounds: state.CurrentRound,
                roundActorOrder: state.RoundActorOrder,
                nextActorIndex: state.NextActorIndex,
                currentActorId: null,
                currentTeamId: null);
            return BattleEncounterScheduleTransitionResult.Complete(state, completed);
        }

        AgilityOrderedScheduleState nextRound = state.Advance(
            currentRound: checked(state.CurrentRound + 1),
            completedRounds: state.CurrentRound,
            roundActorOrder: [],
            nextActorIndex: 0,
            currentActorId: null,
            currentTeamId: null);
        return BattleEncounterScheduleTransitionResult.Advance(
            state,
            nextRound,
            new BattleEncounterRoundStartedScheduleStep(
                ScheduleId,
                nextRound.NextStepSequence,
                nextRound.CurrentRound));
    }

    private static bool IsExactPermutation(
        IReadOnlyList<RuntimeInstanceId>? proposed,
        IReadOnlyList<RuntimeInstanceId> expected) =>
        proposed is not null &&
        proposed.Count == expected.Count &&
        proposed.Distinct().Count() == proposed.Count &&
        proposed.All(expected.Contains);

    private static BattleEncounterScheduleTransitionResult Reject(
        BattleEncounterScheduleStateSnapshot state,
        BattleEncounterScheduleDiagnosticCode code,
        string message) =>
        BattleEncounterScheduleTransitionResult.RejectAdvance(
            state,
            [new BattleEncounterScheduleDiagnostic(code, message)]);

    private sealed class AgilityOrderedScheduleState :
        BattleEncounterScheduleStateSnapshot
    {
        public AgilityOrderedScheduleState(
            long revision,
            int currentRound,
            int completedRounds,
            long nextStepSequence,
            IEnumerable<RuntimeInstanceId> participantIds,
            IEnumerable<ContentId> teamOrder,
            int roundLimit,
            IEnumerable<RuntimeInstanceId> roundActorOrder,
            int nextActorIndex,
            RuntimeInstanceId? currentActorId,
            ContentId? currentTeamId)
            : base(
                ScheduleId,
                revision,
                currentRound,
                completedRounds,
                nextStepSequence,
                participantIds,
                teamOrder,
                roundLimit)
        {
            IReadOnlyList<RuntimeInstanceId> order = RuntimeSnapshotCollections.List(
                roundActorOrder ?? throw new ArgumentNullException(nameof(roundActorOrder)));
            if (order.Any(actorId => !actorId.IsValid) ||
                order.Distinct().Count() != order.Count ||
                order.Any(actorId => !ParticipantIds.Contains(actorId)))
            {
                throw new ArgumentException(
                    "Round actor order must contain unique encounter participant IDs.",
                    nameof(roundActorOrder));
            }

            if (nextActorIndex < 0 || nextActorIndex > order.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(nextActorIndex));
            }

            if (currentActorId is RuntimeInstanceId actorId &&
                (!actorId.IsValid || !ParticipantIds.Contains(actorId)))
            {
                throw new ArgumentException(
                    "Current actor must identify an encounter participant.",
                    nameof(currentActorId));
            }

            if (currentActorId.HasValue != currentTeamId.HasValue ||
                currentTeamId is ContentId teamId && !TeamOrder.Contains(teamId))
            {
                throw new ArgumentException(
                    "Current actor and team must be present together and identify the encounter.");
            }

            RoundActorOrder = order;
            NextActorIndex = nextActorIndex;
            CurrentActorId = currentActorId;
            CurrentTeamId = currentTeamId;
        }

        public IReadOnlyList<RuntimeInstanceId> RoundActorOrder { get; }
        public int NextActorIndex { get; }
        public RuntimeInstanceId? CurrentActorId { get; }
        public ContentId? CurrentTeamId { get; }

        public AgilityOrderedScheduleState Advance(
            int? currentRound = null,
            int? completedRounds = null,
            IEnumerable<RuntimeInstanceId>? roundActorOrder = null,
            int? nextActorIndex = null,
            RuntimeInstanceId? currentActorId = null,
            ContentId? currentTeamId = null) =>
            new(
                checked(Revision + 1),
                currentRound ?? CurrentRound,
                completedRounds ?? CompletedRounds,
                checked(NextStepSequence + 1),
                ParticipantIds,
                TeamOrder,
                RoundLimit,
                roundActorOrder ?? RoundActorOrder,
                nextActorIndex ?? NextActorIndex,
                currentActorId,
                currentTeamId);

        public AgilityOrderedScheduleState WithoutRevisionAdvance(
            int nextActorIndex,
            RuntimeInstanceId? currentActorId,
            ContentId? currentTeamId) =>
            new(
                Revision,
                CurrentRound,
                CompletedRounds,
                NextStepSequence,
                ParticipantIds,
                TeamOrder,
                RoundLimit,
                RoundActorOrder,
                nextActorIndex,
                currentActorId,
                currentTeamId);
    }
}
