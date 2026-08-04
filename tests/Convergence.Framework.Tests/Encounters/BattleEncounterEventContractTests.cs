using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Runtime;
using Convergence.TurnEconomy;
using Xunit;

namespace Convergence.Framework.Tests.Encounters;

public sealed class BattleEncounterEventContractTests
{
    [Fact]
    public void EveryCanonicalEventKind_ExposesItsTypedHostProjections()
    {
        RuntimeInstanceId actor = RuntimeInstanceId.Parse("event_contract_actor");
        RuntimeInstanceId target = RuntimeInstanceId.Parse("event_contract_target");
        ContentId team = ContentId.Parse("event_contract_team");
        ContentId entity = ContentId.Parse("event_contract_entity");
        ContentId action = ContentId.Parse("event_contract_action");
        ContentId resource = ContentId.Parse("hp");
        ContentId status = ContentId.Parse("event_contract_status");
        var beforeEconomy = new StandardActionTurnEconomySnapshot(2);
        var afterEconomy = new StandardActionTurnEconomySnapshot(1);
        var effect = new EffectExecutionResult(
            0,
            target,
            EffectExecutionOutcome.Success,
            Value: 12m);
        var statusEvent = new BattleStatusLifecycleEvent(
            BattleStatusLifecycleEventKind.ResourceChanged,
            actor,
            status,
            4m);

        (BattleEncounterEvent Event,
            RuntimeInstanceId? Actor,
            RuntimeInstanceId? Target,
            ContentId? Source,
            decimal? Value,
            BattleTurnEconomySnapshot? Economy,
            BattleEncounterFaultCode? Fault)[] cases =
        [
            Case(
                BattleEncounterEventKind.ActorCreated,
                new BattleActorCreatedEventPayload(actor, entity, team),
                actor: actor),
            Case(
                BattleEncounterEventKind.BattleStarted,
                new BattleStartedEventPayload(action, entity, null, 3, [actor, target], [team])),
            Case(
                BattleEncounterEventKind.InitiativeRolled,
                new BattleInitiativeRolledEventPayload([team])),
            Case(
                BattleEncounterEventKind.RoundStarted,
                new BattleRoundStartedEventPayload(1)),
            Case(
                BattleEncounterEventKind.PhaseStarted,
                new BattlePhaseStartedEventPayload(team, beforeEconomy),
                economy: beforeEconomy),
            Case(
                BattleEncounterEventKind.TurnStarted,
                new BattleTurnStartedEventPayload(actor, team),
                actor: actor),
            Case(
                BattleEncounterEventKind.TurnRestricted,
                new BattleTurnRestrictedEventPayload(actor, BattleTurnStartRestriction.CanAct),
                actor: actor),
            Case(
                BattleEncounterEventKind.CommandSelected,
                new BattleCommandSelectedEventPayload(actor, action, target),
                actor,
                target,
                action),
            Case(
                BattleEncounterEventKind.CommandPassed,
                new BattleCommandPassedEventPayload(actor),
                actor: actor),
            Case(
                BattleEncounterEventKind.ActionExecuted,
                new BattleActionExecutedEventPayload(
                    BattleActionEventKind.Executed,
                    actor,
                    target,
                    action,
                    5m),
                actor,
                target,
                action,
                5m),
            Case(
                BattleEncounterEventKind.ActionRejected,
                new BattleActionRejectedEventPayload(
                    actor,
                    BattleEncounterCommandStatus.Rejected,
                    action),
                actor: actor,
                source: action),
            Case(
                BattleEncounterEventKind.EffectResolved,
                new BattleEffectResolvedEventPayload(actor, action, effect),
                actor,
                target,
                action,
                12m),
            Case(
                BattleEncounterEventKind.PassiveActivated,
                new BattlePassiveActivatedEventPayload(actor, action),
                actor: actor,
                source: action),
            Case(
                BattleEncounterEventKind.StatusChanged,
                new BattleStatusChangedEventPayload(statusEvent),
                actor: actor,
                source: status,
                value: 4m),
            Case(
                BattleEncounterEventKind.ResourceChanged,
                new BattleResourceChangedEventPayload(actor, target, -8m, resource, action),
                actor,
                target,
                action,
                -8m),
            Case(
                BattleEncounterEventKind.TurnEconomyChanged,
                new BattleTurnEconomyChangedEventPayload(
                    actor,
                    beforeEconomy,
                    afterEconomy,
                    ActionTurnConsumption.Normal),
                actor: actor,
                economy: afterEconomy),
            Case(
                BattleEncounterEventKind.EncounterPresenceChanged,
                new BattleEncounterPresenceChangedEventPayload(actor, true, team),
                actor: actor),
            Case(
                BattleEncounterEventKind.ActorDefeated,
                new BattleActorDefeatedEventPayload(actor, team),
                actor: actor),
            Case(
                BattleEncounterEventKind.PhaseEnded,
                new BattlePhaseEndedEventPayload(team, afterEconomy),
                economy: afterEconomy),
            Case(
                BattleEncounterEventKind.BattleFaulted,
                new BattleFaultedEventPayload(
                    BattleEncounterFaultCode.TurnHandlerExecutionFailed,
                    actor,
                    team,
                    "turn-handler"),
                actor: actor,
                fault: BattleEncounterFaultCode.TurnHandlerExecutionFailed),
            Case(
                BattleEncounterEventKind.BattleEnded,
                new BattleEndedEventPayload(
                    BattleEncounterOutcome.Victory,
                    team,
                    finalRoundNumber: 1,
                    completedRounds: 0),
                source: team),
            Case(
                BattleEncounterEventKind.HostActionRequested,
                new BattleHostActionRequestedEventPayload(actor, action, target),
                actor,
                target,
                action),
            Case(
                BattleEncounterEventKind.TurnEnded,
                new BattleTurnEndedEventPayload(
                    actor,
                    team,
                    BattleEncounterTurnEndReason.CommandCommitted,
                    afterEconomy,
                    ActionTurnConsumption.Normal),
                actor: actor,
                economy: afterEconomy),
            Case(
                BattleEncounterEventKind.RoundEnded,
                new BattleRoundEndedEventPayload(1))
        ];

        Assert.Equal(Enum.GetValues<BattleEncounterEventKind>().Length, cases.Length);
        Assert.Equal(cases.Length, cases.Select(item => item.Event.Kind).Distinct().Count());
        foreach (var item in cases)
        {
            Assert.Equal(item.Actor, item.Event.ActorId);
            Assert.Equal(item.Target, item.Event.TargetId);
            Assert.Equal(item.Source, item.Event.SourceId);
            Assert.Equal(item.Value, item.Event.Value);
            Assert.Same(item.Economy, item.Event.TurnEconomyState);
            Assert.Equal(item.Fault, item.Event.FaultCode);
        }

        var resourceFallback = new BattleEncounterEvent(
            0,
            BattleEncounterEventKind.ResourceChanged,
            new BattleResourceChangedEventPayload(actor, target, 3m, resource));
        Assert.Equal(resource, resourceFallback.SourceId);

        var faultedEnd = new BattleEncounterEvent(
            0,
            BattleEncounterEventKind.BattleEnded,
            new BattleEndedEventPayload(
                BattleEncounterOutcome.Faulted,
                null,
                finalRoundNumber: 1,
                completedRounds: 0,
                faultCode: BattleEncounterFaultCode.EventPublicationFailed));
        Assert.Equal(BattleEncounterFaultCode.EventPublicationFailed, faultedEnd.FaultCode);
    }

