using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Bridges;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Core;
using Convergence.Tests.TestSupport;
using Xunit;

namespace Convergence.Tests;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class NegotiationRewardPresentationTests
{
    [Fact]
    public async Task PromptAdapter_MapsAnswerDemandSelectionAndCancellation()
    {
        var io = new ScriptedGameIO().QueueMenu(1, -1);
        var adapter = new LegacyNegotiationPresentationAdapter(io, "Pixie");

        NegotiationAnswerSelection answer = await adapter.ReadAnswerAsync(
            new NegotiationQuestionPrompt(
                "Do you like humans?",
                [
                    new NegotiationAnswerOption("Yes", 2),
                    new NegotiationAnswerOption("No", -2)
                ]));
        NegotiationDemandSelection demand = await adapter.ReadDemandAsync(
            new NegotiationDemandPrompt(
                NegotiationDemandKind.Macca,
                "Pixie: \"A gift of 40 Macca should suffice.\"",
                [
                    new NegotiationDemandOption(NegotiationDemandDecision.Accept, "Give 40 Macca"),
                    new NegotiationDemandOption(NegotiationDemandDecision.Refuse, "Refuse")
                ]));

        Assert.False(answer.Cancelled);
        Assert.Equal(1, answer.SelectedIndex);
        Assert.True(demand.Cancelled);
        Assert.Equal(
            "Pixie: \"Do you like humans?\"",
            adapter.AnswerPrompts.Single().Header);
        Assert.Equal(["Yes", "No"], adapter.AnswerPrompts.Single().Options);
        Assert.Equal(NegotiationPresentationKind.Selected, adapter.AnswerPrompts.Single().Kind);
        Assert.Equal(1, adapter.AnswerPrompts.Single().SelectedIndex);
        Assert.Equal(
            "Pixie: \"A gift of 40 Macca should suffice.\"",
            adapter.DemandPrompts.Single().Header);
        Assert.Equal(["Give 40 Macca", "Refuse"], adapter.DemandPrompts.Single().Options);
        Assert.Equal(NegotiationPresentationKind.Back, adapter.DemandPrompts.Single().Kind);
        Assert.Null(adapter.DemandPrompts.Single().SelectedIndex);
        Assert.Equal(
            ["Pixie: \"Do you like humans?\"", "Pixie: \"A gift of 40 Macca should suffice.\""],
            io.Menus.Select(menu => menu.Header));
    }

    [Theory]
    [InlineData(NegotiationEventKind.Information, "Info", ConsoleColor.White, 0)]
    [InlineData(NegotiationEventKind.Warning, "Warn", ConsoleColor.White, 0)]
    [InlineData(NegotiationEventKind.Failure, "The Full Moon blocks talk.", ConsoleColor.Red, 1000)]
    [InlineData(NegotiationEventKind.Failure, "The required donation of 40 Macca is missing.", ConsoleColor.Red, 1000)]
    [InlineData(NegotiationEventKind.Failure, "Pixie refuses to talk!", ConsoleColor.White, 1000)]
    [InlineData(NegotiationEventKind.Failure, "Pixie seems unresponsive...", ConsoleColor.White, 800)]
    [InlineData(NegotiationEventKind.FamiliarDialogue, "Pixie: \"We meet again.\"", ConsoleColor.Cyan, 0)]
    [InlineData(NegotiationEventKind.DemandIntro, "Pixie: \"Talk is cheap.\"", ConsoleColor.White, 800)]
    [InlineData(NegotiationEventKind.MoodPositive, "Pixie seems pleased.", ConsoleColor.White, 0)]
    [InlineData(NegotiationEventKind.MoodNeutral, "Pixie is considering your words...", ConsoleColor.White, 0)]
    [InlineData(NegotiationEventKind.MoodNegative, "Pixie grows angry!", ConsoleColor.Red, 800)]
    public void EventPresentation_PreservesCurrentColorDelayAndMessage(
        NegotiationEventKind kind,
        string message,
        ConsoleColor color,
        int delay)
    {
        NegotiationEvent source = new(kind, message);

        NegotiationEventPresentationResult presentation =
            LegacyNegotiationPresentationAdapter.PresentEvent(source);

        Assert.Equal(NegotiationPresentationKind.Shown, presentation.Kind);
        Assert.Same(source, presentation.SourceEvent);
        Assert.Equal(message, presentation.Message);
        Assert.Equal(color, presentation.Color);
        Assert.Equal(delay, presentation.Delay);
    }

