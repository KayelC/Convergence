using Convergence.Content;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class TimedContributionStatModifierPolicyTests
{
    private static readonly ContentId PolicyId = ContentId.Parse("timed_contribution_test");
    private static readonly ContentId Attack = ContentId.Parse("attack");
    private static readonly ContentId Defense = ContentId.Parse("defense");
    private static readonly ContentId OwnerTurn = ContentId.Parse("owner_turn_completed");
    private static readonly ContentId TeamPhase = ContentId.Parse("team_phase_completed");

    [Fact]
    public void Constructor_RequiresValidIdentityAndSignedBounds()
    {
        Assert.Throws<ArgumentException>(() => new TimedContributionStatModifierPolicy(default));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TimedContributionStatModifierPolicy(PolicyId, 0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TimedContributionStatModifierPolicy(PolicyId, -4, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TimedContributionStatModifierPolicy(PolicyId, -5, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TimedContributionStatModifierPolicy(PolicyId, -4, 5));
    }

    [Fact]
    public void RollingDurations_ReproduceTheConfirmedFourTurnSequence()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = Empty();
        int[] expectedStages = [1, 2, 3, 3];
        int[][] expectedDurations = [[3], [2, 3], [1, 2, 3], [1, 2, 3]];

        for (int turn = 1; turn <= 4; turn++)
        {
            state = Apply(
                service,
                state,
                1,
                duration: 3,
                activeBoundarySequence: turn).After;
            state = Tick(service, state, OwnerTurn, turn).After;

            Assert.Equal(expectedStages[turn - 1], Stage(state));
            Assert.Equal(expectedDurations[turn - 1], Durations(state));
        }

        Assert.Equal([2L, 3L, 4L], Contributions(state).Select(contribution => contribution.Sequence));
    }

    [Fact]
    public void MultipleApplicationsBeforeExpiry_CanReachPositiveAndNegativeCaps()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot positive = Empty();
        RuntimeStatModifierStateSnapshot negative = Empty();

        for (int index = 0; index < 4; index++)
        {
            positive = Apply(service, positive, 1, duration: 3, activeBoundarySequence: 1).After;
            negative = Apply(service, negative, -1, duration: 3, activeBoundarySequence: 1).After;
        }

        Assert.Equal(4, Stage(positive));
        Assert.Equal(-4, Stage(negative));
        Assert.Equal(4, Contributions(positive).Count);
        Assert.Equal(4, Contributions(negative).Count);
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(-1, -4)]
    public void SameDirectionApplicationAtCap_RefreshesOldestWithoutAddingHiddenState(
        int sign,
        int expectedStage)
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = State(Track(
            Attack,
            expectedStage,
            Contribution(1, sign, 1, 1),
            Contribution(2, sign, 2, 1),
            Contribution(3, sign, 3, 1),
            Contribution(4, sign, 4, 1)));

        StatModifierTransitionResult result = Apply(
            service,
            state,
            sign * 2,
            duration: 5,
            activeBoundarySequence: 10);

        Assert.Equal(expectedStage, Stage(result.After));
        Assert.Equal(4, Contributions(result.After).Count);
        RuntimeStatModifierContributionSnapshot oldest = Contributions(result.After)[0];
        Assert.Equal(sign, oldest.StageDelta);
        Assert.Equal(5, Assert.IsType<TurnDurationDefinition>(oldest.Duration).Value);
        Assert.Equal(10, oldest.LastLifecycleBoundary!.Sequence);
        Assert.Equal([1L, 2L, 3L, 4L], Contributions(result.After).Select(value => value.Sequence));
    }

    [Fact]
    public void MultiStageApplication_IsOneContributionWithOneTimer()
    {
        StatModifierTransitionResult result = Apply(
            Service(),
            Empty(),
            2,
            duration: 3,
            activeBoundarySequence: 1);

        RuntimeStatModifierContributionSnapshot contribution = Assert.Single(Contributions(result.After));
        Assert.Equal(2, Stage(result.After));
        Assert.Equal(2, contribution.StageDelta);
        Assert.Equal(3, Assert.IsType<TurnDurationDefinition>(contribution.Duration).Value);
    }

    [Fact]
    public void OppositeContributions_CoexistAndExpiryRevealsTheRemainingSide()
    {
        RuntimeStatModifierStateSnapshot state = State(Track(
            Attack,
            2,
            Contribution(1, 3, 3, 1),
            Contribution(2, -1, 1, 1)));

        StatModifierTransitionResult result = Tick(Service(), state, OwnerTurn, 2);

        Assert.Equal(3, Stage(result.After));
        RuntimeStatModifierContributionSnapshot remaining = Assert.Single(Contributions(result.After));
        Assert.Equal(3, remaining.StageDelta);
        Assert.Equal(2, Assert.IsType<TurnDurationDefinition>(remaining.Duration).Value);
        Assert.Contains(result.Events, @event =>
            @event.Kind == StatModifierEventKind.ContributionExpired &&
            @event.ContributionSequence == 2);
    }

    [Fact]
    public void AggregateClamping_DoesNotDiscardVisibleTimedContributions()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = Apply(service, Empty(), 3, 3, 1).After;

        state = Apply(service, state, 2, 3, 1).After;

        Assert.Equal(4, Stage(state));
        Assert.Equal([3, 2], Contributions(state).Select(contribution => contribution.StageDelta));
    }

    [Fact]
    public void NeutralAggregate_RetainsContributionsUntilTheirIndependentExpiry()
    {
        RuntimeStatModifierStateSnapshot state = State(Track(
            Attack,
            0,
            Contribution(1, 1, 3, 1),
            Contribution(2, -1, 1, 1)));

        StatModifierValidationResult validation = Service().ValidateState(state);
        StatModifierTransitionResult ticked = Tick(Service(), state, OwnerTurn, 2);

        Assert.True(validation.IsValid);
        Assert.Equal(1, Stage(ticked.After));
    }

    [Fact]
    public void Tick_UsesEachContributionsOwnClockAndDuration()
    {
        RuntimeStatModifierStateSnapshot state = State(Track(
            Attack,
            2,
            Contribution(1, 1, 3, 1, eventId: OwnerTurn),
            Contribution(2, 1, 3, 1, eventId: TeamPhase)));

        StatModifierTransitionResult ownerTick = Tick(Service(), state, OwnerTurn, 2);

        Assert.Equal([2, 3], Durations(ownerTick.After));
        Assert.Equal([2L, 1L], Contributions(ownerTick.After)
            .OrderBy(contribution => Assert.IsType<TurnDurationDefinition>(contribution.Duration).Value)
            .Select(contribution => contribution.LastLifecycleBoundary!.Sequence));
    }

    [Fact]
    public void Tick_HonorsReserveSuspensionPerContribution()
    {
        RuntimeStatModifierStateSnapshot state = State(Track(
            Attack,
            2,
            Contribution(1, 1, 3, 1, suspendWhileReserve: true),
            Contribution(2, 1, 3, 1, suspendWhileReserve: false)));

        StatModifierTransitionResult result = Tick(
            Service(),
            state,
            OwnerTurn,
            2,
            isActorDeployed: false);

        Assert.Equal([3, 2], Contributions(result.After)
            .Select(contribution => Assert.IsType<TurnDurationDefinition>(contribution.Duration).Value));
        Assert.All(Contributions(result.After), contribution =>
            Assert.Equal(2, contribution.LastLifecycleBoundary!.Sequence));
    }

    [Fact]
    public void Tick_IsIdempotentAndRejectsOutOfOrderBoundaryAtomically()
    {
        RuntimeStatModifierStateSnapshot state = State(Track(
            Attack,
            2,
            Contribution(1, 1, 3, 5),
            Contribution(2, 1, 3, 5)));
        var service = Service();

        StatModifierTransitionResult duplicate = Tick(service, state, OwnerTurn, 5);
        StatModifierTransitionResult stale = Tick(service, state, OwnerTurn, 4);

        Assert.Equal(StatModifierTransitionCode.Unchanged, duplicate.Code);
        Assert.Same(state, duplicate.After);
        Assert.Equal(StatModifierTransitionCode.Rejected, stale.Code);
        Assert.Equal(StatModifierDiagnosticCode.InvalidLifecycleBoundary, Assert.Single(stale.Diagnostics).Code);
        Assert.Same(state, stale.After);
    }

    [Fact]
    public void ApplicationBoundary_IsProtectedUntilTheNextMatchingCompletion()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = Apply(service, Empty(), 1, 3, 8).After;

        StatModifierTransitionResult same = Tick(service, state, OwnerTurn, 8);
        StatModifierTransitionResult next = Tick(service, state, OwnerTurn, 9);

        Assert.Equal(3, Duration(same.After, sequence: 1));
        Assert.Equal(2, Duration(next.After, sequence: 1));
    }

    [Fact]
    public void Remove_OperatesOnContributionSignsTracksSequencesAndAllState()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = State(
            Track(
                Attack,
                1,
                Contribution(1, 2, 3, 1),
                Contribution(2, -1, 3, 1)),
            Track(Defense, -1, Contribution(3, -1, 3, 1)));

        StatModifierTransitionResult positive = service.Remove(
            new StatModifierRemovalRequest(state, StatModifierRemovalMode.Positive));
        Assert.Equal(-1, positive.After.Tracks.Single(track => track.ModifierTrackId == Attack).ResolvedStage);

        StatModifierTransitionResult negative = service.Remove(
            new StatModifierRemovalRequest(state, StatModifierRemovalMode.Negative));
        Assert.Equal([Attack], negative.After.Tracks.Select(track => track.ModifierTrackId));
        Assert.Equal(2, Stage(negative.After));

        StatModifierTransitionResult selectedTrack = service.Remove(
            new StatModifierRemovalRequest(
                state,
                StatModifierRemovalMode.SelectedTracks,
                modifierTrackIds: [Defense]));
        Assert.Equal([Attack], selectedTrack.After.Tracks.Select(track => track.ModifierTrackId));

        StatModifierTransitionResult selectedContribution = service.Remove(
            new StatModifierRemovalRequest(
                state,
                StatModifierRemovalMode.SelectedContributions,
                contributionSequences: [1]));
        Assert.Equal(-1, selectedContribution.After.Tracks
            .Single(track => track.ModifierTrackId == Attack).ResolvedStage);

        Assert.Empty(service.Remove(
            new StatModifierRemovalRequest(state, StatModifierRemovalMode.All)).After.Tracks);
    }

    [Theory]
    [InlineData(StatModifierCleanupScope.Swap, false)]
    [InlineData(StatModifierCleanupScope.ActorDeparture, true)]
    [InlineData(StatModifierCleanupScope.EncounterEnd, true)]
    [InlineData(StatModifierCleanupScope.FieldTransition, true)]
    [InlineData(StatModifierCleanupScope.RecoveryEvent, true)]
    public void Cleanup_UsesSharedScopeRules(StatModifierCleanupScope scope, bool expectedCleared)
    {
        RuntimeStatModifierStateSnapshot state = State(Track(
            Attack,
            1,
            Contribution(1, 1, 3, 1)));

        StatModifierTransitionResult result = Service().Cleanup(
            new StatModifierCleanupRequest(state, scope));

        Assert.Equal(expectedCleared, result.After.Tracks.Count == 0);
    }

    [Fact]
    public void Apply_RejectsZeroOutOfBoundsMissingDurationAndSequenceExhaustion()
    {
        var service = Service();
        StatModifierTransitionResult zero = service.Apply(
            new StatModifierApplicationRequest(Empty(), Attack, 0, Turns(3)));
        StatModifierTransitionResult outOfBounds = Apply(service, Empty(), 5, 3);
        StatModifierTransitionResult missing = service.Apply(
            new StatModifierApplicationRequest(Empty(), Attack, 1));
        RuntimeStatModifierStateSnapshot exhausted = State(Track(
            Defense,
            1,
            Contribution(long.MaxValue, 1, 3, 1)));
        StatModifierTransitionResult sequence = Apply(service, exhausted, 1, 3);

        Assert.Equal(StatModifierDiagnosticCode.InvalidStageDelta, Assert.Single(zero.Diagnostics).Code);
        Assert.Equal(StatModifierDiagnosticCode.InvalidStageDelta, Assert.Single(outOfBounds.Diagnostics).Code);
        Assert.Equal(StatModifierDiagnosticCode.InvalidDuration, Assert.Single(missing.Diagnostics).Code);
        Assert.Equal(StatModifierDiagnosticCode.NumericOverflow, Assert.Single(sequence.Diagnostics).Code);
        Assert.All([zero, outOfBounds, missing, sequence], result => Assert.Same(result.Before, result.After));
    }

    [Fact]
    public void ValidateState_RejectsOutOfBoundsMissingDurationAndWrongAggregate()
    {
        var service = Service();
        var state = new RuntimeStatModifierStateSnapshot(
            PolicyId,
            [
                Track(Attack, 4, new RuntimeStatModifierContributionSnapshot(1, 5, Turns(3))),
                Track(Defense, 2, new RuntimeStatModifierContributionSnapshot(2, 1))
            ]);

        StatModifierValidationResult result = service.ValidateState(state);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Diagnostics.Count(diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.IncompatibleState));
    }

    [Fact]
    public void AssessmentAndExecution_ReturnEquivalentImmutableState()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = State(Track(
            Attack,
            1,
            Contribution(1, 1, 2, 1)));
        StatModifierApplicationRequest request = Request(state, -1, 3, 2);

        StatModifierTransitionResult assessment = service.AssessApplication(request);
        StatModifierTransitionResult execution = service.Apply(request);

        Assert.Equal(assessment.Code, execution.Code);
        Assert.Equal(Shape(assessment.After), Shape(execution.After));
        Assert.Equal("attack:1:1/1/2/owner_turn_completed/1", Shape(state));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeStatModifierContributionSnapshot>)assessment.After.Tracks[0].Contributions).Clear());
    }

    [Fact]
    public void CrossTrackStaleApplicationBoundary_IsRejectedWithoutMutation()
    {
        RuntimeStatModifierStateSnapshot state = State(
            Track(Attack, 1, Contribution(1, 1, 3, 4)),
            Track(Defense, 1, Contribution(2, 1, 3, 5)));

        StatModifierTransitionResult result = Apply(
            Service(),
            state,
            1,
            duration: 3,
            activeBoundarySequence: 4);

        Assert.Equal(StatModifierTransitionCode.Rejected, result.Code);
        Assert.Equal(StatModifierDiagnosticCode.InvalidLifecycleBoundary, Assert.Single(result.Diagnostics).Code);
        Assert.Same(state, result.After);
    }

    private static StatModifierPolicyService Service() =>
        new(new TimedContributionStatModifierPolicy(PolicyId));

    private static StatModifierTransitionResult Apply(
        StatModifierPolicyService service,
        RuntimeStatModifierStateSnapshot state,
        int stageDelta,
        int duration,
        long? activeBoundarySequence = null) =>
        service.Apply(Request(state, stageDelta, duration, activeBoundarySequence));

    private static StatModifierApplicationRequest Request(
        RuntimeStatModifierStateSnapshot state,
        int stageDelta,
        int duration,
        long? activeBoundarySequence = null) =>
        new(
            state,
            Attack,
            stageDelta,
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

    private static RuntimeStatModifierTrackSnapshot Track(
        ContentId trackId,
        int resolvedStage,
        params RuntimeStatModifierContributionSnapshot[] contributions) =>
        new(trackId, resolvedStage, contributions);

    private static RuntimeStatModifierContributionSnapshot Contribution(
        long sequence,
        int stageDelta,
        int duration,
        long? boundarySequence,
        bool suspendWhileReserve = false,
        ContentId? eventId = null) =>
        new(
            sequence,
            stageDelta,
            new TurnDurationDefinition(duration, eventId ?? OwnerTurn, suspendWhileReserve),
            boundarySequence is long value
                ? new StatModifierLifecycleBoundary(eventId ?? OwnerTurn, value)
                : null);

    private static TurnDurationDefinition Turns(int duration) =>
        new(duration, OwnerTurn, false);

    private static int Stage(RuntimeStatModifierStateSnapshot state) =>
        Assert.Single(state.Tracks).ResolvedStage;

    private static IReadOnlyList<RuntimeStatModifierContributionSnapshot> Contributions(
        RuntimeStatModifierStateSnapshot state) =>
        Assert.Single(state.Tracks).Contributions;

    private static int[] Durations(RuntimeStatModifierStateSnapshot state) =>
        Contributions(state)
            .Select(contribution => Assert.IsType<TurnDurationDefinition>(contribution.Duration).Value)
            .ToArray();

    private static int Duration(RuntimeStatModifierStateSnapshot state, long sequence) =>
        Assert.IsType<TurnDurationDefinition>(Contributions(state)
            .Single(contribution => contribution.Sequence == sequence).Duration).Value;

    private static string Shape(RuntimeStatModifierStateSnapshot state) =>
        string.Join('|', state.Tracks.Select(track =>
            $"{track.ModifierTrackId}:{track.ResolvedStage}:" +
            string.Join(',', track.Contributions.Select(contribution =>
            {
                var duration = Assert.IsType<TurnDurationDefinition>(contribution.Duration);
                return $"{contribution.Sequence}/{contribution.StageDelta}/{duration.Value}/" +
                    $"{contribution.LastLifecycleBoundary?.EventId}/" +
                    $"{contribution.LastLifecycleBoundary?.Sequence}";
            }))));
}
