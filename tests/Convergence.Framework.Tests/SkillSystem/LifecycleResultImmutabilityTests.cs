using Convergence.Content;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class LifecycleResultImmutabilityTests
{
    [Fact]
    public void PassiveTriggerExecutionResult_SnapshotsConstructorAndRecordCloneEffects()
    {
        EffectExecutionResult originalEffect = Effect(0);
        EffectExecutionResult replacementEffect = Effect(1);
        var originalEffects = new List<EffectExecutionResult> { originalEffect };
        var replacementEffects = new List<EffectExecutionResult> { replacementEffect };
        var original = Activation("original", originalEffects);

        PassiveTriggerExecutionResult clone = original with
        {
            Outcome = PassiveTriggerOutcome.ConditionNotMet,
            Effects = replacementEffects
        };

        originalEffects.Clear();
        replacementEffects.Clear();

        Assert.Equal(originalEffect, Assert.Single(original.Effects));
        Assert.Equal(replacementEffect, Assert.Single(clone.Effects));
        Assert.Equal(PassiveTriggerOutcome.ConditionNotMet, clone.Outcome);
        Assert.NotSame(originalEffects, original.Effects);
        Assert.NotSame(replacementEffects, clone.Effects);
        AssertReadOnly(clone.Effects, originalEffect);

        clone.Deconstruct(out _, out _, out _, out _, out _, out IReadOnlyList<EffectExecutionResult> effects);
        Assert.Same(clone.Effects, effects);
    }

    [Fact]
    public void PassiveTriggerDispatchResult_SnapshotsConstructorAndRecordCloneActivations()
    {
        PassiveTriggerExecutionResult originalActivation = Activation("original", []);
        PassiveTriggerExecutionResult replacementActivation = Activation("replacement", []);
        var originalActivations = new List<PassiveTriggerExecutionResult> { originalActivation };
        var replacementActivations = new List<PassiveTriggerExecutionResult> { replacementActivation };
        var original = new PassiveTriggerDispatchResult(originalActivations);

        PassiveTriggerDispatchResult clone = original with { Activations = replacementActivations };

        originalActivations.Clear();
        replacementActivations.Clear();

        Assert.Equal(originalActivation, Assert.Single(original.Activations));
        Assert.Equal(replacementActivation, Assert.Single(clone.Activations));
        Assert.NotSame(originalActivations, original.Activations);
        Assert.NotSame(replacementActivations, clone.Activations);
        AssertReadOnly(clone.Activations, originalActivation);

        clone.Deconstruct(out IReadOnlyList<PassiveTriggerExecutionResult> activations);
        Assert.Same(clone.Activations, activations);
    }

    [Fact]
    public void BattleTurnEndLifecycleResult_SnapshotsConstructorAndRecordCloneCollections()
    {
        BattleStatusLifecycleEvent originalEvent = Event("original");
        BattleStatusLifecycleEvent replacementEvent = Event("replacement");
        PassiveTriggerExecutionResult originalActivation = Activation("original", []);
        PassiveTriggerExecutionResult replacementActivation = Activation("replacement", []);
        var originalEvents = new List<BattleStatusLifecycleEvent> { originalEvent };
        var replacementEvents = new List<BattleStatusLifecycleEvent> { replacementEvent };
        var originalActivations = new List<PassiveTriggerExecutionResult> { originalActivation };
        var replacementActivations = new List<PassiveTriggerExecutionResult> { replacementActivation };
        var original = new BattleTurnEndLifecycleResult(originalEvents, originalActivations);

        BattleTurnEndLifecycleResult clone = original with
        {
            Events = replacementEvents,
            PassiveActivations = replacementActivations
        };

        originalEvents.Clear();
        replacementEvents.Clear();
        originalActivations.Clear();
        replacementActivations.Clear();

        Assert.Equal(originalEvent, Assert.Single(original.Events));
        Assert.Equal(originalActivation, Assert.Single(original.PassiveActivations));
        Assert.Equal(replacementEvent, Assert.Single(clone.Events));
        Assert.Equal(replacementActivation, Assert.Single(clone.PassiveActivations));
        AssertReadOnly(clone.Events, originalEvent);
        AssertReadOnly(clone.PassiveActivations, originalActivation);

        clone.Deconstruct(
            out IReadOnlyList<BattleStatusLifecycleEvent> events,
            out IReadOnlyList<PassiveTriggerExecutionResult> activations);
        Assert.Same(clone.Events, events);
        Assert.Same(clone.PassiveActivations, activations);
    }

    [Fact]
    public void BattleAilmentApplicationResult_SnapshotsConstructorAndRecordCloneEvents()
    {
        BattleStatusLifecycleEvent originalEvent = Event("original");
        BattleStatusLifecycleEvent replacementEvent = Event("replacement");
        var originalEvents = new List<BattleStatusLifecycleEvent> { originalEvent };
        var replacementEvents = new List<BattleStatusLifecycleEvent> { replacementEvent };
        var original = new BattleAilmentApplicationResult(
            BattleAilmentApplicationStatus.Applied,
            originalEvents);

        BattleAilmentApplicationResult clone = original with
        {
            Status = BattleAilmentApplicationStatus.Missed,
            Events = replacementEvents
        };

        originalEvents.Clear();
        replacementEvents.Clear();

        Assert.Equal(originalEvent, Assert.Single(original.Events));
        Assert.True(original.Applied);
        Assert.Equal(replacementEvent, Assert.Single(clone.Events));
        Assert.False(clone.Applied);
        Assert.NotSame(originalEvents, original.Events);
        Assert.NotSame(replacementEvents, clone.Events);
        AssertReadOnly(clone.Events, originalEvent);

        clone.Deconstruct(
            out BattleAilmentApplicationStatus status,
            out IReadOnlyList<BattleStatusLifecycleEvent> events);
        Assert.Equal(BattleAilmentApplicationStatus.Missed, status);
        Assert.Same(clone.Events, events);
    }

    [Fact]
    public void AilmentTransitionContractsSnapshotPolicyInputAndCommittedChanges()
    {
        ContentId candidate = ContentId.Parse("candidate");
        ContentId existing = ContentId.Parse("existing");
        StatusLifetimeDefinition candidateLifetime = Lifetime(3);
        StatusLifetimeDefinition existingLifetime = Lifetime(2);
        var conflicts = new List<BattleAilmentStateSnapshot>
        {
            new(existing, existingLifetime)
        };
        var request = new BattleAilmentTransitionPolicyRequest(
            candidate,
            candidateLifetime,
            existingSameAilment: null,
            conflicts);
        conflicts.Clear();
        var changes = new List<BattleAilmentStateChange>
        {
            new(
                BattleAilmentStateChangeKind.Removed,
                existing,
                existingLifetime,
                after: null,
                StatusRemovalCause.ExclusivityReplacement),
            new(
                BattleAilmentStateChangeKind.Added,
                candidate,
                before: null,
                candidateLifetime)
        };
        var result = new BattleAilmentTransitionResult(
            BattleAilmentTransitionOutcome.Replaced,
            candidate,
            changes);
        changes.Clear();

        Assert.Equal(existing, Assert.Single(request.ExclusiveConflicts).AilmentId);
        Assert.Equal([existing, candidate], result.AffectedAilmentIds);
        Assert.Equal(2, result.StateChanges.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BattleAilmentStateSnapshot>)request.ExclusiveConflicts).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BattleAilmentStateChange>)result.StateChanges).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ContentId>)result.AffectedAilmentIds).Clear());

        Assert.Throws<ArgumentException>(() => new BattleAilmentTransitionResult(
            BattleAilmentTransitionOutcome.Applied,
            candidate,
            [new BattleAilmentStateChange(
                BattleAilmentStateChangeKind.Refreshed,
                candidate,
                existingLifetime,
                candidateLifetime)]));
        Assert.Throws<ArgumentException>(() => new BattleAilmentTransitionResult(
            BattleAilmentTransitionOutcome.Replaced,
            candidate,
            [new BattleAilmentStateChange(
                BattleAilmentStateChangeKind.Added,
                candidate,
                before: null,
                candidateLifetime)]));
    }

    [Fact]
    public void AilmentApplicationGateContractsSnapshotInputAndRejectContradictoryDecisions()
    {
        RuntimeActorState actor = Actor("gate_actor");
        RuntimeActorState target = Actor("gate_target");
        var participants = new List<RuntimeActorState> { actor, target };
        var request = new BattleAilmentApplicationGateRequest(
            actor,
            target,
            Ailment("gate_ailment"),
            participants,
            ContentId.Parse("gate_source"));
        participants.Clear();

        Assert.Equal([actor, target], request.Participants);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeActorState>)request.Participants).Clear());
        Assert.Throws<ArgumentException>(() => new BattleAilmentApplicationGateDecision(
            BattleAilmentApplicationGateOutcome.Allowed,
            BattleAilmentApplicationGateReason.Guarding));
        Assert.Throws<ArgumentException>(() => new BattleAilmentApplicationGateDecision(
            BattleAilmentApplicationGateOutcome.Blocked));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleAilmentApplicationGateDecision(
            (BattleAilmentApplicationGateOutcome)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleAilmentApplicationGateDecision(
            BattleAilmentApplicationGateOutcome.Blocked,
            (BattleAilmentApplicationGateReason)int.MaxValue));
    }

    [Fact]
    public void LifecycleResultRecordClones_NormalizeNullCollectionsToImmutableEmptySnapshots()
    {
        PassiveTriggerExecutionResult activation = Activation("null", []) with { Effects = null! };
        PassiveTriggerDispatchResult dispatch = PassiveTriggerDispatchResult.Empty with { Activations = null! };
        var turnEnd = new BattleTurnEndLifecycleResult([], []) with
        {
            Events = null!,
            PassiveActivations = null!
        };
        var ailment = new BattleAilmentApplicationResult(BattleAilmentApplicationStatus.Missed, []) with
        {
            Events = null!
        };

        Assert.Empty(activation.Effects);
        Assert.Empty(dispatch.Activations);
        Assert.Empty(turnEnd.Events);
        Assert.Empty(turnEnd.PassiveActivations);
        Assert.Empty(ailment.Events);
        AssertReadOnly(activation.Effects, Effect(99));
        AssertReadOnly(dispatch.Activations, Activation("forged", []));
        AssertReadOnly(turnEnd.Events, Event("forged"));
        AssertReadOnly(turnEnd.PassiveActivations, Activation("forged_turn", []));
        AssertReadOnly(ailment.Events, Event("forged_ailment"));
    }

    private static PassiveTriggerExecutionResult Activation(
        string id,
        IReadOnlyList<EffectExecutionResult> effects) =>
        new(
            ContentId.Parse($"{id}_skill"),
            0,
            ContentId.Parse($"{id}_event"),
            RuntimeInstanceId.Parse($"{id}_target"),
            PassiveTriggerOutcome.Executed,
            effects);

    private static EffectExecutionResult Effect(int index) =>
        new(index, RuntimeInstanceId.Parse($"target_{index}"), EffectExecutionOutcome.Success);

    private static BattleStatusLifecycleEvent Event(string id) =>
        new(
            BattleStatusLifecycleEventKind.StatusExpired,
            RuntimeInstanceId.Parse($"{id}_actor"),
            ContentId.Parse($"{id}_status"));

    private static RuntimeActorState Actor(string id)
    {
        ContentId hp = ContentId.Parse("hp");
        ContentId team = ContentId.Parse("team");
        return new RuntimeActorState(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            team,
            hp,
            CombatDefenseProfile.Empty,
            [new BattleResourceState(hp, 10, 10)],
            new RuntimeEncounterPresenceSnapshot(true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("owner"), team),
            []);
    }

    private static AilmentDefinition Ailment(string id) =>
        new(
            ContentId.Parse(id),
            id,
            "Gate test ailment.",
            Lifetime(1),
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1, 0, 1, 1, false),
            new AilmentRecoveryDefinition());

    private static StatusLifetimeDefinition Lifetime(int turns) =>
        StandardStatusLifetimes.Field(
            new TurnDurationDefinition(turns, ContentId.Parse("owner_turn_end"), false));

    private static void AssertReadOnly<T>(IReadOnlyList<T> values, T forged) =>
        Assert.Throws<NotSupportedException>(() => ((IList<T>)values).Add(forged));
}
