using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Bridges;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Core;
using Xunit;

namespace Convergence.Tests;

public sealed class BattleCommandShellTests
{
    private readonly LegacyBattleCommandShellAdapter _shell = new();

    [Fact]
    public void BasicAttack_BecomesHostMediatedLegacyCommand()
    {
        Combatant actor = Actor("Hero");
        Combatant target = Actor("Slime", ClassType.Demon);

        BattleCommandShellResult result = _shell.CreateBasicAttack(actor, [target]);

        AssertLegacyHostCommand(
            result,
            BattleCommandShellPayloadKind.BasicAttack,
            BattleActionKind.HostSpecial,
            LegacyBattleCommandShellAdapter.LegacyBasicAttackActionId,
            ActionTurnConsumptionKind.PressTurn);
        Assert.Same(target, Assert.Single(result.Targets));
    }

    [Fact]
    public void LegacySkill_BecomesHostMediatedCommandWithSkillPayload()
    {
        Combatant actor = Actor("Hero");
        Combatant target = Actor("Slime", ClassType.Demon);
        SkillData skill = Skill("Agi");

        BattleCommandShellResult result = _shell.CreateLegacySkill(actor, skill, [target]);

        HostMediatedBattleActionCommand command = AssertLegacyHostCommand(
            result,
            BattleCommandShellPayloadKind.LegacySkill,
            BattleActionKind.HostSpecial,
            LegacyBattleCommandShellAdapter.LegacySkillActionId,
            ActionTurnConsumptionKind.PressTurn);
        Assert.Same(skill, result.Skill);
        Assert.Equal(skill.Name, command.Parameters["skill"]);
        Assert.Same(target, Assert.Single(result.Targets));
    }

    [Fact]
    public void LegacyItem_BecomesHostMediatedCommandWithItemPayload()
    {
        Combatant actor = Actor("Hero");
        Combatant target = Actor("Hero");
        ItemData item = Item("medicine", "Medicine");

        BattleCommandShellResult result = _shell.CreateLegacyItem(actor, item, [target]);

        HostMediatedBattleActionCommand command = AssertLegacyHostCommand(
            result,
            BattleCommandShellPayloadKind.LegacyItem,
            BattleActionKind.HostSpecial,
            LegacyBattleCommandShellAdapter.LegacyItemActionId,
            ActionTurnConsumptionKind.Normal);
        Assert.Same(item, result.Item);
        Assert.Equal(item.Id, command.Parameters["item"]);
        Assert.Same(target, Assert.Single(result.Targets));
    }

    [Fact]
    public void TraestoGem_ItemShellCarriesNoTurnConsumptionIntent()
    {
        Combatant actor = Actor("Hero");
        ItemData item = Item("traesto", "Traesto Gem");

        BattleCommandShellResult result = _shell.CreateLegacyItem(actor, item, []);

        AssertLegacyHostCommand(
            result,
            BattleCommandShellPayloadKind.LegacyItem,
            BattleActionKind.HostSpecial,
            LegacyBattleCommandShellAdapter.LegacyItemActionId,
            ActionTurnConsumptionKind.None);
    }

    [Fact]
    public void GuardAndPass_UseConcreteFrameworkCommands()
    {
        Combatant actor = Actor("Hero");

        BattleCommandShellResult guard = _shell.CreateGuard(actor);
        BattleCommandShellResult pass = _shell.CreatePass(actor);

        Assert.True(guard.CanExecute);
        Assert.IsType<GuardBattleActionCommand>(guard.Command);
        Assert.Equal(BattleCommandShellPayloadKind.Guard, guard.PayloadKind);
        Assert.Equal(BattleActionKind.Guard, guard.Assessment?.Kind);
        Assert.Equal(ActionTurnConsumptionKind.Normal, guard.ExpectedTurnConsumption.Kind);

        Assert.True(pass.CanExecute);
        Assert.IsType<PassBattleActionCommand>(pass.Command);
        Assert.Equal(BattleCommandShellPayloadKind.Pass, pass.PayloadKind);
        Assert.Equal(BattleActionKind.Pass, pass.Assessment?.Kind);
        Assert.Equal(ActionTurnConsumptionKind.Pass, pass.ExpectedTurnConsumption.Kind);
    }

