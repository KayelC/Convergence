using JRPGPrototype.Core;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Bridges;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Messaging;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Runtime;
using JRPGPrototype.Services;
using Convergence.Tests.TestSupport;
using Xunit;

namespace Convergence.Tests;

public sealed class BattleEventPresentationTests
{
    [Fact]
    public void EventSink_MapsEveryEncounterEventKindDeterministically()
    {
        var adapter = new LegacyBattleEventPresentationAdapter(new RecordingBattleMessenger());

        foreach (BattleEncounterEventKind kind in Enum.GetValues<BattleEncounterEventKind>())
        {
            var source = new BattleEncounterEvent(
                7,
                kind,
                kind.ToString(),
                RuntimeInstanceId.Parse("actor"));

            BattleEventPresentationResult presentation = adapter.Present(source);

            Assert.Equal(kind, presentation.EventKind);
            Assert.Same(source, presentation.SourceEvent);
            Assert.Equal(ExpectedKind(kind), presentation.Kind);
        }
    }

    [Fact]
    public async Task PublishAsync_SuppressesStructuralEventsAndPreservesSourceOrder()
    {
        var messenger = new RecordingBattleMessenger();
        var adapter = new LegacyBattleEventPresentationAdapter(messenger);
        BattleEncounterEvent[] events =
        [
            Event(2, BattleEncounterEventKind.ActorCreated),
            Event(3, BattleEncounterEventKind.PhaseStarted),
            Event(4, BattleEncounterEventKind.PressTurnChanged)
        ];

        foreach (BattleEncounterEvent battleEvent in events)
        {
            await adapter.PublishAsync(battleEvent);
        }

        Assert.Empty(messenger.Messages);
        Assert.Equal([2, 3, 4], adapter.Presentations.Select(p => p.SourceEvent!.Sequence));
        Assert.All(adapter.Presentations, presentation =>
            Assert.Equal(BattleEventPresentationKind.Suppressed, presentation.Kind));
    }

    [Fact]
    public void LifecyclePresentations_PreserveLegacyMessages()
    {
        var messenger = new RecordingBattleMessenger();
        var adapter = new LegacyBattleEventPresentationAdapter(messenger);
        Combatant hero = Actor("Hero");
        Combatant enemy = Actor("Slime", ClassType.Demon);
        Combatant demon = Actor("Pixie", ClassType.Demon);

        adapter.Publish(adapter.PresentTurnRestriction(hero, TurnStartResult.Skip, isPlayerSide: true));
        adapter.Publish(adapter.PresentTurnRestriction(hero, TurnStartResult.FleeBattle, isPlayerSide: true));
        adapter.Publish(adapter.PresentTurnRestriction(hero, TurnStartResult.ReturnToCOMP, isPlayerSide: true));
        adapter.Publish(adapter.PresentTurnRestriction(enemy, TurnStartResult.ReturnToCOMP, isPlayerSide: false));
        adapter.Publish(adapter.PresentDemonReturnedToStock(demon));

        Assert.Collection(
            messenger.Messages,
            message => AssertMessage(message, "Hero is unable to move!", ConsoleColor.Magenta, 800),
            message => AssertMessage(message, "Hero fled in fear!", ConsoleColor.Red, 1000),
            message => AssertMessage(message, "Hero returned to COMP in terror!", ConsoleColor.Red, 400),
            message => AssertMessage(message, "Slime has fled!", ConsoleColor.Yellow, 400),
            message => AssertMessage(message, "Pixie faded away and returned to stock...", ConsoleColor.Gray, 0));
    }

