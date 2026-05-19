using System;
using System.Collections.Generic;
using System.Reflection;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field.State;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Fusion.Messaging;
using JRPGPrototype.Services;
using Xunit;

namespace Convergence.Tests;

public sealed class FusionBugRegressionTests
{
    [Fact]
    public void FinalizeRecall_DoesNotMaterializeDuplicateOperatorDemon()
    {
        var owner = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var existing = new Combatant("Pixie", ClassType.Demon) { SourceId = "pixie" };
        var snapshot = new Combatant("Pixie", ClassType.Demon) { SourceId = "PIXIE" };
        owner.DemonStock.Add(existing);

        var party = new PartyManager(owner);
        var economy = new EconomyManager();
        economy.AddMacca(5000);
        var mutator = new FusionMutator(party, economy, new CapturingFusionMessenger());

        bool recalled = mutator.FinalizeRecall(owner, snapshot, cost: 1000);

        Assert.False(recalled);
        Assert.Equal(5000, economy.Macca);
        Assert.Single(owner.DemonStock);
        Assert.Same(existing, owner.DemonStock[0]);
        Assert.DoesNotContain(snapshot, party.ActiveParty);
    }

    [Fact]
    public void ExecuteFusionTransaction_DoesNotCreateDuplicateOperatorFusionResult()
    {
        const string resultId = "test_duplicate_aquans";
        EnsurePersonaTemplate(resultId, "Aquans", "Element", 10);

        var owner = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var existing = new Combatant("Aquans", ClassType.Demon) { SourceId = resultId };
        var parentA = CreateManualDemon("Parent A", "test_parent_a", "Fairy", level: 10);
        var parentB = CreateManualDemon("Parent B", "test_parent_b", "Jirae", level: 10);
        owner.DemonStock.Add(existing);
        owner.DemonStock.Add(parentA);
        owner.DemonStock.Add(parentB);

        var party = new PartyManager(owner);
        var mutator = new FusionMutator(party, new EconomyManager(), new CapturingFusionMessenger());
        var context = new FusionContext(
            owner,
            new List<object> { parentA, parentB },
            sacrifice: null,
            chosenSkills: new List<string>(),
            resultId: resultId,
            messenger: new CapturingFusionMessenger(),
            party: party);

        mutator.ExecuteFusionTransaction(context, FusionOperationType.CreateNewDemon);

        Assert.Equal(3, owner.DemonStock.Count);
        Assert.Single(owner.DemonStock, d => d.SourceId.Equals(resultId, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parentA, owner.DemonStock);
        Assert.Contains(parentB, owner.DemonStock);
    }

    [Fact]
    public void ExecuteFusionTransaction_StatBoostConsumesMitamaCatalyst()
    {
        const string pixieId = "test_mitama_consume_pixie";
        const string mitamaId = "test_mitama_consume_kusi";
        EnsurePersonaTemplate(pixieId, "Preview Pixie", "Fairy", 2);
        EnsurePersonaTemplate(mitamaId, "Kusi Mitama", "Mitama", 10);

        var owner = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var target = CreateManualDemon("Preview Pixie", pixieId, "Fairy", level: 50);
        var mitama = CreateManualDemon("Kusi Mitama", mitamaId, "Mitama", level: 10);
        owner.DemonStock.Add(target);
        owner.DemonStock.Add(mitama);

        var party = new PartyManager(owner);
        Assert.True(party.SummonDemon(owner, target));
        Assert.True(party.SummonDemon(owner, mitama));

        var mutator = new FusionMutator(party, new EconomyManager(), new CapturingFusionMessenger());
        var context = new FusionContext(
            owner,
            new List<object> { mitama, target },
            sacrifice: null,
            chosenSkills: new List<string>(),
            resultId: pixieId,
            messenger: new CapturingFusionMessenger(),
            party: party);

        mutator.ExecuteFusionTransaction(context, FusionOperationType.StatBoostFusion);

        Assert.DoesNotContain(mitama, owner.DemonStock);
        Assert.DoesNotContain(mitama, party.ActiveParty);
        Assert.DoesNotContain(target, owner.DemonStock);
        Assert.DoesNotContain(target, party.ActiveParty);
        Assert.Single(owner.DemonStock, d => d.SourceId.Equals(pixieId, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(party.ActiveParty, d => d.SourceId.Equals(pixieId, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateStagedDemon_StatBoostPreviewUsesNonMitamaLevelAndStats(bool mitamaFirst)
    {
        const string pixieId = "test_preview_pixie";
        const string mitamaId = "test_preview_kusi_mitama";
        EnsurePersonaTemplate(pixieId, "Preview Pixie", "Fairy", 2);
        EnsurePersonaTemplate(mitamaId, "Kusi Mitama", "Mitama", 10);

        Combatant target = CreateManualDemon("Preview Pixie", pixieId, "Fairy", level: 50);
        target.ActivePersona!.StatModifiers[StatType.St] = 13;
        target.ActivePersona.StatModifiers[StatType.Ma] = 18;
        target.ActivePersona.StatModifiers[StatType.Vi] = 13;
        target.ActivePersona.StatModifiers[StatType.Ag] = 10;
        target.ActivePersona.StatModifiers[StatType.Lu] = 16;

        Combatant mitama = CreateManualDemon("Kusi Mitama", mitamaId, "Mitama", level: 10);
        object first = mitamaFirst ? mitama : target;
        object second = mitamaFirst ? target : mitama;

        Combatant staged = InvokeCreateStagedDemon(
            FusionOperationType.StatBoostFusion,
            pixieId,
            first,
            second);

        Assert.Equal(target.Level, staged.Level);
        Assert.Equal(13, staged.GetStat(StatType.St));
        Assert.Equal(18, staged.GetStat(StatType.Ma));
        Assert.Equal(15, staged.GetStat(StatType.Vi));
        Assert.Equal(11, staged.GetStat(StatType.Ag));
        Assert.Equal(16, staged.GetStat(StatType.Lu));
    }

    private static Combatant InvokeCreateStagedDemon(FusionOperationType operation, string targetId, object p1, object p2)
    {
        var io = new FakeGameIO();
        var player = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var conductor = new FusionConductor(
            io,
            player,
            new PartyManager(player),
            new EconomyManager(),
            new FieldUIState(),
            new CompendiumRegistry(io));

        MethodInfo method = typeof(FusionConductor).GetMethod(
            "CreateStagedDemon",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = method.Invoke(conductor, new object?[]
        {
            operation,
            targetId,
            p1,
            p2,
            null,
            new List<string>()
        });

        return Assert.IsType<Combatant>(result);
    }

    private static Combatant CreateManualDemon(string name, string sourceId, string race, int level)
    {
        var demon = new Combatant(name, ClassType.Demon)
        {
            SourceId = sourceId,
            Level = level,
            ActivePersona = new Persona
            {
                Name = name,
                Race = race,
                Level = level
            }
        };

        foreach (StatType stat in Enum.GetValues(typeof(StatType)))
        {
            demon.CharacterStats[stat] = 0;
            demon.ActivePersona.StatModifiers[stat] = 0;
        }

        return demon;
    }

    private static void EnsurePersonaTemplate(string id, string name, string race, int level)
    {
        Database.Personas[id] = new PersonaData
        {
            Id = id,
            Name = name,
            Race = race,
            Rank = 1,
            Level = level,
            RawStats = new Dictionary<string, int>
            {
                ["St"] = 1,
                ["Ma"] = 1,
                ["Vi"] = 1,
                ["Ag"] = 1,
                ["Lu"] = 1
            },
            RawAffinities = new Dictionary<string, string>(),
            BaseSkills = new List<string>(),
            LearnedSkillsRaw = new Dictionary<string, string>(),
            FamiliarDialogue = string.Empty
        };
    }

    private sealed class CapturingFusionMessenger : IFusionMessenger
    {
        public event EventHandler<FusionMessageArgs>? OnMessagePublished;

        public void Publish(string? message, ConsoleColor color = ConsoleColor.Gray, int delay = 0, bool waitForInput = false, bool clearScreen = false)
            => OnMessagePublished?.Invoke(this, new FusionMessageArgs(message, color, delay, waitForInput, clearScreen));
    }

    private sealed class FakeGameIO : IGameIO
    {
        public void WriteLine(string message, ConsoleColor color = ConsoleColor.White) { }
        public void Write(string message, ConsoleColor color = ConsoleColor.White) { }
        public void Clear() { }
        public void Wait(int milliseconds) { }
        public string ReadLine() => string.Empty;
        public ConsoleKeyInfo ReadKey(bool intercept = true) => new ConsoleKeyInfo('\0', ConsoleKey.Enter, false, false, false);
        public void SetForegroundColor(ConsoleColor color) { }
        public void SetBackgroundColor(ConsoleColor color) { }
        public void ResetColor() { }
        public void SetCursorVisible(bool visible) { }
        public int RenderMenu(string header, List<string> options, int initialIndex, List<bool>? disabledOptions = null, Action<int>? onHighlight = null, bool supportStatusInspect = false) => -1;
    }
}
