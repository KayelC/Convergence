using System;
using System.Collections.Generic;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
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
    public void BuildOwnedDuplicateResultReasons_DisablesSecondParentWhenDirectResultIsAlreadyOwned()
    {
        const string resultId = "test_owned_result_aquans";
        const string raceA = "TestUxRaceA";
        const string raceB = "TestUxRaceB";
        EnsurePersonaTemplate(resultId, "Owned Result", "Element", 10);
        EnsureFusionRecipe(raceA, raceB, resultId);

        var owner = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var firstParent = CreateManualDemon("First Parent", "test_ux_first", raceA, level: 10);
        var duplicateCandidate = CreateManualDemon("Duplicate Candidate", "test_ux_second", raceB, level: 10);
        var existingResult = new Combatant("Owned Result", ClassType.Demon) { SourceId = resultId };
        owner.DemonStock.Add(firstParent);
        owner.DemonStock.Add(duplicateCandidate);
        owner.DemonStock.Add(existingResult);

        var rules = new FusionOwnershipRules(new PartyManager(owner));
        Dictionary<object, string> result = rules.BuildOwnedDuplicateResultReasons(
            owner,
            new List<object> { firstParent, duplicateCandidate },
            firstParent,
            new List<object> { firstParent });

        Assert.True(result.ContainsKey(duplicateCandidate));
        Assert.Contains("Owned Result", result[duplicateCandidate]);
    }

    [Fact]
    public void OwnershipRules_DetectOperatorOwnedResultFromActiveParty()
    {
        const string resultId = "test_active_owned_result";
        EnsurePersonaTemplate(resultId, "Active Owned", "Fairy", 10);

        var owner = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var activeOwned = CreateManualDemon("Active Owned", resultId, "Fairy", level: 10);
        owner.DemonStock.Add(activeOwned);

        var party = new PartyManager(owner);
        Assert.True(party.SummonDemon(owner, activeOwned));
        var rules = new FusionOwnershipRules(party);

        Assert.True(rules.TryGetOwnedCreateResult(owner, resultId, out FusionOwnedResult ownedResult));
        Assert.Equal("Owned Result: Active Owned", ownedResult.DisabledReason);
        Assert.Equal("Fusion aborted: that demon is already in your party or COMP.", ownedResult.TransactionAbortMessage);
    }

    [Fact]
    public void OwnershipRules_DetectWildCardOwnedPersonaResultFromStock()
    {
        const string resultId = "test_wildcard_owned_result";
        EnsurePersonaTemplate(resultId, "Owned Mask", "Magician", 10);

        var owner = new Combatant("Hero", ClassType.WildCard) { Level = 50 };
        owner.PersonaStock.Add(new Persona { Name = "Owned Mask", Race = "Magician", Level = 10 });

        var rules = new FusionOwnershipRules(new PartyManager(owner));

        Assert.True(rules.TryGetOwnedCreateResult(owner, resultId, out FusionOwnedResult ownedResult));
        Assert.Equal("Owned Result: Owned Mask", ownedResult.DisabledReason);
        Assert.Equal("Fusion aborted: that Persona is already in your stock.", ownedResult.TransactionAbortMessage);
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

        Combatant staged = CreatePreview(
            FusionOperationType.StatBoostFusion,
            pixieId,
            first,
            second,
            sacrifice: null,
            chosenSkills: new List<string>());

        Assert.Equal(target.Level, staged.Level);
        Assert.Equal(13, staged.GetStat(StatType.St));
        Assert.Equal(18, staged.GetStat(StatType.Ma));
        Assert.Equal(15, staged.GetStat(StatType.Vi));
        Assert.Equal(11, staged.GetStat(StatType.Ag));
        Assert.Equal(16, staged.GetStat(StatType.Lu));
    }

    [Fact]
    public void PreviewFactory_RankMutationCarriesStatModifiers()
    {
        const string targetId = "test_rank_preview_high_pixie";
        EnsurePersonaTemplate(targetId, "High Pixie", "Fairy", 12);

        Combatant original = CreateManualDemon("Pixie", "test_rank_preview_pixie", "Fairy", level: 10);
        original.ActivePersona!.StatModifiers[StatType.Ma] = 7;
        Combatant element = CreateManualDemon("Aeros", "test_rank_preview_aeros", "Element", level: 10);

        Combatant staged = CreatePreview(
            FusionOperationType.RankUpParent,
            targetId,
            original,
            element,
            sacrifice: null,
            chosenSkills: new List<string>());

        Assert.Equal(7, staged.ActivePersona!.StatModifiers[StatType.Ma]);
    }

    [Fact]
    public void PreviewFactory_SacrificialPreviewAppliesTransferWithoutMutatingSacrifice()
    {
        const string resultId = "test_sacrifice_preview_child";
        EnsurePersonaTemplate(resultId, "Preview Child", "Fairy", 2);

        Combatant parentA = CreateManualDemon("Parent A", "test_sacrifice_parent_a", "Fairy", level: 2);
        Combatant parentB = CreateManualDemon("Parent B", "test_sacrifice_parent_b", "Jirae", level: 2);
        Combatant sacrifice = CreateManualDemon("Sacrifice", "test_sacrifice_offer", "Beast", level: 2);
        sacrifice.LifetimeEarnedExp = 1500;

        Combatant staged = CreatePreview(
            FusionOperationType.CreateNewDemon,
            resultId,
            parentA,
            parentB,
            sacrifice,
            chosenSkills: new List<string>());

        Assert.Equal(1500, sacrifice.LifetimeEarnedExp);
        Assert.Equal(1000, staged.LifetimeEarnedExp);
    }

    [Fact]
    public void PreviewFactory_SelectedInheritedSkillsAppearOnStagedResult()
    {
        const string resultId = "test_skill_preview_child";
        EnsurePersonaTemplate(resultId, "Skill Child", "Fairy", 2);

        Combatant parentA = CreateManualDemon("Parent A", "test_skill_parent_a", "Fairy", level: 2);
        Combatant parentB = CreateManualDemon("Parent B", "test_skill_parent_b", "Jirae", level: 2);

        Combatant staged = CreatePreview(
            FusionOperationType.CreateNewDemon,
            resultId,
            parentA,
            parentB,
            sacrifice: null,
            chosenSkills: new List<string> { "Agi", "Dia" });

        Assert.Contains("Agi", staged.ExtraSkills);
        Assert.Contains("Dia", staged.ExtraSkills);
    }

    [Fact]
    public void ExecuteFusionTransaction_StandardFusionConsumesParentsWhenAllowed()
    {
        const string resultId = "test_standard_child";
        EnsurePersonaTemplate(resultId, "Standard Child", "Fairy", 2);

        var owner = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var parentA = CreateManualDemon("Parent A", "test_standard_parent_a", "Fairy", level: 10);
        var parentB = CreateManualDemon("Parent B", "test_standard_parent_b", "Jirae", level: 10);
        owner.DemonStock.Add(parentA);
        owner.DemonStock.Add(parentB);

        var party = new PartyManager(owner);
        var context = new FusionContext(
            owner,
            new List<object> { parentA, parentB },
            sacrifice: null,
            chosenSkills: new List<string>(),
            resultId: resultId,
            messenger: new CapturingFusionMessenger(),
            party: party);

        var mutator = new FusionMutator(party, new EconomyManager(), new CapturingFusionMessenger());
        mutator.ExecuteFusionTransaction(context, FusionOperationType.CreateNewDemon);

        Assert.DoesNotContain(parentA, owner.DemonStock);
        Assert.DoesNotContain(parentB, owner.DemonStock);
        Assert.Contains(owner.DemonStock, d => d.SourceId.Equals(resultId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExecuteFusionTransaction_RankMutationReplacesActiveAndStockReferences()
    {
        const string resultId = "test_rank_child";
        EnsurePersonaTemplate(resultId, "Rank Child", "Fairy", 12);

        var owner = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var original = CreateManualDemon("Rank Parent", "test_rank_parent", "Fairy", level: 10);
        var element = CreateManualDemon("Element", "test_rank_element", "Element", level: 10);
        owner.DemonStock.Add(original);
        owner.DemonStock.Add(element);

        var party = new PartyManager(owner);
        Assert.True(party.SummonDemon(owner, original));

        var context = new FusionContext(
            owner,
            new List<object> { original, element },
            sacrifice: null,
            chosenSkills: new List<string>(),
            resultId: resultId,
            messenger: new CapturingFusionMessenger(),
            party: party);

        var mutator = new FusionMutator(party, new EconomyManager(), new CapturingFusionMessenger());
        mutator.ExecuteFusionTransaction(context, FusionOperationType.RankUpParent);

        Assert.DoesNotContain(original, owner.DemonStock);
        Assert.DoesNotContain(original, party.ActiveParty);
        Assert.Contains(owner.DemonStock, d => d.SourceId.Equals(resultId, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(party.ActiveParty, d => d.SourceId.Equals(resultId, StringComparison.OrdinalIgnoreCase));
    }

    private static Combatant CreatePreview(
        FusionOperationType operation,
        string targetId,
        object p1,
        object p2,
        object? sacrifice,
        List<string> chosenSkills)
    {
        var factory = new FusionPreviewFactory();
        Combatant? staged = factory.CreatePreview(
            operation,
            targetId,
            FusionParticipant.From(p1),
            FusionParticipant.From(p2),
            sacrifice != null ? FusionParticipant.From(sacrifice) : null,
            chosenSkills);

        return Assert.IsType<Combatant>(staged);
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

    private static void EnsureFusionRecipe(string parentA, string parentB, string result)
    {
        if (Database.FusionRecipes.Exists(r =>
            r.ParentA.Equals(parentA, StringComparison.OrdinalIgnoreCase) &&
            r.ParentB.Equals(parentB, StringComparison.OrdinalIgnoreCase) &&
            r.Result.Equals(result, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Database.FusionRecipes.Add(new FusionRecipe
        {
            ParentA = parentA,
            ParentB = parentB,
            Result = result
        });
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
