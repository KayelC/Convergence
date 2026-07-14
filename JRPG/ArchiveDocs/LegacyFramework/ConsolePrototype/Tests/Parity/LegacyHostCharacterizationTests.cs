using System;
using System.Collections.Generic;
using System.Linq;
using Convergence.Tests.TestSupport;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Host;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Fusion;
using Xunit;

namespace Convergence.Tests.Parity;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class LegacyHostCharacterizationTests
{
    [Fact]
    public void OrdinaryStartup_LoadsDataReachesFieldAndExits()
    {
        LegacyBaselineSupport.ResetLegacyState();
        var io = new ScriptedGameIO()
            .QueueKey('1', ConsoleKey.D1)
            .QueueMenu(4)
            .QueueKey('\r', ConsoleKey.Enter);

        new ConsoleGameHost(io).Run([]);

        Assert.Contains("[System] Loaded 417 skills.", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("[System] Loaded 304 entities (Personas/Demons).", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("[GAME SESSION ENDED]", io.CombinedOutput, StringComparison.Ordinal);
        GameIoMenuCall fieldMenu = Assert.Single(io.Menus);
        Assert.Contains("=== FIELD MENU ===", fieldMenu.Header, StringComparison.Ordinal);
        Assert.Equal(
            ["Explore Tartarus", "City Services", "Inventory", "Status", "Exit Game"],
            fieldMenu.Options);
        io.AssertConsumed();
    }

    [Theory]
    [InlineData('1', ClassType.Human, false, 0, 0)]
    [InlineData('2', ClassType.PersonaUser, true, 0, 0)]
    [InlineData('3', ClassType.WildCard, true, 2, 0)]
    [InlineData('4', ClassType.Operator, false, 0, 13)]
    public void ScenarioChoices_ConfigureCurrentActorModels(
        char choice,
        ClassType expectedClass,
        bool expectsActivePersona,
        int expectedPersonaStock,
        int expectedDemonStock)
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO().QueueKey(choice);
        ScenarioServices services = CreateScenarioServices(io);

        ScenarioSetupResult result = ScenarioFactory.SelectAndApplyScenario(
            services.Player,
            services.Inventory,
            services.Economy,
            io,
            services.Knowledge,
            services.Compendium);

        Assert.False(result.ShouldExit);
        Assert.False(result.JumpToDebugBattle);
        Assert.Equal(expectedClass, services.Player.Class);
        Assert.Equal(expectsActivePersona, services.Player.ActivePersona is not null);
        Assert.Equal(expectedPersonaStock, services.Player.PersonaStock.Count);
        Assert.Equal(expectedDemonStock, services.Player.DemonStock.Count);
        if (expectsActivePersona)
        {
            Assert.Equal("Orpheus", services.Player.ActivePersona!.Name);
        }
        io.AssertConsumed();
    }

    [Fact]
    public void InvalidScenarioInput_PreservesCurrentDefaultContinuation()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO().QueueKey('x');
        ScenarioServices services = CreateScenarioServices(io);

        ScenarioSetupResult result = ScenarioFactory.SelectAndApplyScenario(
            services.Player,
            services.Inventory,
            services.Economy,
            io,
            services.Knowledge,
            services.Compendium);

        Assert.Equal(ScenarioSetupResult.Continue, result);
        Assert.Equal(ClassType.Human, services.Player.Class);
        Assert.Null(services.Player.ActivePersona);
        Assert.Empty(services.Player.PersonaStock);
        Assert.Empty(services.Player.DemonStock);
    }

    [Fact]
    public void StandardPrototypeSetup_PreservesLevelResourcesAndStartingAssets()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO();
        ScenarioServices services = CreateScenarioServices(io);

        ScenarioFactory.ApplyStandardPrototypeSetup(
            services.Player,
            services.Inventory,
            services.Economy);

