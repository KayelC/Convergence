using Convergence.Content;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class PersistentStagedStatModifierPolicyTests
{
    private static readonly ContentId PolicyId = ContentId.Parse("persistent_staged_test");
    private static readonly ContentId Attack = ContentId.Parse("attack");
    private static readonly ContentId Defense = ContentId.Parse("defense");
    private static readonly ContentId TurnEnd = ContentId.Parse("owner_turn_end");

    [Fact]
    public void Constructor_RequiresValidIdentityAndSignedBounds()
    {
        Assert.Throws<ArgumentException>(() => new PersistentStagedStatModifierPolicy(default));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PersistentStagedStatModifierPolicy(PolicyId, 0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PersistentStagedStatModifierPolicy(PolicyId, -4, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PersistentStagedStatModifierPolicy(PolicyId, -5, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PersistentStagedStatModifierPolicy(PolicyId, -4, 5));
    }

    [Fact]
    public void Apply_WalksEveryReferenceStageAndRemovesTrackAtZero()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = Empty();

        for (int expected = 1; expected <= 4; expected++)
        {
            StatModifierTransitionResult result = Apply(service, state, Attack, 1);
            Assert.Equal(StatModifierTransitionCode.Applied, result.Code);
            Assert.Equal(expected, Assert.Single(result.After.Tracks).ResolvedStage);
            state = result.After;
        }

        for (int expected = 3; expected >= -4; expected--)
        {
            StatModifierTransitionResult result = Apply(service, state, Attack, -1);
            Assert.Equal(StatModifierTransitionCode.Applied, result.Code);
            if (expected == 0)
            {
                Assert.Empty(result.After.Tracks);
            }
            else
            {
                Assert.Equal(expected, Assert.Single(result.After.Tracks).ResolvedStage);
            }

            state = result.After;
        }
    }

    [Theory]
    [InlineData(3, int.MaxValue, 4)]
    [InlineData(-3, int.MinValue, -4)]
    [InlineData(4, 1, 4)]
    [InlineData(-4, -1, -4)]
    public void Apply_ClampsSafelyAndReportsCapAsUnchanged(int current, int delta, int expected)
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = State(Track(Attack, current, 1));

        StatModifierTransitionResult result = Apply(service, state, Attack, delta);

        Assert.Equal(expected == current
            ? StatModifierTransitionCode.Unchanged
            : StatModifierTransitionCode.Applied, result.Code);
        Assert.Equal(expected, Assert.Single(result.After.Tracks).ResolvedStage);
        Assert.Equal(expected != current, result.StateChanged);
    }

    [Fact]
    public void Apply_UsesOneStableNetContributionPerTrackAndDeterministicSequences()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = Apply(service, Empty(), Defense, -2).After;
        state = Apply(service, state, Attack, 3).After;
        long attackSequence = state.Tracks.Single(track => track.ModifierTrackId == Attack)
            .Contributions[0].Sequence;

        StatModifierTransitionResult changed = Apply(service, state, Attack, -1);

        RuntimeStatModifierTrackSnapshot attack = changed.After.Tracks
            .Single(track => track.ModifierTrackId == Attack);
        Assert.Equal(2, attack.ResolvedStage);
        Assert.Equal(attackSequence, Assert.Single(attack.Contributions).Sequence);
        Assert.Equal(2, attack.Contributions[0].StageDelta);
        Assert.Equal([1L, 2L], changed.After.Tracks
            .SelectMany(track => track.Contributions)
            .Select(contribution => contribution.Sequence)
            .Order());
    }

    [Fact]
    public void Apply_DoesNotRetainAuthoredDurationAndTickNeverExpiresState()
    {
        var service = Service();
        var duration = new TurnDurationDefinition(3, TurnEnd, false);

        StatModifierTransitionResult applied = service.Apply(
            new StatModifierApplicationRequest(Empty(), Attack, 1, duration));
        StatModifierTransitionResult ticked = service.Tick(
            new StatModifierTickRequest(
                applied.After,
                new StatModifierLifecycleBoundary(TurnEnd, 1),
                true));

        Assert.Null(Assert.Single(Assert.Single(applied.After.Tracks).Contributions).Duration);
        Assert.Equal(StatModifierTransitionCode.Unchanged, ticked.Code);
        Assert.Same(applied.After, ticked.After);
    }

    [Fact]
    public void Remove_SupportsSignTrackContributionAndAllSelectors()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = State(
            Track(Attack, 2, 1),
            Track(Defense, -2, 2));

        StatModifierTransitionResult positive = service.Remove(
            new StatModifierRemovalRequest(state, StatModifierRemovalMode.Positive));
        Assert.Equal([Defense], positive.After.Tracks.Select(track => track.ModifierTrackId));

        StatModifierTransitionResult negative = service.Remove(
            new StatModifierRemovalRequest(state, StatModifierRemovalMode.Negative));
        Assert.Equal([Attack], negative.After.Tracks.Select(track => track.ModifierTrackId));

        StatModifierTransitionResult track = service.Remove(
            new StatModifierRemovalRequest(
                state,
                StatModifierRemovalMode.SelectedTracks,
                modifierTrackIds: [Defense]));
        Assert.Equal([Attack], track.After.Tracks.Select(candidate => candidate.ModifierTrackId));

        StatModifierTransitionResult contribution = service.Remove(
            new StatModifierRemovalRequest(
                state,
                StatModifierRemovalMode.SelectedContributions,
                contributionSequences: [1]));
        Assert.Equal([Defense], contribution.After.Tracks.Select(candidate => candidate.ModifierTrackId));

        Assert.Empty(service.Remove(
            new StatModifierRemovalRequest(state, StatModifierRemovalMode.All)).After.Tracks);
    }

    [Theory]
    [InlineData(StatModifierCleanupScope.Swap, false)]
    [InlineData(StatModifierCleanupScope.ActorDeparture, true)]
    [InlineData(StatModifierCleanupScope.EncounterEnd, true)]
    [InlineData(StatModifierCleanupScope.FieldTransition, true)]
    public void Cleanup_PreservesSwapAndClearsDepartureOrEncounterState(
        StatModifierCleanupScope scope,
        bool expectedCleared)
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = State(Track(Attack, 2, 1));

        StatModifierTransitionResult result = service.Cleanup(
            new StatModifierCleanupRequest(state, scope));

        Assert.Equal(expectedCleared, result.After.Tracks.Count == 0);
        Assert.Equal(expectedCleared
            ? StatModifierTransitionCode.Applied
            : StatModifierTransitionCode.Unchanged, result.Code);
    }

    [Fact]
    public void ValidateState_RejectsNoncanonicalPersistentShapes()
    {
        var service = Service();
        var invalid = new RuntimeStatModifierStateSnapshot(
            PolicyId,
            [
                new RuntimeStatModifierTrackSnapshot(
                    Attack,
                    4,
                    [
                        new RuntimeStatModifierContributionSnapshot(1, 2),
                        new RuntimeStatModifierContributionSnapshot(2, 2)
                    ]),
                new RuntimeStatModifierTrackSnapshot(
                    Defense,
                    -5,
                    [new RuntimeStatModifierContributionSnapshot(
                        3,
                        -4,
                        new BattleDurationDefinition())])
            ]);

        StatModifierValidationResult result = service.ValidateState(invalid);

        Assert.False(result.IsValid);
        Assert.Equal(4, result.Diagnostics.Count(diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.IncompatibleState));
    }

    [Fact]
    public void SnapshotReconstruction_PreservesCanonicalState()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot original = State(
            Track(Attack, 3, 7),
            Track(Defense, -2, 11));
        var reconstructed = new RuntimeStatModifierStateSnapshot(
            original.PolicyId,
            original.Tracks.Select(track => new RuntimeStatModifierTrackSnapshot(
                track.ModifierTrackId,
                track.ResolvedStage,
                track.Contributions.Select(contribution =>
                    new RuntimeStatModifierContributionSnapshot(
                        contribution.Sequence,
                        contribution.StageDelta,
                        contribution.Duration,
                        contribution.LastLifecycleBoundary)))));

        Assert.True(service.ValidateState(reconstructed).IsValid);
        Assert.Equal(
            original.Tracks.Select(Shape),
            reconstructed.Tracks.Select(Shape));
    }

    [Fact]
    public void SequenceExhaustion_IsRejectedWithoutChangingState()
    {
        var service = Service();
        RuntimeStatModifierStateSnapshot state = State(Track(Attack, 1, long.MaxValue));

        StatModifierTransitionResult result = Apply(service, state, Defense, 1);

        Assert.Equal(StatModifierTransitionCode.Rejected, result.Code);
        Assert.Same(state, result.After);
        Assert.Equal(StatModifierDiagnosticCode.NumericOverflow, Assert.Single(result.Diagnostics).Code);
    }

    private static StatModifierPolicyService Service() =>
        new(new PersistentStagedStatModifierPolicy(PolicyId));

    private static StatModifierTransitionResult Apply(
        StatModifierPolicyService service,
        RuntimeStatModifierStateSnapshot state,
        ContentId trackId,
        int delta) =>
        service.Apply(new StatModifierApplicationRequest(state, trackId, delta));

    private static RuntimeStatModifierStateSnapshot Empty() => new(PolicyId);

    private static RuntimeStatModifierStateSnapshot State(
        params RuntimeStatModifierTrackSnapshot[] tracks) =>
        new(PolicyId, tracks);

    private static RuntimeStatModifierTrackSnapshot Track(
        ContentId trackId,
        int stage,
        long sequence) =>
        new(
            trackId,
            stage,
            [new RuntimeStatModifierContributionSnapshot(sequence, stage)]);

    private static string Shape(RuntimeStatModifierTrackSnapshot track) =>
        $"{track.ModifierTrackId}:{track.ResolvedStage}:" +
        string.Join(',', track.Contributions.Select(contribution =>
            $"{contribution.Sequence}/{contribution.StageDelta}/{contribution.Duration}/" +
            $"{contribution.LastLifecycleBoundary?.EventId}/" +
            $"{contribution.LastLifecycleBoundary?.Sequence}"));
}