    [Fact]
    public void Analyze_UsesConcreteFrameworkCommandWithSelectedTarget()
    {
        Combatant actor = Actor("Hero", ClassType.Operator);
        Combatant target = Actor("Slime", ClassType.Demon);

        BattleCommandShellResult result = _shell.CreateAnalyze(actor, target);

        Assert.True(result.CanExecute);
        AnalyzeBattleActionCommand command = Assert.IsType<AnalyzeBattleActionCommand>(result.Command);
        Assert.Equal(BattleCommandShellPayloadKind.Analyze, result.PayloadKind);
        Assert.Equal(BattleActionKind.Analyze, result.Assessment?.Kind);
        Assert.Equal([AnalysisLayer.Full], command.Layers);
        Assert.Same(target, Assert.Single(result.Targets));
    }

    [Fact]
    public void PersonaSwap_UsesConcreteFrameworkCommandAndAssessment()
    {
        Combatant actor = Actor("Hero", ClassType.WildCard);
        Persona active = new() { Name = "Orpheus", Level = 1 };
        Persona stock = new() { Name = "Pixie", Level = 1 };
        actor.ActivePersona = active;
        actor.PersonaStock.Add(stock);

        BattleCommandShellResult result = _shell.CreatePersonaSwap(actor, stock);

        Assert.True(result.CanExecute);
        PersonaSwapBattleActionCommand command = Assert.IsType<PersonaSwapBattleActionCommand>(result.Command);
        Assert.Equal(BattleCommandShellPayloadKind.PersonaSwap, result.PayloadKind);
        Assert.Equal(BattleActionKind.PersonaSwap, result.Assessment?.Kind);
        Assert.Equal(ActionTurnConsumptionKind.None, result.ExpectedTurnConsumption.Kind);
        Assert.Same(stock, result.Persona);
        Assert.Contains(command.PersonaInstanceId, result.Assessment?.PartyStockTransition?.AffectedInstanceIds ?? []);
        Assert.Same(active, actor.ActivePersona);
        Assert.Same(stock, Assert.Single(actor.PersonaStock));
    }

    [Fact]
    public void PersonaSwap_RejectedAssessmentDoesNotMutateLegacyActor()
    {
        Combatant actor = Actor("Hero", ClassType.WildCard);
        Persona stock = new() { Name = "Pixie", Level = 1 };
        actor.PersonaStock.Add(stock);

        BattleCommandShellResult result = _shell.CreatePersonaSwap(actor, stock);

        Assert.False(result.CanExecute);
        Assert.IsType<PersonaSwapBattleActionCommand>(result.Command);
        Assert.Equal(BattleCommandShellPayloadKind.PersonaSwap, result.PayloadKind);
        Assert.Null(actor.ActivePersona);
        Assert.Same(stock, Assert.Single(actor.PersonaStock));
    }

    [Fact]
    public void CompCommands_UseConcreteFrameworkStockCommands()
    {
        Combatant actor = Actor("Hero", ClassType.Operator);
        actor.Level = 20;
        var party = new PartyManager(actor);
        Combatant active = Demon("Pixie");
        Combatant standby = Demon("Jack Frost");
        actor.DemonStock.AddRange([active, standby]);
        Assert.True(party.SummonDemon(actor, active));

        BattleCommandShellResult summon = _shell.CreateDemonSummon(party, actor, standby);
        BattleCommandShellResult returned = _shell.CreateDemonReturn(party, actor, active);
        BattleCommandShellResult swap = _shell.CreateDemonSwap(party, actor, active, standby);

        AssertStockCommand<DemonSummonBattleActionCommand>(
            summon,
            BattleCommandShellPayloadKind.DemonSummon,
            BattleActionKind.DemonSummon,
            BattleCompActionKind.Summon);
        AssertStockCommand<DemonReturnBattleActionCommand>(
            returned,
            BattleCommandShellPayloadKind.DemonReturn,
            BattleActionKind.DemonReturn,
            BattleCompActionKind.Return);
        AssertStockCommand<DemonSwapBattleActionCommand>(
            swap,
            BattleCommandShellPayloadKind.DemonSwap,
            BattleActionKind.DemonSwap,
            BattleCompActionKind.Swap);

        Assert.Contains(active, party.ActiveParty);
        Assert.Contains(active, actor.DemonStock);
        Assert.Contains(standby, actor.DemonStock);
    }

