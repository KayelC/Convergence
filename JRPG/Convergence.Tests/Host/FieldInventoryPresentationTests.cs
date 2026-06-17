using System;
using System.Collections.Generic;
using Convergence.Tests.TestSupport;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Field.Bridges;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Field.Engines;
using JRPGPrototype.Logic.Field.Messaging;
using JRPGPrototype.Logic.Field.State;
using Xunit;

namespace Convergence.Tests.Host;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class FieldInventoryPresentationTests
{
    [Fact]
    public void ItemSelection_ReturnsUnavailableWhenNoUsableItemsRemain()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var player = new Combatant("Hero");
        var io = new ScriptedGameIO();
        var bridge = new InventoryUIBridge(io, new FieldUIState(), new InventoryManager(), new PartyManager(player));

        FieldItemSelectionResult result = bridge.SelectItemResult(player, inDungeon: false);

        Assert.Equal(FieldSelectionResultKind.Unavailable, result.Kind);
        Assert.Null(result.Item);
        Assert.Equal("No usable items remaining.", Assert.Single(io.Writes).Text);
        Assert.Equal(800, Assert.Single(io.Waits));
        Assert.Empty(io.Menus);
    }

    [Fact]
    public void ItemSelection_ReturnsTypedSelectionAndPreservesDisabledLabels()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var player = new Combatant("Hero");
        var inventory = new InventoryManager();
        inventory.AddItem("101", 1);
        inventory.AddItem("113", 1);
        inventory.AddItem("114", 1);
        var io = new ScriptedGameIO().QueueMenu(0);
        var bridge = new InventoryUIBridge(io, new FieldUIState(), inventory, new PartyManager(player));

        FieldItemSelectionResult result = bridge.SelectItemResult(player, inDungeon: false);

        Assert.Equal(FieldSelectionResultKind.Selected, result.Kind);
        Assert.Equal("Medicine", result.Item?.Name);
        Assert.Equal(
            [
                $"{"Medicine",-20} x1",
                $"{"Traesto Gem",-20} x1 [BATTLE ONLY]",
                $"{"Goho-M",-20} x1 [DUNGEON ONLY]",
                "Back"
            ],
            Assert.Single(io.Menus).Options);
        Assert.Equal([false, true, true, false], io.Menus[0].DisabledOptions);
        io.AssertConsumed();
    }

    [Fact]
    public void FieldSelections_ReturnTypedBackResultsWithoutLegacyNullGuessing()
    {
        var player = new Combatant("Hero") { CurrentHP = 40, MaxHP = 50, CurrentSP = 20, MaxSP = 30 };
        var ally = new Combatant("Ally") { CurrentHP = 10, MaxHP = 30, CurrentSP = 5, MaxSP = 10 };
        var io = new ScriptedGameIO()
            .QueueMenu(-1)
            .QueueMenu(-1);
        var party = new PartyManager(player);
        party.AddMember(ally);
        var bridge = new InventoryUIBridge(io, new FieldUIState(), new InventoryManager(), party);

        FieldSkillPerformerSelectionResult performer = bridge.SelectSkillPerformerResult(player);
        FieldTargetSelectionResult target = bridge.SelectFieldTargetResult(player, "Medicine");

        Assert.Equal(FieldSelectionResultKind.Back, performer.Kind);
        Assert.Equal(FieldSelectionResultKind.Back, target.Kind);
        Assert.Null(performer.Performer);
        Assert.Null(target.Target);
        Assert.Equal("Who is performing the skill?", io.Menus[0].Header);
        Assert.Equal("Using Medicine. Select Target:", io.Menus[1].Header);
        io.AssertConsumed();
    }

    [Fact]
    public void FieldSkillSelection_ReturnsUnavailableOrSelectedWithCurrentLabels()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var performer = new Combatant("Yukari") { CurrentSP = 10, MaxSP = 20 };
        var unavailableBridge = new InventoryUIBridge(
            new ScriptedGameIO(),
            new FieldUIState(),
            new InventoryManager(),
            new PartyManager(performer));

        FieldSkillSelectionResult unavailable = unavailableBridge.SelectFieldSkillResult(performer);
        Assert.Equal(FieldSelectionResultKind.Unavailable, unavailable.Kind);

        performer.ExtraSkills.Add("Dia");
        var io = new ScriptedGameIO().QueueMenu(0);
        var bridge = new InventoryUIBridge(io, new FieldUIState(), new InventoryManager(), new PartyManager(performer));

        FieldSkillSelectionResult selected = bridge.SelectFieldSkillResult(performer);

        Assert.Equal(FieldSelectionResultKind.Selected, selected.Kind);
        Assert.Equal("Dia", selected.Skill?.Name);
        Assert.Equal([$"{"Dia",-15} (4 SP)", "Back"], Assert.Single(io.Menus).Options);
    }

    [Fact]
    public void ItemExecution_UsesAssessmentForConsumptionAndPresentationEvents()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var inventory = new InventoryManager();
        var messenger = new RecordingFieldMessenger();
        var dungeon = new DungeonState { CurrentFloor = 5 };
        var engine = Engine(inventory, messenger, dungeon);
        var target = new Combatant("Medic") { MaxHP = 100, CurrentHP = 100, MaxSP = 20, CurrentSP = 20 };
        ItemData medicine = Database.Items["101"];
        inventory.AddItem("101", 1);

        FieldUseExecutionResult failed = engine.ExecuteItemUsageDetailed(medicine, target, target);

        Assert.False(failed.Applied);
        Assert.False(failed.ConsumeItem);
        Assert.Equal(FieldUseExecutionReason.FullHp, failed.Reason);
        Assert.Equal(1, inventory.GetQuantity("101"));
        Assert.Equal($"{target.Name}'s HP is already full.", Assert.Single(failed.PresentationEvents).Message);

        target.CurrentHP = 50;
        FieldUseExecutionResult applied = engine.ExecuteItemUsageDetailed(medicine, target, target);

        Assert.True(applied.Applied);
        Assert.True(applied.ConsumeItem);
        Assert.Equal(ItemUsageResult.Applied, applied.LegacyResult);
        Assert.Equal(0, inventory.GetQuantity("101"));
        Assert.Equal(100, target.CurrentHP);
        Assert.Equal([$"{target.Name}'s HP is already full.", $"{target.Name} recovered health.", null], messenger.Messages);
        Assert.Equal([0, 0, 800], messenger.Delays);
    }

    [Fact]
    public void ItemExecution_PreservesCureGohoAndUnsupportedFieldBehavior()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var inventory = new InventoryManager();
        var messenger = new RecordingFieldMessenger();
        var dungeon = new DungeonState { CurrentFloor = 7 };
        var engine = Engine(inventory, messenger, dungeon);
        var target = new Combatant("Hero") { MaxHP = 100, CurrentHP = 1, MaxSP = 20, CurrentSP = 20 };
        target.InflictAilment(new AilmentData { Name = "Poison", CureKeyword = "Poison" });

        inventory.AddItem("112", 1);
        FieldUseExecutionResult cure = engine.ExecuteItemUsageDetailed(Database.Items["112"], target, target);

        Assert.True(cure.Applied);
        Assert.True(cure.ConsumeItem);
        Assert.Null(target.CurrentAilment);
        Assert.Equal(0, inventory.GetQuantity("112"));
        Assert.Equal($"{target.Name} was cured of their ailment!", cure.PresentationEvents[0].Message);

        inventory.AddItem("109", 1);
        FieldUseExecutionResult revive = engine.ExecuteItemUsageDetailed(Database.Items["109"], target, target);

        Assert.False(revive.Applied);
        Assert.False(revive.ConsumeItem);
        Assert.Equal(FieldUseExecutionReason.UnsupportedFieldUse, revive.Reason);
        Assert.Equal(1, inventory.GetQuantity("109"));
        Assert.Empty(revive.PresentationEvents);

        inventory.AddItem("114", 1);
        FieldUseExecutionResult goho = engine.ExecuteItemUsageDetailed(Database.Items["114"], target, target);

        Assert.True(goho.Applied);
        Assert.True(goho.ConsumeItem);
        Assert.Equal(ItemUsageResult.RequestDungeonExit, goho.LegacyResult);
        Assert.Equal(0, inventory.GetQuantity("114"));
        Assert.Equal(1, dungeon.CurrentFloor);
        Assert.Equal("Using Goho-M... A mystical light surrounds the party.", goho.PresentationEvents[0].Message);
    }

    [Fact]
    public void SkillExecution_AssessesCostNoEffectAndSuccessBeforeMutation()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var messenger = new RecordingFieldMessenger();
        var engine = Engine(new InventoryManager(), messenger, new DungeonState());
        SkillData dia = Database.Skills["Dia"];
        var user = new Combatant("Yukari") { CurrentSP = 3, MaxSP = 20 };
        var target = new Combatant("Hero") { MaxHP = 100, CurrentHP = 20, MaxSP = 20, CurrentSP = 20 };

        FieldUseExecutionResult insufficient = engine.ExecuteSkillUsageDetailed(dia, user, target);

        Assert.False(insufficient.Applied);
        Assert.Equal(FieldUseExecutionReason.InsufficientSp, insufficient.Reason);
        Assert.Equal(3, user.CurrentSP);
        Assert.Equal(20, target.CurrentHP);

        user.CurrentSP = 10;
        target.CurrentHP = 100;
        FieldUseExecutionResult redundant = engine.ExecuteSkillUsageDetailed(dia, user, target);

        Assert.False(redundant.Applied);
        Assert.Equal(FieldUseExecutionReason.NoEffect, redundant.Reason);
        Assert.Equal(10, user.CurrentSP);

        target.CurrentHP = 20;
        FieldUseExecutionResult applied = engine.ExecuteSkillUsageDetailed(dia, user, target);

        Assert.True(applied.Applied);
        Assert.False(applied.ConsumeItem);
        Assert.Equal(6, user.CurrentSP);
        Assert.Equal(70, target.CurrentHP);
        Assert.Equal(["Yukari does not have enough SP.", "This action would have no effect.", "Hero was healed.", null], messenger.Messages);
    }

    private static FieldServiceEngine Engine(
        InventoryManager inventory,
        IFieldMessenger messenger,
        DungeonState dungeon) =>
        new(
            messenger,
            new ScriptedGameIO(),
            new EconomyManager(),
            inventory,
            new PartyManager(new Combatant("Hero")),
            dungeon);

    private sealed class RecordingFieldMessenger : IFieldMessenger
    {
        public event EventHandler<FieldMessageArgs>? OnMessagePublished;
        public List<string?> Messages { get; } = [];
        public List<int> Delays { get; } = [];

        public void Publish(
            string? message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false)
        {
            Messages.Add(message);
            Delays.Add(delay);
            OnMessagePublished?.Invoke(this, new FieldMessageArgs(message, color, delay, waitForInput, clearScreen));
        }
    }
}
