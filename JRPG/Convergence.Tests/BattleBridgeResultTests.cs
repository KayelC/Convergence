using System;
using System.Collections.Generic;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle.Bridges;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Services;
using Xunit;

namespace Convergence.Tests;

public sealed class BattleBridgeResultTests
{
    [Fact]
    public void ShowMainMenu_ReturnsBackWhenMenuIsCanceled()
    {
        var bridge = CreateBridge(new QueuedGameIO(-1), CreateCombatant("Hero"), out var actor);

        BattleMainMenuResult result = bridge.ShowMainMenu(actor);

        Assert.Equal(BattleMenuResultKind.Back, result.Kind);
        Assert.Null(result.Action);
    }

    [Theory]
    [InlineData(ClassType.Human, 0, BattleMainMenuAction.Attack)]
    [InlineData(ClassType.Human, 2, BattleMainMenuAction.UseSkill)]
    [InlineData(ClassType.Human, 3, BattleMainMenuAction.UseItem)]
    [InlineData(ClassType.PersonaUser, 2, BattleMainMenuAction.Persona)]
    [InlineData(ClassType.PersonaUser, 3, BattleMainMenuAction.Talk)]
    [InlineData(ClassType.Operator, 2, BattleMainMenuAction.UseSkill)]
    [InlineData(ClassType.Operator, 3, BattleMainMenuAction.Comp)]
    [InlineData(ClassType.Operator, 6, BattleMainMenuAction.Tactics)]
    public void ShowMainMenu_MapsClassSpecificLabelsToActions(
        ClassType classType,
        int selectedIndex,
        BattleMainMenuAction expectedAction)
    {
        var actor = CreateCombatant("Hero", classType);
        var bridge = CreateBridge(new QueuedGameIO(selectedIndex), actor, out _);

        BattleMainMenuResult result = bridge.ShowMainMenu(actor);

        Assert.Equal(BattleMenuResultKind.Selected, result.Kind);
        Assert.Equal(expectedAction, result.Action);
    }

    [Fact]
    public void SelectPersonaAction_ReturnsBackWhenMenuIsCanceled()
    {
        var actor = CreateCombatant("Hero", ClassType.WildCard);
        var bridge = CreateBridge(new QueuedGameIO(-1), actor, out _);

        BattlePersonaActionResult result = bridge.SelectPersonaAction(actor);

        Assert.Equal(BattlePersonaActionKind.Back, result.Kind);
        Assert.Null(result.SelectedSkill);
    }

    [Fact]
    public void SelectPersonaAction_ReturnsRequestSwapForWildCardChangePersona()
    {
        var actor = CreateCombatant("Hero", ClassType.WildCard);
        AddBridgeTestSkill(actor);
        var bridge = CreateBridge(new QueuedGameIO(1), actor, out _);

        BattlePersonaActionResult result = bridge.SelectPersonaAction(actor);

        Assert.Equal(BattlePersonaActionKind.RequestSwap, result.Kind);
        Assert.Null(result.SelectedSkill);
    }

    [Fact]
    public void SelectPersonaAction_ReturnsSelectedSkill()
    {
        var actor = CreateCombatant("Hero", ClassType.WildCard);
        SkillData skill = AddBridgeTestSkill(actor);
        var bridge = CreateBridge(new QueuedGameIO(0), actor, out _);

        BattlePersonaActionResult result = bridge.SelectPersonaAction(actor);

        Assert.Equal(BattlePersonaActionKind.SelectedSkill, result.Kind);
        Assert.Same(skill, result.SelectedSkill);
    }

    [Fact]
    public void OpenCOMPMenu_ReturnsBackWhenMenuIsCanceled()
    {
        var actor = CreateCombatant("Hero", ClassType.Operator);
        var bridge = CreateBridge(new QueuedGameIO(-1), actor, out _);

        BattleCompActionResult result = bridge.OpenCOMPMenu(actor);

        Assert.Equal(BattleCompActionKind.Back, result.Kind);
    }

    [Fact]
    public void OpenCOMPMenu_ReturnsSummonForStandbySelectionWhenPartyHasRoom()
    {
        var actor = CreateCombatant("Hero", ClassType.Operator);
        var standby = CreateCombatant("Pixie", ClassType.Demon);
        actor.DemonStock.Add(standby);
        var bridge = CreateBridge(new QueuedGameIO(0, 0), actor, out _);

        BattleCompActionResult result = bridge.OpenCOMPMenu(actor);

        Assert.Equal(BattleCompActionKind.Summon, result.Kind);
        Assert.Same(standby, result.Standby);
        Assert.Null(result.Active);
    }

    [Fact]
    public void OpenCOMPMenu_ReturnsSwapWhenPartyIsFull()
    {
        var actor = CreateCombatant("Hero", ClassType.Operator);
        var bridge = CreateBridge(new QueuedGameIO(0, 3, 0), actor, out _, out PartyManager party);
        var activeA = CreateCombatant("Active A", ClassType.Demon);
        var activeB = CreateCombatant("Active B", ClassType.Demon);
        var activeC = CreateCombatant("Active C", ClassType.Demon);
        var standby = CreateCombatant("Standby", ClassType.Demon);

        party.AddMember(activeA);
        party.AddMember(activeB);
        party.AddMember(activeC);
        actor.DemonStock.AddRange(new[] { activeA, activeB, activeC, standby });

        BattleCompActionResult result = bridge.OpenCOMPMenu(actor);

        Assert.Equal(BattleCompActionKind.Swap, result.Kind);
        Assert.Same(standby, result.Standby);
        Assert.Same(activeA, result.Active);
    }