    [Fact]
    public void TacticsAndNegotiation_UseStableHostMediatedCommands()
    {
        Combatant actor = Actor("Hero", ClassType.Operator);
        Combatant target = Demon("Slime");

        BattleCommandShellResult escape = _shell.CreateTacticsEscape(actor);
        BattleCommandShellResult strategy = _shell.CreateTacticsStrategy(actor, target);
        BattleCommandShellResult negotiation = _shell.CreateNegotiation(actor, target);

        AssertLegacyHostCommand(
            escape,
            BattleCommandShellPayloadKind.TacticsEscape,
            BattleActionKind.HostSpecial,
            LegacyBattleCommandShellAdapter.LegacyEscapeAttemptActionId,
            ActionTurnConsumptionKind.Normal);
        Assert.Equal(BattleTacticsAction.Escape, escape.TacticsAction);

        HostMediatedBattleActionCommand strategyCommand = AssertLegacyHostCommand(
            strategy,
            BattleCommandShellPayloadKind.TacticsStrategy,
            BattleActionKind.TacticsChange,
            LegacyBattleCommandShellAdapter.LegacyTacticsStrategyActionId,
            ActionTurnConsumptionKind.None);
        Assert.Equal(BattleTacticsAction.Strategy, strategy.TacticsAction);
        Assert.Equal("Slime", strategy.Targets.Single().SourceId);
        Assert.True(strategyCommand.Parameters.ContainsKey("target"));

        HostMediatedBattleActionCommand negotiationCommand = AssertLegacyHostCommand(
            negotiation,
            BattleCommandShellPayloadKind.Negotiation,
            BattleActionKind.Negotiation,
            LegacyBattleCommandShellAdapter.LegacyNegotiationActionId,
            ActionTurnConsumptionKind.Normal);
        Assert.Same(target, Assert.Single(negotiation.Targets));
        Assert.True(negotiationCommand.Parameters.ContainsKey("target"));
    }

    private static HostMediatedBattleActionCommand AssertLegacyHostCommand(
        BattleCommandShellResult result,
        BattleCommandShellPayloadKind payloadKind,
        BattleActionKind kind,
        ContentId hostActionId,
        ActionTurnConsumptionKind turnConsumption)
    {
        Assert.True(result.CanExecute);
        Assert.Equal(BattleSelectionResultKind.Selected, result.Kind);
        Assert.Equal(payloadKind, result.PayloadKind);
        HostMediatedBattleActionCommand command = Assert.IsType<HostMediatedBattleActionCommand>(result.Command);
        Assert.Equal(kind, command.Kind);
        Assert.Equal(hostActionId, command.HostActionId);
        Assert.Equal(turnConsumption, command.TurnConsumption.Kind);
        Assert.Equal(turnConsumption, result.ExpectedTurnConsumption.Kind);
        Assert.Equal(kind, result.Assessment?.Kind);
        return command;
    }

    private static void AssertStockCommand<TCommand>(
        BattleCommandShellResult result,
        BattleCommandShellPayloadKind payloadKind,
        BattleActionKind kind,
        BattleCompActionKind compKind)
        where TCommand : PartyStockBattleActionCommand
    {
        Assert.True(result.CanExecute);
        Assert.IsType<TCommand>(result.Command);
        Assert.Equal(payloadKind, result.PayloadKind);
        Assert.Equal(kind, result.Assessment?.Kind);
        Assert.Equal(ActionTurnConsumptionKind.Normal, result.ExpectedTurnConsumption.Kind);
        Assert.NotNull(result.Assessment?.PartyStockTransition);
        Assert.True(result.Assessment.PartyStockTransition.Applied);
        Assert.Equal(compKind, result.CompAction?.Kind);
    }

    private static Combatant Actor(string name, ClassType classType = ClassType.Human) =>
        new(name, classType)
        {
            SourceId = name,
            Level = 20,
            MaxHP = 100,
            CurrentHP = 100,
            MaxSP = 50,
            CurrentSP = 50
        };

    private static Combatant Demon(string sourceId) =>
        Actor(sourceId, ClassType.Demon);

    private static SkillData Skill(string name) =>
        new()
        {
            Name = name,
            Category = "Magic",
            Power = "40",
            Accuracy = "100%",
            Cost = "0 SP",
            Effect = "Deals damage."
        };

    private static ItemData Item(string id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Type = "Healing",
            EffectValue = 50,
            Description = string.Empty
        };
}