    [Fact]
    public void PayloadConstructors_RejectContradictoryPublicState()
    {
        RuntimeInstanceId actor = RuntimeInstanceId.Parse("event_contract_actor");
        ContentId team = ContentId.Parse("event_contract_team");
        ContentId skill = ContentId.Parse("event_contract_skill");
        ContentId trigger = ContentId.Parse("event_contract_trigger");
        var economy = new StandardActionTurnEconomySnapshot(1);
        var passiveResult = new PassiveTriggerExecutionResult(
            skill,
            0,
            trigger,
            actor,
            PassiveTriggerOutcome.Executed,
            []);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleStartedEventPayload(team, skill, null, 0, [actor], [team]));
        Assert.Throws<ArgumentNullException>(() =>
            new BattleStartedEventPayload(team, skill, null, 1, null!, [team]));
        Assert.Throws<ArgumentNullException>(() =>
            new BattleStartedEventPayload(team, skill, null, 1, [actor], null!));
        Assert.Throws<ArgumentNullException>(() => new BattleInitiativeRolledEventPayload(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleRoundStartedEventPayload(0));
        Assert.Throws<ArgumentException>(() =>
            new BattlePhaseStartedEventPayload(default, economy));
        Assert.Throws<ArgumentNullException>(() =>
            new BattlePhaseStartedEventPayload(team, null!));

        Assert.Throws<ArgumentException>(() =>
            new BattlePassiveActivatedEventPayload(default, skill));
        Assert.Throws<ArgumentException>(() =>
            new BattlePassiveActivatedEventPayload(actor, default));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattlePassiveActivatedEventPayload(
                actor,
                skill,
                (PassiveTriggerOutcome)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattlePassiveActivatedEventPayload(
                actor,
                skill,
                PassiveTriggerOutcome.Executed,
                triggerIndex: -1));
        Assert.Throws<ArgumentException>(() =>
            new BattlePassiveActivatedEventPayload(
                actor,
                skill,
                PassiveTriggerOutcome.Executed,
                triggerIndex: 0,
                eventId: default(ContentId)));
        Assert.Throws<ArgumentException>(() =>
            new BattlePassiveActivatedEventPayload(
                actor,
                skill,
                PassiveTriggerOutcome.ConditionNotMet,
                triggerIndex: 0,
                eventId: trigger,
                result: passiveResult));

        Assert.Throws<ArgumentException>(() =>
            new BattleTurnEndedEventPayload(default, team, BattleEncounterTurnEndReason.ActorUnavailable, economy));
        Assert.Throws<ArgumentException>(() =>
            new BattleTurnEndedEventPayload(actor, default, BattleEncounterTurnEndReason.ActorUnavailable, economy));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleTurnEndedEventPayload(
                actor,
                team,
                (BattleEncounterTurnEndReason)int.MaxValue,
                economy));
        Assert.Throws<ArgumentNullException>(() =>
            new BattleTurnEndedEventPayload(
                actor,
                team,
                BattleEncounterTurnEndReason.ActorUnavailable,
                null!));
        Assert.Throws<ArgumentException>(() =>
            new BattleTurnEndedEventPayload(
                actor,
                team,
                BattleEncounterTurnEndReason.CommandCommitted,
                economy));
        Assert.Throws<ArgumentException>(() =>
            new BattleTurnEndedEventPayload(
                actor,
                team,
                BattleEncounterTurnEndReason.ActorUnavailable,
                economy,
                ActionTurnConsumption.Normal));

