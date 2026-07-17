using Convergence.Content;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class TimedExclusiveStatModifierPolicyTests
{
    private static readonly ContentId PolicyId = ContentId.Parse("timed_exclusive_test");
    private static readonly ContentId Attack = ContentId.Parse("attack");
    private static readonly ContentId Defense = ContentId.Parse("defense");
    private static readonly ContentId OwnerTurn = ContentId.Parse("owner_turn_completed");
    private static readonly ContentId Round = ContentId.Parse("round_completed");

    [Fact]
    public void Constructor_RequiresValidPolicyIdentity()
    {
        Assert.Throws<ArgumentException>(() => new TimedExclusiveStatModifierPolicy(default));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(2)]
    public void Apply_AcceptsEachNonneutralSignal(int signal)
    {
        StatModifierTransitionResult result = Apply(
            Empty(),
            signal,
            duration: 3,
            activeBoundarySequence: 7);

        Assert.Equal(StatModifierTransitionCode.Applied, result.Code);
        RuntimeStatModifierTrackSnapshot track = Assert.Single(result.After.Tracks);
        RuntimeStatModifierContributionSnapshot contribution = Assert.Single(track.Contributions);
        Assert.Equal(signal, track.ResolvedStage);
        Assert.Equal(signal, contribution.StageDelta);
        Assert.Equal(3, Assert.IsType<TurnDurationDefinition>(contribution.Duration).Value);
        Assert.Equal(7, Assert.IsType<StatModifierLifecycleBoundary>(
            contribution.LastLifecycleBoundary).Sequence);
    }

    [Fact]
    public void Apply_RejectsOutOfScaleSignalsAndNoncountedDurations()
    {
        var service = Service();

        StatModifierTransitionResult magnitude = service.Apply(
            new StatModifierApplicationRequest(Empty(), Attack, 3, Turns(3)));
        StatModifierTransitionResult missingDuration = service.Apply(
            new StatModifierApplicationRequest(Empty(), Attack, 1));
        StatModifierTransitionResult battleDuration = service.Apply(
            new StatModifierApplicationRequest(
                Empty(),
                Attack,
                1,
                new BattleDurationDefinition()));

        Assert.Equal(StatModifierDiagnosticCode.InvalidStageDelta, Assert.Single(magnitude.Diagnostics).Code);
        Assert.Equal(StatModifierDiagnosticCode.InvalidDuration, Assert.Single(missingDuration.Diagnostics).Code);
        Assert.Equal(StatModifierDiagnosticCode.InvalidDuration, Assert.Single(battleDuration.Diagnostics).Code);
        Assert.All([magnitude, missingDuration, battleDuration], result => Assert.Same(result.Before, result.After));
    }

    [Fact]
    public void SameDirection_RefreshesEqual_UpgradesStronger_AndRejectsWeaker()
    {
        RuntimeStatModifierStateSnapshot positive = State(SignalTrack(1, 1, 1, boundarySequence: 4));

        StatModifierTransitionResult refreshed = Apply(positive, 1, duration: 3, activeBoundarySequence: 5);
        RuntimeStatModifierContributionSnapshot refreshedContribution = Contribution(refreshed.After);
        Assert.Equal(1, Stage(refreshed.After));
        Assert.Equal(3, Assert.IsType<TurnDurationDefinition>(refreshedContribution.Duration).Value);
        Assert.Equal(5, refreshedContribution.LastLifecycleBoundary!.Sequence);

        StatModifierTransitionResult upgraded = Apply(positive, 2, duration: 4, activeBoundarySequence: 5);
        Assert.Equal(2, Stage(upgraded.After));
        Assert.Equal(4, Assert.IsType<TurnDurationDefinition>(Contribution(upgraded.After).Duration).Value);

        RuntimeStatModifierStateSnapshot strong = State(SignalTrack(2, 2, 1, boundarySequence: 4));
        StatModifierTransitionResult rejectedAssessment = Service().AssessApplication(
            Request(strong, 1, duration: 3, activeBoundarySequence: 5));
        StatModifierTransitionResult rejectedExecution = Service().Apply(
            Request(strong, 1, duration: 3, activeBoundarySequence: 5));

        Assert.Equal(StatModifierTransitionCode.Rejected, rejectedAssessment.Code);
        Assert.Equal(StatModifierDiagnosticCode.AlreadyInEffect, Assert.Single(rejectedAssessment.Diagnostics).Code);
        Assert.Same(strong, rejectedAssessment.After);
        Assert.Equal(rejectedAssessment.Diagnostics.Select(diagnostic => diagnostic.Code),
            rejectedExecution.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Theory]
    [InlineData(1, 1, 1, false)]
    [InlineData(1, 2, 2, false)]
    [InlineData(2, 1, 2, true)]
    [InlineData(2, 2, 2, false)]
    [InlineData(-1, -1, -1, false)]
    [InlineData(-1, -2, -2, false)]
    [InlineData(-2, -1, -2, true)]
    [InlineData(-2, -2, -2, false)]
    [InlineData(1, -1, 0, false)]
    [InlineData(1, -2, -1, false)]
    [InlineData(2, -1, 1, false)]
    [InlineData(2, -2, 0, false)]
    [InlineData(-1, 1, 0, false)]
    [InlineData(-1, 2, 1, false)]
    [InlineData(-2, 1, -1, false)]
    [InlineData(-2, 2, 0, false)]
    public void SignalTransitionMatrix_CoversEveryOccupiedStateAndIncomingSignal(
        int current,
        int incoming,
        int expected,
        bool expectedRejection)
    {
        RuntimeStatModifierStateSnapshot state = State(
            SignalTrack(current, duration: 1, sequence: 1, boundarySequence: 4));

        StatModifierTransitionResult result = Apply(
            state,
            incoming,
            duration: 3,
            activeBoundarySequence: 5);

        Assert.Equal(expectedRejection, result.Code == StatModifierTransitionCode.Rejected);
        if (expectedRejection)
        {
            Assert.Equal(StatModifierDiagnosticCode.AlreadyInEffect, Assert.Single(result.Diagnostics).Code);
            Assert.Same(state, result.After);
        }
        else if (expected == 0)
        {
            Assert.Empty(result.After.Tracks);
        }
        else
        {
            Assert.Equal(expected, Stage(result.After));
        }
    }

    [Theory]
    [InlineData(1, -1, 0, false)]
    [InlineData(2, -1, 1, false)]
    [InlineData(1, -2, -1, true)]
    [InlineData(-1, 1, 0, false)]
    [InlineData(-2, 1, -1, false)]
    [InlineData(-1, 2, 1, true)]
    public void OppositeSignals_OffsetAndKeepTheDominantEffectsTimer(
        int current,
        int incoming,
        int expected,
        bool incomingWins)
    {
        RuntimeStatModifierStateSnapshot state = State(
            SignalTrack(current, duration: 1, sequence: 9, boundarySequence: 4));

        StatModifierTransitionResult result = Apply(
            state,
            incoming,
            duration: 3,
            activeBoundarySequence: 10);

        Assert.Equal(StatModifierTransitionCode.Applied, result.Code);
        if (expected == 0)
        {
            Assert.Empty(result.After.Tracks);
            return;
        }

        RuntimeStatModifierContributionSnapshot contribution = Contribution(result.After);
        Assert.Equal(expected, Stage(result.After));
        Assert.Equal(incomingWins ? 3 : 1,
            Assert.IsType<TurnDurationDefinition>(contribution.Duration).Value);
        Assert.Equal(incomingWins ? 10 : 4, contribution.LastLifecycleBoundary!.Sequence);
    }

    [Fact]
    public void Tick_ProtectsApplicationBoundary_AdvancesOnce_AndExpires()
    {
        RuntimeStatModifierStateSnapshot state = State(
            SignalTrack(1, duration: 3, sequence: 1, boundarySequence: 12));
        var service = Service();

        StatModifierTransitionResult sameBoundary = Tick(service, state, OwnerTurn, 12);
        Assert.Equal(StatModifierTransitionCode.Unchanged, sameBoundary.Code);
        Assert.Same(state, sameBoundary.After);

        StatModifierTransitionResult first = Tick(service, state, OwnerTurn, 13);
        Assert.Equal(2, Duration(first.After));
        Assert.Equal(13, Contribution(first.After).LastLifecycleBoundary!.Sequence);
        StatModifierEvent updated = Assert.Single(first.Events);
        Assert.Equal(StatModifierEventKind.ContributionUpdated, updated.Kind);
        Assert.Equal(12, updated.PreviousLifecycleBoundary!.Sequence);
        Assert.Equal(13, updated.CurrentLifecycleBoundary!.Sequence);

        StatModifierTransitionResult duplicate = Tick(service, first.After, OwnerTurn, 13);
        Assert.Equal(StatModifierTransitionCode.Unchanged, duplicate.Code);
        Assert.Same(first.After, duplicate.After);

        StatModifierTransitionResult second = Tick(service, first.After, OwnerTurn, 14);
        Assert.Equal(1, Duration(second.After));
        StatModifierTransitionResult expired = Tick(service, second.After, OwnerTurn, 15);
        Assert.Empty(expired.After.Tracks);
        Assert.Equal(
            [
                StatModifierEventKind.ContributionExpired,
                StatModifierEventKind.AggregateStageChanged,
                StatModifierEventKind.TrackRemoved
            ],
            expired.Events.Select(@event => @event.Kind));
    }

    [Fact]
    public void Tick_RejectsOutOfOrderBoundaryWithoutMutation()
    {
        RuntimeStatModifierStateSnapshot state = State(
            SignalTrack(1, duration: 2, sequence: 1, boundarySequence: 9));

        StatModifierTransitionResult result = Tick(Service(), state, OwnerTurn, 8);

        Assert.Equal(StatModifierTransitionCode.Rejected, result.Code);
        Assert.Equal(StatModifierDiagnosticCode.InvalidLifecycleBoundary, Assert.Single(result.Diagnostics).Code);
        Assert.Same(state, result.After);
    }

    [Fact]
    public void Tick_AfterApplicationOutsideTargetBoundary_DecrementsAtTargetsNextCompletion()
    {
        StatModifierTransitionResult applied = Apply(Empty(), 1, duration: 3);
        Assert.Null(Contribution(applied.After).LastLifecycleBoundary);

        StatModifierTransitionResult ticked = Tick(Service(), applied.After, OwnerTurn, 1);

        Assert.Equal(2, Duration(ticked.After));
        Assert.Equal(1, Contribution(ticked.After).LastLifecycleBoundary!.Sequence);
    }

    [Fact]
    public void Tick_UsesAuthoredReserveSuspensionAndStillObservesTheBoundary()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot suspended = State(
            SignalTrack(1, duration: 3, sequence: 1, boundarySequence: 1, suspendWhileReserve: true));
        RuntimeStatModifierStateSnapshot activeClock = State(
            SignalTrack(1, duration: 3, sequence: 1, boundarySequence: 1, suspendWhileReserve: false));

        StatModifierTransitionResult held = Tick(service, suspended, OwnerTurn, 2, isActorDeployed: false);
        Assert.Equal(3, Duration(held.After));
        Assert.Equal(2, Contribution(held.After).LastLifecycleBoundary!.Sequence);
        StatModifierTransitionResult duplicateAfterDeployment = Tick(
            service,
            held.After,
            OwnerTurn,
            2,
            isActorDeployed: true);
        Assert.Equal(StatModifierTransitionCode.Unchanged, duplicateAfterDeployment.Code);

        StatModifierTransitionResult continued = Tick(
            service,
            activeClock,
            OwnerTurn,
            2,
            isActorDeployed: false);
        Assert.Equal(2, Duration(continued.After));
    }

    [Fact]
    public void Tick_IgnoresOtherLifecycleClocks()
    {
        RuntimeStatModifierStateSnapshot state = State(
            SignalTrack(1, duration: 3, sequence: 1, boundarySequence: 2));

        StatModifierTransitionResult result = Tick(Service(), state, Round, 3);

        Assert.Equal(StatModifierTransitionCode.Unchanged, result.Code);
        Assert.Same(state, result.After);
    }

    [Fact]
    public void BoundaryValidation_RejectsMalformedMismatchedAndStaleApplicationMetadata()
    {
        var service = Service();
        StatModifierTransitionResult malformedTick = service.Tick(
            new StatModifierTickRequest(
                Empty(),
                new StatModifierLifecycleBoundary(default, 0),
                true));
        StatModifierTransitionResult mismatchedApplication = service.Apply(
            new StatModifierApplicationRequest(
                Empty(),
                Attack,
                1,
                Turns(3),
                activeLifecycleBoundary: new StatModifierLifecycleBoundary(Round, 1)));
        RuntimeStatModifierStateSnapshot current = State(
            SignalTrack(1, duration: 2, sequence: 1, boundarySequence: 5));
        StatModifierTransitionResult staleApplication = Apply(
            current,
            1,
            duration: 3,
            activeBoundarySequence: 4);

        Assert.Equal(2, malformedTick.Diagnostics.Count(diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.InvalidLifecycleBoundary));
        Assert.Equal(StatModifierDiagnosticCode.InvalidLifecycleBoundary,
            Assert.Single(mismatchedApplication.Diagnostics).Code);
        Assert.Equal(StatModifierDiagnosticCode.InvalidLifecycleBoundary,
            Assert.Single(staleApplication.Diagnostics).Code);
        Assert.Same(current, staleApplication.After);
    }

    [Fact]
    public void Apply_RejectsBoundaryOlderThanAnotherTrackWithoutChangingState()
    {
        RuntimeStatModifierStateSnapshot state = State(
            SignalTrack(1, duration: 3, sequence: 1, boundarySequence: 4, trackId: Attack),
            SignalTrack(1, duration: 3, sequence: 2, boundarySequence: 5, trackId: Defense));

        StatModifierTransitionResult result = Apply(
            state,
            1,
            duration: 3,
            activeBoundarySequence: 4);

        Assert.Equal(StatModifierTransitionCode.Rejected, result.Code);
        Assert.Equal(
            StatModifierDiagnosticCode.InvalidLifecycleBoundary,
            Assert.Single(result.Diagnostics).Code);
        Assert.Same(state, result.After);
    }

    [Fact]
    public void RemoveAndCleanup_SupportEverySharedScope()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = State(
            SignalTrack(1, 3, 1, 1, Attack),
            SignalTrack(-2, 3, 2, 1, Defense));

        Assert.Equal([Defense], service.Remove(
            new StatModifierRemovalRequest(state, StatModifierRemovalMode.Positive))
            .After.Tracks.Select(track => track.ModifierTrackId));
        Assert.Equal([Attack], service.Remove(
            new StatModifierRemovalRequest(state, StatModifierRemovalMode.Negative))
            .After.Tracks.Select(track => track.ModifierTrackId));
        Assert.Equal([Attack], service.Remove(
            new StatModifierRemovalRequest(
                state,
                StatModifierRemovalMode.SelectedTracks,
                modifierTrackIds: [Defense]))
            .After.Tracks.Select(track => track.ModifierTrackId));
        Assert.Equal([Defense], service.Remove(
            new StatModifierRemovalRequest(
                state,
                StatModifierRemovalMode.SelectedContributions,
                contributionSequences: [1]))
            .After.Tracks.Select(track => track.ModifierTrackId));
        Assert.Empty(service.Remove(
            new StatModifierRemovalRequest(state, StatModifierRemovalMode.All)).After.Tracks);
        Assert.Same(state, service.Cleanup(
            new StatModifierCleanupRequest(state, StatModifierCleanupScope.Swap)).After);
        Assert.Empty(service.Cleanup(
            new StatModifierCleanupRequest(state, StatModifierCleanupScope.EncounterEnd)).After.Tracks);
    }

    [Fact]
    public void ValidateState_RejectsNoncanonicalExclusiveShapes()
    {
        var service = Service();
        var state = new RuntimeStatModifierStateSnapshot(
            PolicyId,
            [
                new RuntimeStatModifierTrackSnapshot(
                    Attack,
                    3,
                    [new RuntimeStatModifierContributionSnapshot(1, 3, Turns(2))]),
                new RuntimeStatModifierTrackSnapshot(
                    Defense,
                    -1,
                    [
                        new RuntimeStatModifierContributionSnapshot(2, -1, Turns(2)),
                        new RuntimeStatModifierContributionSnapshot(3, -1, Turns(2))
                    ])
            ]);

        StatModifierValidationResult result = service.ValidateState(state);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Diagnostics.Count(diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.IncompatibleState));
    }

    [Fact]
    public void ValidateState_RejectsBoundaryThatDoesNotMatchCountedDuration()
    {
        var state = new RuntimeStatModifierStateSnapshot(
            PolicyId,
            [new RuntimeStatModifierTrackSnapshot(
                Attack,
                1,
                [new RuntimeStatModifierContributionSnapshot(
                    1,
                    1,
                    Turns(2),
                    new StatModifierLifecycleBoundary(Round, 1))])]);

        StatModifierValidationResult result = Service().ValidateState(state);

        Assert.False(result.IsValid);
        Assert.Equal(StatModifierDiagnosticCode.InvalidLifecycleBoundary, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Apply_RejectsSequenceExhaustionWithoutChangingState()
    {
        RuntimeStatModifierStateSnapshot state = State(
            SignalTrack(1, duration: 2, sequence: long.MaxValue, boundarySequence: 1, trackId: Defense));

        StatModifierTransitionResult result = Apply(state, 1, duration: 3);

        Assert.Equal(StatModifierTransitionCode.Rejected, result.Code);
        Assert.Equal(StatModifierDiagnosticCode.NumericOverflow, Assert.Single(result.Diagnostics).Code);
        Assert.Same(state, result.After);
    }

    [Theory]
    [InlineData(StatModifierCleanupScope.Swap, false)]
    [InlineData(StatModifierCleanupScope.ActorDeparture, true)]
    [InlineData(StatModifierCleanupScope.EncounterEnd, true)]
    [InlineData(StatModifierCleanupScope.FieldTransition, true)]
    public void Cleanup_UsesTheSharedScopeContract(
        StatModifierCleanupScope scope,
        bool expectedCleared)
    {
        RuntimeStatModifierStateSnapshot state = State(
            SignalTrack(1, duration: 2, sequence: 1, boundarySequence: 1));

        StatModifierTransitionResult result = Service().Cleanup(
            new StatModifierCleanupRequest(state, scope));

        Assert.Equal(expectedCleared, result.After.Tracks.Count == 0);
        Assert.Equal(expectedCleared
            ? StatModifierTransitionCode.Applied
            : StatModifierTransitionCode.Unchanged,
            result.Code);
    }

    private static StatModifierPolicyService Service() =>
        new(new TimedExclusiveStatModifierPolicy(PolicyId));

    private static StatModifierTransitionResult Apply(
        RuntimeStatModifierStateSnapshot state,
        int signal,
        int duration,
        long? activeBoundarySequence = null) =>
        Service().Apply(Request(state, signal, duration, activeBoundarySequence));

    private static StatModifierApplicationRequest Request(
        RuntimeStatModifierStateSnapshot state,
        int signal,
        int duration,
        long? activeBoundarySequence = null) =>
        new(
            state,
            Attack,
            signal,
            Turns(duration),
            activeLifecycleBoundary: activeBoundarySequence is long sequence
                ? new StatModifierLifecycleBoundary(OwnerTurn, sequence)
                : null);

    private static StatModifierTransitionResult Tick(
        StatModifierPolicyService service,
        RuntimeStatModifierStateSnapshot state,
        ContentId eventId,
        long sequence,
        bool isActorDeployed = true) =>
        service.Tick(new StatModifierTickRequest(
            state,
            new StatModifierLifecycleBoundary(eventId, sequence),
            isActorDeployed));

    private static RuntimeStatModifierStateSnapshot Empty() => new(PolicyId);

    private static RuntimeStatModifierStateSnapshot State(
        params RuntimeStatModifierTrackSnapshot[] tracks) =>
        new(PolicyId, tracks);

    private static RuntimeStatModifierTrackSnapshot SignalTrack(
        int signal,
        int duration,
        long sequence,
        long? boundarySequence,
        ContentId? trackId = null,
        bool suspendWhileReserve = false) =>
        new(
            trackId ?? Attack,
            signal,
            [new RuntimeStatModifierContributionSnapshot(
                sequence,
                signal,
                Turns(duration, suspendWhileReserve),
                boundarySequence is long value
                    ? new StatModifierLifecycleBoundary(OwnerTurn, value)
                    : null)]);

    private static TurnDurationDefinition Turns(int value, bool suspendWhileReserve = false) =>
        new(value, OwnerTurn, suspendWhileReserve);

    private static int Stage(RuntimeStatModifierStateSnapshot state) =>
        Assert.Single(state.Tracks).ResolvedStage;

    private static int Duration(RuntimeStatModifierStateSnapshot state) =>
        Assert.IsType<TurnDurationDefinition>(Contribution(state).Duration).Value;

    private static RuntimeStatModifierContributionSnapshot Contribution(
        RuntimeStatModifierStateSnapshot state) =>
        Assert.Single(Assert.Single(state.Tracks).Contributions);
}