    [Fact]
    public void DetailedNegotiation_BlockedMoonRecordsTypedResultAndMutationSummary()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO();
        var actor = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var target = CombatantFactory.CreateEnemy("pixie");
        for (int i = 0; i < 8; i++)
        {
            MoonPhaseSystem.Advance();
        }

        NegotiationSessionPresentationResult result = new NegotiationEngine(
            io,
            new PartyManager(actor),
            new InventoryManager(),
            new EconomyManager(),
            new Random(1)).StartNegotiationDetailed(actor, target, [target]);

        Assert.Equal(NegotiationResult.Failure, result.LegacyResult);
        Assert.Equal(NegotiationOutcomeReason.MoonBlocked, result.SessionResult.Reason);
        Assert.Empty(result.AnswerPrompts);
        Assert.Empty(result.DemandPrompts);
        Assert.Equal(0, result.Mutation.MaccaSpent);
        Assert.Null(result.Mutation.ItemSpentId);
        NegotiationEventPresentationResult presentation = Assert.Single(result.Events);
        Assert.Equal(ConsoleColor.Red, presentation.Color);
        Assert.Equal(1000, presentation.Delay);
        Assert.Contains("Full Moon", io.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailedNegotiation_MissingQuestionsRecordsTypedFailure()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var original = Database.NegotiationQuestions.Questions[PersonalityType.Childlike];
        Database.NegotiationQuestions.Questions[PersonalityType.Childlike] = [];
        try
        {
            var io = new ScriptedGameIO();
            var actor = new Combatant("Hero", ClassType.Operator) { Level = 50 };
            var target = CombatantFactory.CreateEnemy("pixie");

            NegotiationSessionPresentationResult result = new NegotiationEngine(
                io,
                new PartyManager(actor),
                new InventoryManager(),
                new EconomyManager(),
                new Random(1)).StartNegotiationDetailed(actor, target, [target]);

            Assert.Equal(NegotiationResult.Failure, result.LegacyResult);
            Assert.Equal(NegotiationOutcomeReason.MissingQuestions, result.SessionResult.Reason);
            Assert.Empty(result.AnswerPrompts);
            Assert.Empty(result.DemandPrompts);
            Assert.Contains("seems unresponsive", io.CombinedOutput, StringComparison.Ordinal);
            Assert.Equal(800, Assert.Single(result.Events).Delay);
        }
        finally
        {
            Database.NegotiationQuestions.Questions[PersonalityType.Childlike] = original;
        }
    }

    [Fact]
    public void DetailedNegotiation_FamiliarRefusalAndSuccessPreserveLegacyMutation()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();

        var familiarIo = new ScriptedGameIO();
        var familiarInventory = new InventoryManager();
        var familiarEconomy = new EconomyManager();
        var familiarActor = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        familiarActor.DemonStock.Add(CombatantFactory.CreatePlayerDemon("pixie", 10));
        var familiarTarget = CombatantFactory.CreateEnemy("pixie");

        NegotiationSessionPresentationResult familiar = new NegotiationEngine(
            familiarIo,
            new PartyManager(familiarActor),
            familiarInventory,
            familiarEconomy,
            new Random(2)).StartNegotiationDetailed(familiarActor, familiarTarget, [familiarTarget]);

        Assert.Equal(NegotiationResult.FamiliarFlee, familiar.LegacyResult);
        Assert.Equal(NegotiationOutcomeReason.FamiliarDemon, familiar.SessionResult.Reason);
        Assert.Contains(familiar.Events, ev => ev.SourceEvent.Kind == NegotiationEventKind.FamiliarDialogue);
        Assert.Equal(familiar.SessionResult.FamiliarGift, familiar.Mutation.FamiliarGift);

        var refusalIo = new ScriptedGameIO().QueueMenu(0, 0, 0, 1);
        var refusalEconomy = new EconomyManager();
        refusalEconomy.AddMacca(100_000);
        var refusalActor = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var refusalTarget = CombatantFactory.CreateEnemy("pixie");
        int maccaBeforeRefusal = refusalEconomy.Macca;