        Assert.Equal(80, services.Player.Level);
        Assert.Equal(5, services.Player.StatPoints);
        Assert.Equal(5000, services.Player.CurrentHP);
        Assert.Equal(5000, services.Player.CurrentSP);
        Assert.Equal(5, services.Inventory.GetQuantity("101"));
        Assert.Equal(2, services.Inventory.GetQuantity("108"));
        Assert.Equal(3, services.Inventory.GetQuantity("113"));
        Assert.Equal(3, services.Inventory.GetQuantity("114"));
        Assert.Equal("1", services.Player.EquippedWeapon?.Id);
        Assert.Equal("201", services.Player.EquippedArmor?.Id);
        Assert.Equal("301", services.Player.EquippedBoots?.Id);
        Assert.Equal("401", services.Player.EquippedAccessory?.Id);
        Assert.Equal(5_000_000, services.Economy.Macca);
        Assert.Equal(4, MoonPhaseSystem.CurrentPhase);
    }

    [Fact]
    public void AilmentDebugScenario_PreparesCurrentSkillSetAndTargetDummy()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO()
            .QueueKey('5', ConsoleKey.D5)
            .QueueKey('\r', ConsoleKey.Enter);
        ScenarioServices services = CreateScenarioServices(io);
        ScenarioSetupResult setup = ScenarioFactory.SelectAndApplyScenario(
            services.Player,
            services.Inventory,
            services.Economy,
            io,
            services.Knowledge,
            services.Compendium);
        Assert.True(setup.JumpToDebugBattle);

        PartyManager? capturedParty = null;
        List<Combatant>? capturedEnemies = null;
        DebugScenarioRunner.RunAilmentTechnicalBattle(
            services.Player,
            services.Inventory,
            services.Economy,
            io,
            services.Knowledge,
            services.Compendium,
            (party, enemies, isBoss) =>
            {
                capturedParty = party;
                capturedEnemies = enemies;
                Assert.False(isBoss);
            });

        Assert.NotNull(capturedParty);
        Combatant target = Assert.Single(capturedEnemies!);
        Assert.Equal("Target Dummy", target.Name);
        Assert.Equal(9999, target.CurrentHP);
        Assert.All(Enum.GetValues<StatType>(), stat => Assert.Equal(1, target.CharacterStats[stat]));
        Assert.Equal(
            [
                "Dormina", "Lullaby", "Shibaboo", "Binding Cry", "Bash", "Stun Needle",
                "Toxic Sting", "Venom Bite", "Patra", "Tarukaja", "Makakaja", "Sukukaja",
                "Rakukaja", "Sukunda"
            ],
            services.Player.ActivePersona!.SkillSet);
        io.AssertConsumed();
    }

    [Fact]
    public void MonteCarloScenario_IsDeterministicThroughInternalSeams()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var firstIo = new ScriptedGameIO();
        var secondIo = new ScriptedGameIO();

        MonteCarloSimulationSummary first = DebugScenarioRunner.RunMonteCarloSimulation(
            firstIo,
            250,
            new Random(714),
            new Random(915),
            waitForInput: false);
        MonteCarloSimulationSummary second = DebugScenarioRunner.RunMonteCarloSimulation(
            secondIo,
            250,
            new Random(714),
            new Random(915),
            waitForInput: false);

        Assert.Equal(first, second);
        Assert.Equal(250, first.TotalTrials);
        Assert.Equal(250, first.CurseGateTrials);
        Assert.Equal(0, first.CurseGateSuccesses);
        Assert.Contains("CURSE GATE: VERIFIED", firstIo.CombinedOutput, StringComparison.Ordinal);
        Assert.Equal(firstIo.CombinedOutput, secondIo.CombinedOutput);
    }

    [Fact]
    public void CompendiumScenario_ReachesPostBattleRegistryEvaluation()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO()
            .QueueKey('\r', ConsoleKey.Enter)
            .QueueKey('\r', ConsoleKey.Enter);
        ScenarioServices services = CreateScenarioServices(io);
        bool battleRequested = false;

        DebugScenarioRunner.RunCompendiumAutoRegistrationTest(
            services.Player,
            services.Inventory,
            services.Economy,
            io,
            services.Knowledge,
            services.Compendium,
            (party, enemies, isBoss) =>
            {
                battleRequested = true;
                Assert.False(isBoss);
                services.Compendium.RegisterDemon(Assert.Single(enemies));
            });

        Assert.True(battleRequested);
        Assert.True(services.Compendium.HasEntry("pixie"));
        Assert.Contains("[FOUND] Pixie", io.CombinedOutput, StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public void UnifiedStockScenario_PreservesOwnedAndActiveOverlap()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO().QueueKey('\r', ConsoleKey.Enter);
        ScenarioServices services = CreateScenarioServices(io);
        PartyManager? capturedParty = null;

        DebugScenarioRunner.RunUnifiedStockModelTest(
            services.Player,
            services.Inventory,
            services.Economy,
            io,
            services.Knowledge,
            services.Compendium,
            (party, enemies, isBoss) => capturedParty = party);

        Assert.NotNull(capturedParty);
        Assert.Equal(5, services.Player.DemonStock.Count);
        Assert.Equal(4, capturedParty!.ActiveParty.Count);
        Assert.Equal(3, services.Player.DemonStock.Count(capturedParty.ActiveParty.Contains));
        Assert.Contains("Overlap Count: 3 (Expected: 3)", io.CombinedOutput, StringComparison.Ordinal);
        io.AssertConsumed();
    }

    private static ScenarioServices CreateScenarioServices(ScriptedGameIO io) =>
        new(
            new Combatant("Hero"),
            new InventoryManager(),
            new EconomyManager(),
            new BattleKnowledge(),
            new CompendiumRegistry(io));

    private sealed record ScenarioServices(
        Combatant Player,
        InventoryManager Inventory,
        EconomyManager Economy,
        BattleKnowledge Knowledge,
        CompendiumRegistry Compendium);
}
