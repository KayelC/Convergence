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

    [Theory]
    [InlineData(ActionTurnConsumptionKind.Normal)]
    [InlineData(ActionTurnConsumptionKind.Pass)]
    [InlineData(ActionTurnConsumptionKind.TurnEconomy)]
    public void StandardActionEconomy_ConsumesOneOpportunityForEveryPricedAction(
        ActionTurnConsumptionKind kind)
    {
        var standard = new StandardActionTurnEconomy();
        standard.StartPhase(2);

        standard.Apply(ActionTurnConsumption.None);
        Assert.Equal(2, standard.CaptureSnapshot().RemainingActions);

        standard.Apply(kind switch
        {
            ActionTurnConsumptionKind.Normal => ActionTurnConsumption.Normal,
            ActionTurnConsumptionKind.Pass => ActionTurnConsumption.Pass,
            ActionTurnConsumptionKind.TurnEconomy => Outcome(TurnEconomyOutcome.Weakness),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        });

        Assert.Equal(1, standard.CaptureSnapshot().RemainingActions);
        standard.Apply(ActionTurnConsumption.TerminatePhase);
        Assert.Equal(0, standard.CaptureSnapshot().RemainingActions);
        standard.Apply(ActionTurnConsumption.Normal);
        Assert.Equal(0, standard.CaptureSnapshot().RemainingActions);
    }

    [Fact]
    public void ActionToken_NormalAndPassConsumePartialTokensBeforeFullTokens()
    {
        var normal = new ActionTokenTurnEconomy();
        normal.StartPhase(2);
        normal.Apply(Outcome(TurnEconomyOutcome.Weakness));
        AssertTokens(normal, full: 1, partial: 1);
        normal.Apply(ActionTurnConsumption.Normal);
        AssertTokens(normal, full: 1, partial: 0);
        normal.Apply(ActionTurnConsumption.Normal);
        AssertTokens(normal, full: 0, partial: 0);

        var pass = new ActionTokenTurnEconomy();
        pass.StartPhase(2);
        pass.Apply(Outcome(TurnEconomyOutcome.Weakness));
        AssertTokens(pass, full: 1, partial: 1);
        pass.Apply(ActionTurnConsumption.Pass);
        AssertTokens(pass, full: 1, partial: 0);
        pass.Apply(ActionTurnConsumption.Pass);
        AssertTokens(pass, full: 0, partial: 1);
        pass.Apply(ActionTurnConsumption.Pass);
        AssertTokens(pass, full: 0, partial: 0);
    }

    [Theory]
    [InlineData(TurnEconomyOutcome.Weakness)]
    [InlineData(TurnEconomyOutcome.Critical)]
    public void ActionToken_RewardedOutcomesConvertAFullTokenOrConsumeAPartial(
        TurnEconomyOutcome outcome)
    {
        var economy = new ActionTokenTurnEconomy();
        economy.StartPhase(1);

        economy.Apply(Outcome(outcome));
        AssertTokens(economy, full: 0, partial: 1);
        economy.Apply(Outcome(outcome));
        AssertTokens(economy, full: 0, partial: 0);
    }

    [Theory]
    [InlineData(TurnEconomyOutcome.Miss)]
    [InlineData(TurnEconomyOutcome.Null)]
    public void ActionToken_PenaltyOutcomesConsumeUpToTwoTokensPartialFirst(
        TurnEconomyOutcome outcome)
    {
        var economy = new ActionTokenTurnEconomy();
        economy.StartPhase(3);
        economy.Apply(Outcome(TurnEconomyOutcome.Weakness));
        AssertTokens(economy, full: 2, partial: 1);

        economy.Apply(Outcome(outcome));
        AssertTokens(economy, full: 1, partial: 0);
        economy.Apply(Outcome(outcome));
        AssertTokens(economy, full: 0, partial: 0);
    }

    [Theory]
    [InlineData(TurnEconomyOutcome.Repel)]
    [InlineData(TurnEconomyOutcome.Absorb)]
    public void ActionToken_TerminatingDefenseOutcomesClearThePhase(TurnEconomyOutcome outcome)
    {
        var economy = new ActionTokenTurnEconomy();
        economy.StartPhase(3);

        economy.Apply(Outcome(outcome));

        AssertTokens(economy, full: 0, partial: 0);
    }

    [Fact]
    public void ActionToken_ExplicitTerminationAndResolutionTerminationClearThePhase()
    {
        var explicitTermination = new ActionTokenTurnEconomy();
        explicitTermination.StartPhase(2);
        explicitTermination.Apply(ActionTurnConsumption.TerminatePhase);
        AssertTokens(explicitTermination, full: 0, partial: 0);

        var resolutionTermination = new ActionTokenTurnEconomy();
        resolutionTermination.StartPhase(2);
        resolutionTermination.Apply(ActionTurnConsumption.FromTurnEconomy(
            new TurnEconomyResolution(TurnEconomyOutcome.Normal, false, true)));
        AssertTokens(resolutionTermination, full: 0, partial: 0);
    }

    [Fact]
    public void SuppliedTurnEconomies_ValidatePhaseAndSnapshotNumericBoundaries()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StandardActionTurnEconomy().StartPhase(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActionTokenTurnEconomy().StartPhase(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActionTokenTurnEconomySnapshot(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActionTokenTurnEconomySnapshot(0, -1));
        Assert.Throws<OverflowException>(() =>
            new ActionTokenTurnEconomySnapshot(int.MaxValue, 1));
    }

    private static ActionTurnConsumption Outcome(TurnEconomyOutcome outcome) =>
        ActionTurnConsumption.FromTurnEconomy(new TurnEconomyResolution(outcome, false, false));

    private static void AssertTokens(ActionTokenTurnEconomy economy, int full, int partial)
    {
        var snapshot = Assert.IsType<ActionTokenTurnEconomySnapshot>(economy.CaptureSnapshot());
        Assert.Equal(full, snapshot.FullTokens);
        Assert.Equal(partial, snapshot.PartialTokens);
        Assert.Equal(full + partial, snapshot.RemainingActions);
        Assert.Equal(full + partial > 0, economy.HasTurnsRemaining());
    }

    private sealed record TestTurnEconomySnapshot : BattleTurnEconomySnapshot
    {
        public TestTurnEconomySnapshot(ContentId economyId, int remainingActions)
            : base(economyId, remainingActions)
        {
        }
    }
}