    [Fact]
    public void BattleConductor_StartBattle_DoesNotRenderSuppressedFrameworkStructuralEvents()
    {
        var io = new ScriptedGameIO()
            .QueueMenu(0, 0)
            .QueueKey('x', ConsoleKey.X);
        var economy = new EconomyManager();
        var player = new Combatant("Hero", ClassType.Human)
        {
            Level = 99,
            MaxHP = 999,
            CurrentHP = 999,
            MaxSP = 99,
            CurrentSP = 99
        };
        player.CharacterStats[StatType.St] = 40;
        player.CharacterStats[StatType.Ag] = 40;
        player.CharacterStats[StatType.Lu] = 40;
        var party = new PartyManager(player);

        var enemy = new Combatant("Training Dummy", ClassType.Demon)
        {
            SourceId = "training_dummy",
            Level = 10,
            MaxHP = 1,
            CurrentHP = 1,
            MaxSP = 1,
            CurrentSP = 1
        };
        enemy.CharacterStats[StatType.Vi] = 1;
        enemy.CharacterStats[StatType.Ag] = 1;
        enemy.CharacterStats[StatType.Lu] = 1;

        var conductor = new BattleConductor(
            party,
            [enemy],
            new InventoryManager(),
            economy,
            io,
            new BattleKnowledge(),
            new CompendiumRegistry(io));

        conductor.StartBattle();

        Assert.Contains("=== ENEMY ENCOUNTER ===", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Appeared: Training Dummy (Lv.10)", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Hero attacks Training Dummy!", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("VICTORY!", io.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Battle started.", io.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Round 1 started.", io.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("started a phase", io.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("turn started", io.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Press Turn:", io.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Created Hero as", io.CombinedOutput, StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Theory]
    [InlineData(BattleEncounterEventKind.BattleFaulted)]
    [InlineData(BattleEncounterEventKind.ActionRejected)]
    [InlineData(BattleEncounterEventKind.BattleEnded)]
    public void FaultAndBattleEndEvents_AreRepresentedAsHostOwned(BattleEncounterEventKind eventKind)
    {
        var messenger = new RecordingBattleMessenger();
        var adapter = new LegacyBattleEventPresentationAdapter(messenger);

        BattleEventPresentationResult presentation = adapter.Present(Event(11, eventKind));

        Assert.Equal(BattleEventPresentationKind.HostOwned, presentation.Kind);
        Assert.Equal(eventKind, presentation.EventKind);
        Assert.Empty(messenger.Messages);
    }

    private static BattleEncounterEvent Event(int sequence, BattleEncounterEventKind kind) =>
        new(sequence, kind, kind.ToString(), RuntimeInstanceId.Parse("actor"));

    private static BattleEventPresentationKind ExpectedKind(BattleEncounterEventKind kind) =>
        kind switch
        {
            BattleEncounterEventKind.ActorCreated => BattleEventPresentationKind.Suppressed,
            BattleEncounterEventKind.BattleStarted => BattleEventPresentationKind.Suppressed,
            BattleEncounterEventKind.InitiativeRolled => BattleEventPresentationKind.Suppressed,
            BattleEncounterEventKind.RoundStarted => BattleEventPresentationKind.Suppressed,
            BattleEncounterEventKind.PhaseStarted => BattleEventPresentationKind.Suppressed,
            BattleEncounterEventKind.TurnStarted => BattleEventPresentationKind.Suppressed,
            BattleEncounterEventKind.PressTurnChanged => BattleEventPresentationKind.Suppressed,
            BattleEncounterEventKind.PhaseEnded => BattleEventPresentationKind.Suppressed,
            _ => BattleEventPresentationKind.HostOwned
        };

    private static Combatant Actor(string name, ClassType classType = ClassType.Human) =>
        new(name, classType)
        {
            Level = 10,
            MaxHP = 100,
            CurrentHP = 100,
            MaxSP = 20,
            CurrentSP = 20
        };

    private static void AssertMessage(
        BattleMessageArgs message,
        string expectedMessage,
        ConsoleColor expectedColor,
        int expectedDelay)
    {
        Assert.Equal(expectedMessage, message.Message);
        Assert.Equal(expectedColor, message.Color);
        Assert.Equal(expectedDelay, message.Delay);
        Assert.False(message.WaitForInput);
        Assert.False(message.ClearScreen);
    }

    private sealed class RecordingBattleMessenger : IBattleMessenger
    {
        public List<BattleMessageArgs> Messages { get; } = [];

        public event EventHandler<BattleMessageArgs>? OnMessagePublished;

        public void Publish(
            string message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            Combatant? analysisTarget = null,
            bool clearScreen = false)
        {
            var args = new BattleMessageArgs(message, color, delay, waitForInput, analysisTarget, clearScreen);
            Messages.Add(args);
            OnMessagePublished?.Invoke(this, args);
        }
    }
}
