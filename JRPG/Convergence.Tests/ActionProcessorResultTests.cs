using System;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Messaging;
using JRPGPrototype.Logic.Battle.Results;
using Xunit;

namespace Convergence.Tests;

public sealed class ActionProcessorResultTests
{
    [Fact]
    public void ExecuteSkill_ReturnsRejectedForRedundantSkill()
    {
        var processor = CreateProcessor();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target");
        var skill = CreateSkill(
            name: "Dia",
            category: "Recovery",
            power: "-",
            effect: "Restores HP to one ally.");

        BattleActionExecutionResult result = processor.ExecuteSkill(actor, new() { target }, skill);

        Assert.Equal(BattleActionExecutionKind.Rejected, result.Kind);
        Assert.Empty(result.CombatResults);
        Assert.Equal(target.MaxHP, target.CurrentHP);
    }

    [Fact]
    public void ExecuteSkill_ReturnsRejectedForMissingEffectStrategy()
    {
        var processor = CreateProcessor();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target");
        var skill = CreateSkill(
            name: "Unknown Technique",
            category: "NoSuchCategory",
            power: "-",
            effect: "No mapped battle effect.",
            cost: "10 SP");

        BattleActionExecutionResult result = processor.ExecuteSkill(actor, new() { target }, skill);

        Assert.Equal(BattleActionExecutionKind.Rejected, result.Kind);
        Assert.Empty(result.CombatResults);
        Assert.Equal(50, actor.CurrentSP);
    }

    [Fact]
    public void ExecuteSkill_ReturnsExecutedWithCombatResultsForValidSkill()
    {
        var processor = CreateProcessor();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target");
        var skill = CreateSkill(
            name: "Tarukaja",
            category: "Enhance",
            power: "-",
            effect: "Raises physical attack.");

        BattleActionExecutionResult result = processor.ExecuteSkill(actor, new() { target }, skill);

        Assert.Equal(BattleActionExecutionKind.Executed, result.Kind);
        CombatResult combatResult = Assert.Single(result.CombatResults);
        Assert.Equal(HitType.Normal, combatResult.Type);
        Assert.Equal(1, target.Buffs["PhysAtk"]);
    }

    [Fact]
    public void ExecuteItem_ReturnsEscapedForTraestoGem()
    {
        var processor = CreateProcessor();
        var actor = CreateCombatant("Actor");
        var item = CreateItem(name: "Traesto Gem", type: "Utility");

        BattleActionExecutionResult result = processor.ExecuteItem(actor, new(), item);

        Assert.Equal(BattleActionExecutionKind.Escaped, result.Kind);
        Assert.Empty(result.CombatResults);
    }

    [Fact]
    public void ExecuteItem_ReturnsExecutedForValidItem()
    {
        var processor = CreateProcessor();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target", currentHp: 20);
        var item = CreateItem(name: "Medicine", type: "Healing", effectValue: 30);

        BattleActionExecutionResult result = processor.ExecuteItem(actor, new() { target }, item);

        Assert.Equal(BattleActionExecutionKind.Executed, result.Kind);
        CombatResult combatResult = Assert.Single(result.CombatResults);
        Assert.Equal(HitType.Normal, combatResult.Type);
        Assert.Equal(50, target.CurrentHP);
    }

    [Fact]
    public void ExecuteItem_ReturnsRejectedForMissingEffectStrategy()
    {
        var processor = CreateProcessor();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target", currentHp: 20);
        var item = CreateItem(name: "Mystery Item", type: "UnknownType", effectValue: 30);

        BattleActionExecutionResult result = processor.ExecuteItem(actor, new() { target }, item);

        Assert.Equal(BattleActionExecutionKind.Rejected, result.Kind);
        Assert.Empty(result.CombatResults);
        Assert.Equal(20, target.CurrentHP);
    }

    private static ActionProcessor CreateProcessor()
    {
        var status = new StatusRegistry();
        var messenger = new RecordingBattleMessenger();
        status.SetMessenger(messenger);

        return new ActionProcessor(status, new BattleKnowledge(), messenger);
    }

    private static Combatant CreateCombatant(string name, int currentHp = 100)
    {
        return new Combatant(name)
        {
            MaxHP = 100,
            CurrentHP = currentHp,
            MaxSP = 50,
            CurrentSP = 50
        };
    }

    private static SkillData CreateSkill(
        string name,
        string category,
        string power,
        string effect,
        string cost = "0 SP")
    {
        return new SkillData
        {
            Name = name,
            Category = category,
            Power = power,
            Effect = effect,
            Accuracy = "100%",
            Cost = cost
        };
    }

    private static ItemData CreateItem(string name, string type, int effectValue = 0)
    {
        return new ItemData
        {
            Id = name,
            Name = name,
            Type = type,
            EffectValue = effectValue,
            Description = string.Empty
        };
    }

    private sealed class RecordingBattleMessenger : IBattleMessenger
    {
        public event EventHandler<BattleMessageArgs>? OnMessagePublished;

        public void Publish(
            string message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            Combatant? analysisTarget = null,
            bool clearScreen = false)
        {
            OnMessagePublished?.Invoke(
                this,
                new BattleMessageArgs(message, color, delay, waitForInput, analysisTarget, clearScreen));
        }
    }
}