        Assert.Throws<ArgumentException>(() =>
            new BattlePhaseEndedEventPayload(default, economy));
        Assert.Throws<ArgumentNullException>(() =>
            new BattlePhaseEndedEventPayload(team, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleRoundEndedEventPayload(0));
    }

    [Fact]
    public void BattleEndPayload_RejectsContradictoryOutcomeEvidence()
    {
        ContentId team = ContentId.Parse("event_contract_team");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEndedEventPayload((BattleEncounterOutcome)int.MaxValue, null, 1, 0));
        Assert.Throws<ArgumentException>(() =>
            new BattleEndedEventPayload(BattleEncounterOutcome.Victory, default(ContentId), 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEndedEventPayload(
                BattleEncounterOutcome.Faulted,
                null,
                1,
                0,
                (BattleEncounterFaultCode)int.MaxValue));
        Assert.Throws<ArgumentException>(() =>
            new BattleEndedEventPayload(BattleEncounterOutcome.Victory, null, 1, 0));
        Assert.Throws<ArgumentException>(() =>
            new BattleEndedEventPayload(BattleEncounterOutcome.Draw, team, 1, 0));
        Assert.Throws<ArgumentException>(() =>
            new BattleEndedEventPayload(
                BattleEncounterOutcome.Faulted,
                null,
                finalRoundNumber: 1,
                completedRounds: 0,
                faultCode: null));
        Assert.Throws<ArgumentException>(() =>
            new BattleEndedEventPayload(
                BattleEncounterOutcome.Draw,
                null,
                1,
                0,
                BattleEncounterFaultCode.EventPublicationFailed));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEndedEventPayload(
                BattleEncounterOutcome.Draw,
                null,
                finalRoundNumber: 1,
                completedRounds: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEndedEventPayload(
                BattleEncounterOutcome.Draw,
                null,
                finalRoundNumber: 0,
                completedRounds: 0));
        Assert.Throws<ArgumentException>(() =>
            new BattleEndedEventPayload(BattleEncounterOutcome.Draw, null, null, 1));
        Assert.Throws<ArgumentException>(() =>
            new BattleEndedEventPayload(BattleEncounterOutcome.Draw, null, 1, 2));
    }

