using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Runtime;
using Convergence.TurnEconomy;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class TurnEconomyContractTests
{
    [Fact]
    public void ActionTurnConsumption_ExposesOnlyValidatedImmutableShapes()
    {
        var resolution = new TurnEconomyResolution(TurnEconomyOutcome.Weakness, true, false);

        Assert.Equal(ActionTurnConsumptionKind.None, ActionTurnConsumption.None.Kind);
        Assert.Equal(ActionTurnConsumptionKind.Normal, ActionTurnConsumption.Normal.Kind);
        Assert.Equal(ActionTurnConsumptionKind.Pass, ActionTurnConsumption.Pass.Kind);
        Assert.Equal(ActionTurnConsumptionKind.TerminatePhase, ActionTurnConsumption.TerminatePhase.Kind);
        Assert.Equal(resolution, ActionTurnConsumption.FromTurnEconomy(resolution).TurnEconomy);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActionTurnConsumption((ActionTurnConsumptionKind)999));
        Assert.Throws<ArgumentNullException>(() =>
            new ActionTurnConsumption(ActionTurnConsumptionKind.TurnEconomy));
        Assert.Throws<ArgumentException>(() =>
            new ActionTurnConsumption(ActionTurnConsumptionKind.Normal, resolution));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TurnEconomyResolution((TurnEconomyOutcome)999, false, false));

        Assert.Null(typeof(ActionTurnConsumption).GetProperty(nameof(ActionTurnConsumption.Kind))!.SetMethod);
        Assert.Null(typeof(ActionTurnConsumption).GetProperty(nameof(ActionTurnConsumption.TurnEconomy))!.SetMethod);
        Assert.Null(typeof(TurnEconomyResolution).GetProperty(nameof(TurnEconomyResolution.Outcome))!.SetMethod);
        Assert.Equal(ActionTurnConsumption.Normal, ActionTurnConsumption.Normal with { });
        Assert.Equal(resolution, resolution with { });
    }

    [Fact]
    public void EncounterCommandResult_RejectsMalformedHostValuesAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEncounterCommandResult(
                (BattleEncounterCommandStatus)999,
                ActionTurnConsumption.Normal));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEncounterCommandResult(
                BattleEncounterCommandStatus.Executed,
                ActionTurnConsumption.Normal,
                requestedOutcome: (BattleEncounterOutcome)999));
        Assert.Throws<ArgumentNullException>(() =>
            new BattleEncounterCommandResult(
                BattleEncounterCommandStatus.Executed,
                null!));
        Assert.Throws<ArgumentException>(() =>
            new BattleEncounterCommandResult(
                BattleEncounterCommandStatus.Executed,
                ActionTurnConsumption.Normal,
                winningTeamId: default(ContentId)));
        Assert.Throws<ArgumentException>(() =>
            new BattleEncounterCommandResult(
                BattleEncounterCommandStatus.Executed,
                ActionTurnConsumption.Normal,
                events: [null!]));
    }

    [Fact]
    public void TurnEconomySnapshots_RejectDefaultEconomyIdentity()
    {
        Assert.Throws<ArgumentException>(() => new TestTurnEconomySnapshot(default, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TestTurnEconomySnapshot(ContentId.Parse("test_economy"), -1));
    }

    [Fact]
    public void TurnEconomyEventPayloads_RejectMalformedConstruction()
    {
        var standard = new StandardActionTurnEconomySnapshot(1);
        var actionToken = new ActionTokenTurnEconomySnapshot(1, 0);
        RuntimeInstanceId actorId = RuntimeInstanceId.Parse("event_actor");

        Assert.Throws<ArgumentException>(() =>
            new BattlePhaseStartedEventPayload(default, standard));
        Assert.Throws<ArgumentNullException>(() =>
            new BattlePhaseStartedEventPayload(ContentId.Parse("player_team"), null!));
        Assert.Throws<ArgumentException>(() =>
            new BattlePhaseEndedEventPayload(default, standard));
        Assert.Throws<ArgumentNullException>(() =>
            new BattlePhaseEndedEventPayload(ContentId.Parse("player_team"), null!));

        Assert.Throws<ArgumentException>(() =>
            new BattleTurnEconomyChangedEventPayload(
                default,
                standard,
                new StandardActionTurnEconomySnapshot(0),
                ActionTurnConsumption.Normal));
        Assert.Throws<ArgumentNullException>(() =>
            new BattleTurnEconomyChangedEventPayload(
                actorId,
                null!,
                standard,
                ActionTurnConsumption.Normal));
        Assert.Throws<ArgumentNullException>(() =>
            new BattleTurnEconomyChangedEventPayload(
                actorId,
                standard,
                null!,
                ActionTurnConsumption.Normal));
        Assert.Throws<ArgumentNullException>(() =>
            new BattleTurnEconomyChangedEventPayload(actorId, standard, standard, null!));
        Assert.Throws<ArgumentException>(() =>
            new BattleTurnEconomyChangedEventPayload(
                actorId,
                standard,
                actionToken,
                ActionTurnConsumption.Normal));
        Assert.Throws<ArgumentException>(() =>
            new BattleTurnEconomyChangedEventPayload(
                actorId,
                new TestTurnEconomySnapshot(ContentId.Parse("first_economy"), 1),
                new TestTurnEconomySnapshot(ContentId.Parse("second_economy"), 0),
                ActionTurnConsumption.Normal));
    }

    [Fact]
    public void EncounterEvent_RejectsMalformedTurnEconomyPayloadClones()
    {
        var standard = new StandardActionTurnEconomySnapshot(1);
        var validPhase = new BattlePhaseStartedEventPayload(
            ContentId.Parse("player_team"),
            standard);
        var validTransition = new BattleTurnEconomyChangedEventPayload(
            RuntimeInstanceId.Parse("event_actor"),
            standard,
            new StandardActionTurnEconomySnapshot(0),
            ActionTurnConsumption.Normal);

        Assert.Throws<ArgumentException>(() => new BattleEncounterEvent(
            0,
            BattleEncounterEventKind.PhaseStarted,
            validPhase with { TeamId = default }));
        Assert.Throws<ArgumentNullException>(() => new BattleEncounterEvent(
            0,
            BattleEncounterEventKind.PhaseStarted,
            validPhase with { TurnEconomyState = null! }));
        Assert.Throws<ArgumentException>(() => new BattleEncounterEvent(
            0,
            BattleEncounterEventKind.TurnEconomyChanged,
            validTransition with { ActorId = default }));
        Assert.Throws<ArgumentException>(() => new BattleEncounterEvent(
            0,
            BattleEncounterEventKind.TurnEconomyChanged,
            validTransition with { After = new ActionTokenTurnEconomySnapshot(0, 0) }));
    }

    [Fact]
    public void SuppliedTurnEconomiesPreserveEveryLegalConsumptionShape()
    {
        var standard = new StandardActionTurnEconomy();
        standard.StartPhase(2);
        standard.Apply(ActionTurnConsumption.None);
        Assert.Equal(2, standard.CaptureSnapshot().RemainingActions);
        standard.Apply(ActionTurnConsumption.FromTurnEconomy(
            new TurnEconomyResolution(TurnEconomyOutcome.Weakness, false, false)));
        Assert.Equal(1, standard.CaptureSnapshot().RemainingActions);
        standard.Apply(ActionTurnConsumption.TerminatePhase);
        Assert.Equal(0, standard.CaptureSnapshot().RemainingActions);

        var actionTokens = new ActionTokenTurnEconomy();
        actionTokens.StartPhase(1);
        actionTokens.Apply(ActionTurnConsumption.FromTurnEconomy(
            new TurnEconomyResolution(TurnEconomyOutcome.Weakness, false, false)));
        Assert.Equal(0, actionTokens.FullTokens);
        Assert.Equal(1, actionTokens.PartialTokens);
        actionTokens.Apply(ActionTurnConsumption.Pass);
        Assert.False(actionTokens.HasTurnsRemaining());
    }

    private sealed record TestTurnEconomySnapshot : BattleTurnEconomySnapshot
    {
        public TestTurnEconomySnapshot(ContentId economyId, int remainingActions)
            : base(economyId, remainingActions)
        {
        }
    }
}