        NegotiationSessionPresentationResult refusal = new NegotiationEngine(
            refusalIo,
            new PartyManager(refusalActor),
            new InventoryManager(),
            refusalEconomy,
            new Random(3)).StartNegotiationDetailed(refusalActor, refusalTarget, [refusalTarget]);

        Assert.Equal(NegotiationResult.Failure, refusal.LegacyResult);
        Assert.Contains(
            refusal.SessionResult.Reason,
            new[] { NegotiationOutcomeReason.MaccaRefused, NegotiationOutcomeReason.ItemRefused });
        Assert.Equal(maccaBeforeRefusal, refusalEconomy.Macca);
        Assert.Equal(4, refusalIo.Menus.Count);
        Assert.NotEmpty(refusal.AnswerPrompts);
        Assert.NotEmpty(refusal.DemandPrompts);

        var successIo = new ScriptedGameIO().QueueMenu(0, 0, 0, 0);
        var successEconomy = new EconomyManager();
        successEconomy.AddMacca(100_000);
        var successActor = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var successTarget = CombatantFactory.CreateEnemy("pixie");

        NegotiationSessionPresentationResult success = new NegotiationEngine(
            successIo,
            new PartyManager(successActor),
            new InventoryManager(),
            successEconomy,
            new Random(3)).StartNegotiationDetailed(successActor, successTarget, [successTarget]);

        Assert.Equal(NegotiationResult.Success, success.LegacyResult);
        Assert.Equal(NegotiationOutcomeReason.None, success.SessionResult.Reason);
        Assert.True(success.Mutation.MaccaSpent > 0);
        Assert.True(successEconomy.Macca < 100_000);
    }

    [Fact]
    public void RecruitmentPresentation_PreservesMessagesAndTurnEffects()
    {
        Combatant target = new("Pixie", ClassType.Demon)
        {
            SourceId = "pixie",
            Level = 2
        };

        AssertPresentation(
            BattleNegotiationPresentationResult.AlreadySpoken(target),
            "Pixie has already been spoken to.",
            ConsoleColor.Gray,
            800,
            BattleNegotiationTurnEffect.None,
            removeTarget: false);
        AssertPresentation(
            BattleNegotiationPresentationResult.Joined(target),
            "Pixie joined your party!",
            ConsoleColor.Green,
            0,
            BattleNegotiationTurnEffect.Normal,
            removeTarget: false);
        AssertPresentation(
            BattleNegotiationPresentationResult.FailedEndsTurn(),
            "Negotiation failed! Your turn ends.",
            ConsoleColor.Red,
            0,
            BattleNegotiationTurnEffect.TerminatePhase,
            removeTarget: false);
        AssertPresentation(
            BattleNegotiationPresentationResult.LeftBattle(target),
            "Pixie left the battle.",
            ConsoleColor.Gray,
            0,
            BattleNegotiationTurnEffect.Miss,
            removeTarget: true);
    }

    [Fact]
    public void RewardPresentation_UsesImmutableRewardTotalsAndLegacyMessage()
    {
        var reward = new BattleRewardResult(46, 125);

        BattleRewardPresentationResult presentation = BattleRewardPresentationResult.Shown(reward);

        Assert.Equal(NegotiationPresentationKind.Shown, presentation.Kind);
        Assert.Same(reward, presentation.SourceResult);
        Assert.Equal("Gained 46 EXP and 125 Macca.", presentation.Message);
        Assert.Equal(ConsoleColor.Gray, presentation.Color);
        Assert.Equal(800, presentation.Delay);
    }

    private static void AssertPresentation(
        BattleNegotiationPresentationResult presentation,
        string message,
        ConsoleColor color,
        int delay,
        BattleNegotiationTurnEffect turnEffect,
        bool removeTarget)
    {
        Assert.Equal(NegotiationPresentationKind.Shown, presentation.Kind);
        Assert.Equal(message, presentation.Message);
        Assert.Equal(color, presentation.Color);
        Assert.Equal(delay, presentation.Delay);
        Assert.Equal(turnEffect, presentation.TurnEffect);
        Assert.Equal(removeTarget, presentation.RemoveTarget);
    }
}