    [Fact]
    public void EncounterEvent_RejectsMalformedEnvelopeAndPayloadEvidence()
    {
        RuntimeInstanceId actor = RuntimeInstanceId.Parse("event_contract_actor");
        RuntimeInstanceId target = RuntimeInstanceId.Parse("event_contract_target");
        ContentId team = ContentId.Parse("event_contract_team");
        ContentId action = ContentId.Parse("event_contract_action");
        ContentId resource = ContentId.Parse("hp");
        var effect = new EffectExecutionResult(
            0,
            target,
            EffectExecutionOutcome.Success,
            Value: 1m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEncounterEvent(
                -1,
                BattleEncounterEventKind.CommandPassed,
                new BattleCommandPassedEventPayload(actor)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEncounterEvent(
                0,
                (BattleEncounterEventKind)int.MaxValue,
                new BattleCommandPassedEventPayload(actor)));
        Assert.Throws<ArgumentNullException>(() =>
            new BattleEncounterEvent(0, BattleEncounterEventKind.CommandPassed, null!));
        Assert.Throws<ArgumentException>(() =>
            new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.CommandSelected,
                new BattleCommandPassedEventPayload(actor)));

        AssertInvalid(
            BattleEncounterEventKind.BattleStarted,
            new BattleStartedEventPayload(team, action, null, 1, [], [team]));
        AssertInvalid(
            BattleEncounterEventKind.BattleStarted,
            new BattleStartedEventPayload(team, action, null, 1, [actor, actor], [team]));
        AssertInvalid(
            BattleEncounterEventKind.BattleStarted,
            new BattleStartedEventPayload(team, action, null, 1, [actor], [team, team]));
        AssertInvalid(
            BattleEncounterEventKind.InitiativeRolled,
            new BattleInitiativeRolledEventPayload([]));
        AssertInvalid(
            BattleEncounterEventKind.TurnStarted,
            new BattleTurnStartedEventPayload(default, team));
        AssertInvalid(
            BattleEncounterEventKind.TurnStarted,
            new BattleTurnStartedEventPayload(actor, default));
        AssertInvalid(
            BattleEncounterEventKind.TurnRestricted,
            new BattleTurnRestrictedEventPayload(actor, null!));
        AssertInvalid(
            BattleEncounterEventKind.CommandSelected,
            new BattleCommandSelectedEventPayload(default, action));
        AssertInvalid(
            BattleEncounterEventKind.CommandSelected,
            new BattleCommandSelectedEventPayload(actor, default));
        AssertInvalid(
            BattleEncounterEventKind.CommandSelected,
            new BattleCommandSelectedEventPayload(actor, action, default(RuntimeInstanceId)));
        AssertInvalid(
            BattleEncounterEventKind.CommandPassed,
            new BattleCommandPassedEventPayload(actor, (BattleTurnStartOutcome)int.MaxValue));
        AssertInvalid(
            BattleEncounterEventKind.ActionExecuted,
            new BattleActionExecutedEventPayload((BattleActionEventKind)int.MaxValue, actor));
        AssertInvalid(
            BattleEncounterEventKind.ActionExecuted,
            new BattleActionExecutedEventPayload(BattleActionEventKind.Executed));
        AssertInvalid(
            BattleEncounterEventKind.ActionExecuted,
            new BattleActionExecutedEventPayload(
                BattleActionEventKind.Executed,
                default(RuntimeInstanceId)));
        AssertInvalid(
            BattleEncounterEventKind.ActionExecuted,
            new BattleActionExecutedEventPayload(
                BattleActionEventKind.Executed,
                actor,
                default(RuntimeInstanceId)));
        AssertInvalid(
            BattleEncounterEventKind.ActionExecuted,
            new BattleActionExecutedEventPayload(
                BattleActionEventKind.Executed,
                actor,
                null,
                default(ContentId)));
        _ = Event(
            BattleEncounterEventKind.ActionExecuted,
            new BattleActionExecutedEventPayload(BattleActionEventKind.PartyRosterTransitioned));

        AssertInvalid(
            BattleEncounterEventKind.ActionRejected,
            new BattleActionRejectedEventPayload(default, BattleEncounterCommandStatus.Rejected));
        AssertInvalid(
            BattleEncounterEventKind.ActionRejected,
            new BattleActionRejectedEventPayload(actor, BattleEncounterCommandStatus.Executed));
        AssertInvalid(
            BattleEncounterEventKind.ActionRejected,
            new BattleActionRejectedEventPayload(
                actor,
                BattleEncounterCommandStatus.Rejected,
                default(ContentId)));
        AssertInvalid(
            BattleEncounterEventKind.EffectResolved,
            new BattleEffectResolvedEventPayload(default, action, effect));
        AssertInvalid(
            BattleEncounterEventKind.EffectResolved,
            new BattleEffectResolvedEventPayload(actor, default, effect));
        AssertInvalid(
            BattleEncounterEventKind.EffectResolved,
            new BattleEffectResolvedEventPayload(actor, action, null!));

        AssertInvalid(
            BattleEncounterEventKind.StatusChanged,
            new BattleStatusChangedEventPayload(null!));
        AssertInvalid(
            BattleEncounterEventKind.StatusChanged,
            new BattleStatusChangedEventPayload(
                new BattleStatusLifecycleEvent(
                    (BattleStatusLifecycleEventKind)int.MaxValue,
                    actor)));
        AssertInvalid(
            BattleEncounterEventKind.StatusChanged,
            new BattleStatusChangedEventPayload(
                new BattleStatusLifecycleEvent(BattleStatusLifecycleEventKind.ResourceChanged, default)));
        AssertInvalid(
            BattleEncounterEventKind.ResourceChanged,
            new BattleResourceChangedEventPayload(default, target, 1m, resource));
        AssertInvalid(
            BattleEncounterEventKind.ResourceChanged,
            new BattleResourceChangedEventPayload(actor, default, 1m, resource));
        AssertInvalid(
            BattleEncounterEventKind.ResourceChanged,
            new BattleResourceChangedEventPayload(actor, target, 1m, default(ContentId)));
        AssertInvalid(
            BattleEncounterEventKind.ResourceChanged,
            new BattleResourceChangedEventPayload(actor, target, 1m, resource, default(ContentId)));
        AssertInvalid(
            BattleEncounterEventKind.EncounterPresenceChanged,
            new BattleEncounterPresenceChangedEventPayload(default, true, team));
        AssertInvalid(
            BattleEncounterEventKind.EncounterPresenceChanged,
            new BattleEncounterPresenceChangedEventPayload(actor, true, default));
        AssertInvalid(
            BattleEncounterEventKind.ActorDefeated,
            new BattleActorDefeatedEventPayload(default, team));
        AssertInvalid(
            BattleEncounterEventKind.ActorDefeated,
            new BattleActorDefeatedEventPayload(actor, default));
        AssertInvalid(
            BattleEncounterEventKind.BattleFaulted,
            new BattleFaultedEventPayload((BattleEncounterFaultCode)int.MaxValue));
        AssertInvalid(
            BattleEncounterEventKind.BattleFaulted,
            new BattleFaultedEventPayload(
                BattleEncounterFaultCode.EventPublicationFailed,
                default(RuntimeInstanceId)));
        AssertInvalid(
            BattleEncounterEventKind.BattleFaulted,
            new BattleFaultedEventPayload(
                BattleEncounterFaultCode.EventPublicationFailed,
                TeamId: default(ContentId)));
        AssertInvalid(
            BattleEncounterEventKind.BattleFaulted,
            new BattleFaultedEventPayload(
                BattleEncounterFaultCode.EventPublicationFailed,
                PortName: " "));
        AssertInvalid(
            BattleEncounterEventKind.HostActionRequested,
            new BattleHostActionRequestedEventPayload(default, action));
        AssertInvalid(
            BattleEncounterEventKind.HostActionRequested,
            new BattleHostActionRequestedEventPayload(actor, default));
        AssertInvalid(
            BattleEncounterEventKind.HostActionRequested,
            new BattleHostActionRequestedEventPayload(actor, action, default(RuntimeInstanceId)));
    }

