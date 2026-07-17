using Convergence.Content;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class StatModifierPolicyContractTests
{
    private static readonly ContentId PolicyId = ContentId.Parse("test_modifier_policy");
    private static readonly ContentId Attack = ContentId.Parse("attack");
    private static readonly ContentId Defense = ContentId.Parse("defense");
    private static readonly ContentId TurnEnd = ContentId.Parse("owner_turn_end");

    [Fact]
    public void Snapshots_DefensivelyCopyAndOrderTracksAndContributions()
    {
        var contributions = new List<RuntimeStatModifierContributionSnapshot>
        {
            new(2, 1),
            new(1, 1)
        };
        var tracks = new List<RuntimeStatModifierTrackSnapshot>
        {
            new(Defense, 1, [new RuntimeStatModifierContributionSnapshot(3, 1)]),
            new(Attack, 2, contributions)
        };

        var state = new RuntimeStatModifierStateSnapshot(PolicyId, tracks);
        contributions.Clear();
        tracks.Clear();

        Assert.Equal([Attack, Defense], state.Tracks.Select(track => track.ModifierTrackId));
        Assert.Equal([1L, 2L], state.Tracks[0].Contributions.Select(contribution => contribution.Sequence));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeStatModifierTrackSnapshot>)state.Tracks).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeStatModifierContributionSnapshot>)state.Tracks[0].Contributions).Clear());
    }

    [Fact]
    public void Service_RejectsPolicyMismatchBeforeCallingExtension()
    {
        var policy = new ScriptedPolicy();
        var service = new StatModifierPolicyService(policy);
        RuntimeStatModifierStateSnapshot state = Empty(ContentId.Parse("other_policy"));

        StatModifierTransitionResult result = service.Apply(
            new StatModifierApplicationRequest(state, Attack, 1));

        Assert.Equal(StatModifierTransitionCode.Rejected, result.Code);
        Assert.Same(state, result.Before);
        Assert.Same(state, result.After);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.PolicyMismatch);
        Assert.Equal(0, policy.ApplyCalls);
    }

    [Fact]
    public void Service_RejectsInvalidNeutralStateWithoutCallingExtension()
    {
        var policy = new ScriptedPolicy();
        var service = new StatModifierPolicyService(policy);
        var invalidDuration = new TurnDurationDefinition(0, default, false);
        var state = new RuntimeStatModifierStateSnapshot(
            PolicyId,
            [
                new RuntimeStatModifierTrackSnapshot(
                    Attack,
                    1,
                    [
                        new RuntimeStatModifierContributionSnapshot(1, 0, invalidDuration),
                        new RuntimeStatModifierContributionSnapshot(1, int.MaxValue)
                    ]),
                new RuntimeStatModifierTrackSnapshot(
                    Attack,
                    1,
                    [new RuntimeStatModifierContributionSnapshot(-2, int.MaxValue)])
            ]);

        StatModifierValidationResult result = service.ValidateState(state);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.DuplicateModifierTrack);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.DuplicateContributionSequence);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.InvalidContributionSequence);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.InvalidStageDelta);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.InvalidDuration);
        Assert.Equal(0, policy.ValidationCalls);
    }

    [Fact]
    public void ApplicationValidation_RejectsInvalidRequestWithoutCallingExtension()
    {
        var policy = new ScriptedPolicy();
        var service = new StatModifierPolicyService(policy);
        RuntimeStatModifierStateSnapshot state = Empty();

        StatModifierTransitionResult result = service.Apply(
            new StatModifierApplicationRequest(
                state,
                default,
                0,
                new TurnDurationDefinition(0, default, false)));

        Assert.Equal(StatModifierTransitionCode.Rejected, result.Code);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.InvalidModifierTrackId);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.InvalidStageDelta);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.InvalidDuration);
        Assert.Equal(0, policy.ApplyCalls);
    }

    [Fact]
    public void AssessAndApply_DeriveIdenticalOrderedEventsWithoutMutatingBefore()
    {
        RuntimeStatModifierStateSnapshot before = State(
            new RuntimeStatModifierTrackSnapshot(
                Attack,
                1,
                [new RuntimeStatModifierContributionSnapshot(1, 1, Turns(2))]));
        RuntimeStatModifierStateSnapshot after = State(
            new RuntimeStatModifierTrackSnapshot(
                Attack,
                2,
                [
                    new RuntimeStatModifierContributionSnapshot(1, 1, Turns(2)),
                    new RuntimeStatModifierContributionSnapshot(2, 1, Turns(3))
                ]));
        var policy = new ScriptedPolicy
        {
            ApplyDecision = _ => StatModifierPolicyDecision.Accept(after)
        };
        var service = new StatModifierPolicyService(policy);
        var request = new StatModifierApplicationRequest(before, Attack, 1, Turns(3));

        StatModifierTransitionResult assessment = service.AssessApplication(request);
        StatModifierTransitionResult execution = service.Apply(request);

        Assert.Equal(StatModifierTransitionCode.Applied, assessment.Code);
        Assert.True(assessment.StateChanged);
        Assert.Same(before, assessment.Before);
        Assert.Same(after, assessment.After);
        Assert.Equal(
            [StatModifierEventKind.ContributionAdded, StatModifierEventKind.AggregateStageChanged],
            assessment.Events.Select(@event => @event.Kind));
        Assert.Equal(
            assessment.Events.Select(EventShape),
            execution.Events.Select(EventShape));
        Assert.Equal(1, before.Tracks[0].ResolvedStage);
        Assert.Single(before.Tracks[0].Contributions);
        Assert.Equal(2, policy.ApplyCalls);
    }

    [Fact]
    public void AcceptedEquivalentState_IsUnchangedAndPublishesNoEvents()
    {
        RuntimeStatModifierStateSnapshot state = State(
            new RuntimeStatModifierTrackSnapshot(
                Attack,
                1,
                [new RuntimeStatModifierContributionSnapshot(1, 1)]));
        RuntimeStatModifierStateSnapshot equivalent = State(
            new RuntimeStatModifierTrackSnapshot(
                Attack,
                1,
                [new RuntimeStatModifierContributionSnapshot(1, 1)]));
        var service = new StatModifierPolicyService(new ScriptedPolicy
        {
            ApplyDecision = _ => StatModifierPolicyDecision.Accept(equivalent)
        });

        StatModifierTransitionResult result = service.Apply(
            new StatModifierApplicationRequest(state, Attack, 1));

        Assert.Equal(StatModifierTransitionCode.Unchanged, result.Code);
        Assert.True(result.Accepted);
        Assert.False(result.StateChanged);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void PolicyRejection_PreservesBeforeAndProvidesStableDiagnostic()
    {
        RuntimeStatModifierStateSnapshot state = Empty();
        var service = new StatModifierPolicyService(new ScriptedPolicy
        {
            ApplyDecision = _ => StatModifierPolicyDecision.Reject(
                new StatModifierDiagnostic(
                    StatModifierDiagnosticCode.PolicyRejected,
                    "Rejected by test policy.",
                    Attack))
        });

        StatModifierTransitionResult result = service.Apply(
            new StatModifierApplicationRequest(state, Attack, 1));

        Assert.Equal(StatModifierTransitionCode.Rejected, result.Code);
        Assert.False(result.Accepted);
        Assert.Same(state, result.Before);
        Assert.Same(state, result.After);
        Assert.Equal(StatModifierDiagnosticCode.PolicyRejected, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void InvalidPolicyOutput_IsRejectedWithoutExposingAfterState()
    {
        RuntimeStatModifierStateSnapshot before = Empty();
        RuntimeStatModifierStateSnapshot incompatible = Empty(ContentId.Parse("wrong_policy"));
        var service = new StatModifierPolicyService(new ScriptedPolicy
        {
            ApplyDecision = _ => StatModifierPolicyDecision.Accept(incompatible)
        });

        StatModifierTransitionResult result = service.Apply(
            new StatModifierApplicationRequest(before, Attack, 1));

        Assert.Equal(StatModifierTransitionCode.Rejected, result.Code);
        Assert.Same(before, result.After);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.InvalidPolicyResult);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == StatModifierDiagnosticCode.PolicyMismatch);
    }

    [Fact]
    public void PolicyFault_IsContainedByTypedBoundary()
    {
        RuntimeStatModifierStateSnapshot before = Empty();
        var service = new StatModifierPolicyService(new ScriptedPolicy
        {
            ApplyDecision = _ => throw new InvalidOperationException("test fault")
        });

        StatModifierTransitionResult result = service.Apply(
            new StatModifierApplicationRequest(before, Attack, 1));

        Assert.Equal(StatModifierTransitionCode.Rejected, result.Code);
        Assert.Same(before, result.After);
        StatModifierDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(StatModifierDiagnosticCode.PolicyFaulted, diagnostic.Code);
        Assert.Contains("InvalidOperationException", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TickRemovalAndCleanup_DispatchToDistinctPolicyOperations()
    {
        RuntimeStatModifierStateSnapshot state = Empty();
        var policy = new ScriptedPolicy();
        var service = new StatModifierPolicyService(policy);

        StatModifierTransitionResult tick = service.Tick(
            new StatModifierTickRequest(
                state,
                new StatModifierLifecycleBoundary(TurnEnd, 1),
                true));
        StatModifierTransitionResult removal = service.Remove(
            new StatModifierRemovalRequest(state, StatModifierRemovalMode.All));
        StatModifierTransitionResult cleanup = service.Cleanup(
            new StatModifierCleanupRequest(state, StatModifierCleanupScope.EncounterEnd));

        Assert.Equal(StatModifierOperationKind.Tick, tick.Operation);
        Assert.Equal(StatModifierOperationKind.Removal, removal.Operation);
        Assert.Equal(StatModifierOperationKind.Cleanup, cleanup.Operation);
        Assert.Equal(1, policy.TickCalls);
        Assert.Equal(1, policy.RemoveCalls);
        Assert.Equal(1, policy.CleanupCalls);
    }

    [Fact]
    public void RemovalValidation_RejectsMissingOrExtraneousSelectors()
    {
        var policy = new ScriptedPolicy();
        var service = new StatModifierPolicyService(policy);
        RuntimeStatModifierStateSnapshot state = Empty();

        StatModifierTransitionResult missing = service.Remove(
            new StatModifierRemovalRequest(state, StatModifierRemovalMode.SelectedTracks));
        StatModifierTransitionResult extraneous = service.Remove(
            new StatModifierRemovalRequest(
                state,
                StatModifierRemovalMode.All,
                modifierTrackIds: [Attack]));

        Assert.Equal(StatModifierTransitionCode.Rejected, missing.Code);
        Assert.Equal(StatModifierTransitionCode.Rejected, extraneous.Code);
        Assert.All(
            missing.Diagnostics.Concat(extraneous.Diagnostics),
            diagnostic => Assert.Equal(StatModifierDiagnosticCode.InvalidRemovalRequest, diagnostic.Code));
        Assert.Equal(0, policy.RemoveCalls);
    }

    [Fact]
    public void ResultCollections_AreImmutableSnapshots()
    {
        var diagnostics = new List<StatModifierDiagnostic>
        {
            new(StatModifierDiagnosticCode.PolicyRejected, "test")
        };
        var events = new List<StatModifierEvent>
        {
            new(StatModifierEventKind.AggregateStageChanged, Attack, 0, 1)
        };
        RuntimeStatModifierStateSnapshot state = Empty();
        var result = new StatModifierTransitionResult(
            StatModifierOperationKind.Application,
            StatModifierTransitionCode.Rejected,
            state,
            state,
            diagnostics,
            events);

        diagnostics.Clear();
        events.Clear();

        Assert.Single(result.Diagnostics);
        Assert.Single(result.Events);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<StatModifierDiagnostic>)result.Diagnostics).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<StatModifierEvent>)result.Events).Clear());
    }

    private static RuntimeStatModifierStateSnapshot Empty(ContentId? policyId = null) =>
        new(policyId ?? PolicyId);

    private static RuntimeStatModifierStateSnapshot State(
        params RuntimeStatModifierTrackSnapshot[] tracks) =>
        new(PolicyId, tracks);

    private static TurnDurationDefinition Turns(int value) =>
        new(value, TurnEnd, false);

    private static string EventShape(StatModifierEvent @event) =>
        $"{@event.Kind}:{@event.ModifierTrackId}:{@event.PreviousStage}:{@event.CurrentStage}:" +
        $"{@event.ContributionSequence}:{@event.StageDelta}";

    private sealed class ScriptedPolicy : IStatModifierPolicy
    {
        public Func<StatModifierApplicationRequest, StatModifierPolicyDecision>? ApplyDecision { get; init; }

        public ContentId PolicyId => StatModifierPolicyContractTests.PolicyId;
        public int ValidationCalls { get; private set; }
        public int ApplyCalls { get; private set; }
        public int TickCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public int CleanupCalls { get; private set; }

        public StatModifierValidationResult ValidateState(RuntimeStatModifierStateSnapshot state)
        {
            ValidationCalls++;
            return StatModifierValidationResult.Valid;
        }

        public StatModifierPolicyDecision Apply(StatModifierApplicationRequest request)
        {
            ApplyCalls++;
            return ApplyDecision?.Invoke(request) ?? StatModifierPolicyDecision.Accept(request.State);
        }

        public StatModifierPolicyDecision Tick(StatModifierTickRequest request)
        {
            TickCalls++;
            return StatModifierPolicyDecision.Accept(request.State);
        }

        public StatModifierPolicyDecision Remove(StatModifierRemovalRequest request)
        {
            RemoveCalls++;
            return StatModifierPolicyDecision.Accept(request.State);
        }

        public StatModifierPolicyDecision Cleanup(StatModifierCleanupRequest request)
        {
            CleanupCalls++;
            return StatModifierPolicyDecision.Accept(request.State);
        }
    }
}