    [Fact]
    public void OpenCOMPMenu_ReturnsReturnForActiveDemonSelection()
    {
        var actor = CreateCombatant("Hero", ClassType.Operator);
        var active = CreateCombatant("Active", ClassType.Demon);
        var bridge = CreateBridge(new QueuedGameIO(1, 0), actor, out _, out PartyManager party);
        party.AddMember(active);

        BattleCompActionResult result = bridge.OpenCOMPMenu(actor);

        Assert.Equal(BattleCompActionKind.Return, result.Kind);
        Assert.Null(result.Standby);
        Assert.Same(active, result.Active);
    }

    [Fact]
    public void OpenCOMPMenu_ReturnsAnalyzeForSelectedTarget()
    {
        var actor = CreateCombatant("Hero", ClassType.Operator);
        var enemy = CreateCombatant("Enemy", ClassType.Demon);
        var bridge = CreateBridge(new QueuedGameIO(2, 0), actor, out _, enemies: new List<Combatant> { enemy });

        BattleCompActionResult result = bridge.OpenCOMPMenu(actor);

        Assert.Equal(BattleCompActionKind.Analyze, result.Kind);
        Assert.Same(enemy, result.Active);
    }

    [Theory]
    [InlineData(-1, BattleMenuResultKind.Back, null)]
    [InlineData(0, BattleMenuResultKind.Selected, BattleTacticsAction.Escape)]
    [InlineData(1, BattleMenuResultKind.Selected, BattleTacticsAction.Strategy)]
    public void GetTacticsChoice_MapsMenuSelectionToResult(
        int selectedIndex,
        BattleMenuResultKind expectedKind,
        BattleTacticsAction? expectedAction)
    {
        var actor = CreateCombatant("Hero", ClassType.Operator);
        var bridge = CreateBridge(new QueuedGameIO(selectedIndex), actor, out _);

        BattleTacticsResult result = bridge.GetTacticsChoice(isBossBattle: false, isOperator: true);

        Assert.Equal(expectedKind, result.Kind);
        Assert.Equal(expectedAction, result.Action);
    }

    private static InteractionBridge CreateBridge(
        QueuedGameIO io,
        Combatant actor,
        out Combatant createdActor,
        List<Combatant>? enemies = null)
    {
        return CreateBridge(io, actor, out createdActor, out _, enemies);
    }

    private static InteractionBridge CreateBridge(
        QueuedGameIO io,
        Combatant actor,
        out Combatant createdActor,
        out PartyManager party,
        List<Combatant>? enemies = null)
    {
        createdActor = actor;
        party = new PartyManager(actor);

        return new InteractionBridge(
            io,
            party,
            new InventoryManager(),
            enemies ?? new List<Combatant>(),
            new PressTurnEngine(),
            new BattleKnowledge());
    }

    private static Combatant CreateCombatant(string name, ClassType classType = ClassType.Human)
    {
        return new Combatant(name, classType)
        {
            SourceId = name,
            MaxHP = 100,
            CurrentHP = 100,
            MaxSP = 50,
            CurrentSP = 50
        };
    }

    private static SkillData AddBridgeTestSkill(Combatant actor)
    {
        const string skillName = "Bridge Test Skill";
        var skill = new SkillData
        {
            Name = skillName,
            Effect = "Bridge test effect.",
            Power = "-",
            Accuracy = "100%",
            Cost = "0 SP",
            Category = "Enhance"
        };

        Database.Skills[skillName] = skill;
        actor.ExtraSkills.Add(skillName);

        return skill;
    }

    private sealed class QueuedGameIO : IGameIO
    {
        private readonly Queue<int> _menuSelections;

        public QueuedGameIO(params int[] menuSelections)
        {
            _menuSelections = new Queue<int>(menuSelections);
        }

        public void WriteLine(string message, ConsoleColor color = ConsoleColor.White) { }

        public void Write(string message, ConsoleColor color = ConsoleColor.White) { }

        public void Clear() { }

        public void Wait(int milliseconds) { }

        public string ReadLine() => string.Empty;

        public ConsoleKeyInfo ReadKey(bool intercept = true)
            => new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);

        public void SetForegroundColor(ConsoleColor color) { }

        public void SetBackgroundColor(ConsoleColor color) { }

        public void ResetColor() { }

        public void SetCursorVisible(bool visible) { }

        public int RenderMenu(
            string header,
            List<string> options,
            int initialIndex,
            List<bool>? disabledOptions = null,
            Action<int>? onHighlight = null,
            bool supportStatusInspect = false)
        {
            if (_menuSelections.Count == 0)
            {
                throw new InvalidOperationException("No queued menu selection was available for this test.");
            }

            return _menuSelections.Dequeue();
        }
    }
}