    [Fact]
    public void TurnEconomyEventPayloads_RejectIncompatibleSnapshots()
    {
        RuntimeInstanceId actor = RuntimeInstanceId.Parse("event_contract_actor");
        ContentId team = ContentId.Parse("event_contract_team");
        var standard = new StandardActionTurnEconomySnapshot(2);
        var actionToken = new ActionTokenTurnEconomySnapshot(1, 0);
        var alternateStandard = new AlternateStandardTurnEconomySnapshot(1);

        Assert.Throws<ArgumentException>(() =>
            new BattleTurnEconomyChangedEventPayload(
                default,
                standard,
                new StandardActionTurnEconomySnapshot(1),
                ActionTurnConsumption.Normal));
        Assert.Throws<ArgumentNullException>(() =>
            new BattleTurnEconomyChangedEventPayload(
                actor,
                null!,
                standard,
                ActionTurnConsumption.Normal));
        Assert.Throws<ArgumentNullException>(() =>
            new BattleTurnEconomyChangedEventPayload(
                actor,
                standard,
                null!,
                ActionTurnConsumption.Normal));
        Assert.Throws<ArgumentNullException>(() =>
            new BattleTurnEconomyChangedEventPayload(actor, standard, standard, null!));
        Assert.Throws<ArgumentException>(() =>
            new BattleTurnEconomyChangedEventPayload(
                actor,
                standard,
                actionToken,
                ActionTurnConsumption.Normal));
        Assert.Throws<ArgumentException>(() =>
            new BattleTurnEconomyChangedEventPayload(
                actor,
                standard,
                alternateStandard,
                ActionTurnConsumption.Normal));
        Assert.Throws<ArgumentException>(() =>
            new BattlePhaseStartedEventPayload(default, standard));
        Assert.Throws<ArgumentNullException>(() =>
            new BattlePhaseEndedEventPayload(team, null!));
    }

    [Fact]
    public void TypedPayloadDeconstruction_PreservesAllAuthoredEvidence()
    {
        RuntimeInstanceId actor = RuntimeInstanceId.Parse("event_contract_actor");
        ContentId team = ContentId.Parse("event_contract_team");
        ContentId skill = ContentId.Parse("event_contract_skill");
        ContentId trigger = ContentId.Parse("event_contract_trigger");
        var before = new StandardActionTurnEconomySnapshot(2);
        var after = new StandardActionTurnEconomySnapshot(1);
        var passive = new BattlePassiveActivatedEventPayload(
            actor,
            skill,
            PassiveTriggerOutcome.Executed,
            3,
            trigger);

        var phaseStarted = new BattlePhaseStartedEventPayload(team, before);
        phaseStarted.Deconstruct(out ContentId startedTeam, out BattleTurnEconomySnapshot startedState);
        Assert.Equal(team, startedTeam);
        Assert.Same(before, startedState);

        passive.Deconstruct(
            out RuntimeInstanceId passiveActor,
            out ContentId passiveSkill,
            out PassiveTriggerOutcome? outcome,
            out int? triggerIndex,
            out ContentId? triggerId);
        Assert.Equal((actor, skill, PassiveTriggerOutcome.Executed, 3, trigger),
            (passiveActor, passiveSkill, outcome, triggerIndex, triggerId));

        var changed = new BattleTurnEconomyChangedEventPayload(
            actor,
            before,
            after,
            ActionTurnConsumption.Normal);
        changed.Deconstruct(
            out RuntimeInstanceId changedActor,
            out BattleTurnEconomySnapshot changedBefore,
            out BattleTurnEconomySnapshot changedAfter,
            out ActionTurnConsumption consumption);
        Assert.Equal(actor, changedActor);
        Assert.Same(before, changedBefore);
        Assert.Same(after, changedAfter);
        Assert.Same(ActionTurnConsumption.Normal, consumption);

        var phaseEnded = new BattlePhaseEndedEventPayload(team, after);
        phaseEnded.Deconstruct(out ContentId endedTeam, out BattleTurnEconomySnapshot endedState);
        Assert.Equal(team, endedTeam);
        Assert.Same(after, endedState);
    }

    private static BattleEncounterEvent Event(
        BattleEncounterEventKind kind,
        BattleEncounterEventPayload payload) =>
        new(0, kind, payload);

    private static void AssertInvalid(
        BattleEncounterEventKind kind,
        BattleEncounterEventPayload payload) =>
        Assert.ThrowsAny<ArgumentException>(() => Event(kind, payload));

    private static (
        BattleEncounterEvent Event,
        RuntimeInstanceId? Actor,
        RuntimeInstanceId? Target,
        ContentId? Source,
        decimal? Value,
        BattleTurnEconomySnapshot? Economy,
        BattleEncounterFaultCode? Fault) Case(
            BattleEncounterEventKind kind,
            BattleEncounterEventPayload payload,
            RuntimeInstanceId? actor = null,
            RuntimeInstanceId? target = null,
            ContentId? source = null,
            decimal? value = null,
            BattleTurnEconomySnapshot? economy = null,
            BattleEncounterFaultCode? fault = null) =>
        (new BattleEncounterEvent(0, kind, payload), actor, target, source, value, economy, fault);

    private sealed record AlternateStandardTurnEconomySnapshot : BattleTurnEconomySnapshot
    {
        public AlternateStandardTurnEconomySnapshot(int remainingActions)
            : base(StandardActionTurnEconomy.EconomyId, remainingActions)
        {
        }
    }
}
