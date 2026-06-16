using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.SkillSystem;

public sealed class BattleActionExecutorTests
{
    private static readonly ContentId Battle = Id("battle");
    private static readonly ContentId TeamA = Id("team_a");
    private static readonly ContentId TeamB = Id("team_b");
    private static readonly ContentId Hp = StandardProgressionIds.Hp;
    private static readonly ContentId Sp = StandardProgressionIds.Sp;

    [Fact]
    public async Task GuardAndPass_ReturnTypedTurnConsumptionWithoutPresentationCoupling()
    {
        BattleActionExecutor executor = Executor();
        BattleActorState actor = Actor("actor", TeamA);

        BattleActionExecutionResult guard = await Execute(executor, new GuardBattleActionCommand(), actor, [actor]);
        BattleActionExecutionResult pass = await Execute(executor, new PassBattleActionCommand(), actor, [actor]);

        Assert.Equal(BattleActionExecutionStatus.Executed, guard.Status);
        Assert.True(actor.IsGuarding);
        Assert.Equal(ActionTurnConsumptionKind.Normal, guard.TurnConsumption.Kind);
        Assert.Equal(ActionTurnConsumptionKind.Pass, pass.TurnConsumption.Kind);
        Assert.DoesNotContain(
            guard.Events.Concat(pass.Events),
            battleEvent => battleEvent.Message.Contains("Console", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BasicAttack_ExecutesTypedDamageAndReturnsPressTurnOutcome()
    {
        BattleActionExecutor executor = Executor();
        BattleActorState actor = Actor("actor", TeamA);
        BattleActorState target = Actor(
            "target",
            TeamB,
            hp: 40,
            defense: new CombatDefenseProfile(
                [new KeyValuePair<DamageElement, ElementalAffinity>(DamageElement.Physical, ElementalAffinity.Weak)]));
        var command = new BasicAttackBattleActionCommand(
            new EquipmentBasicAttackDefinition(DamageElement.Physical, 15, 100, false),
            SingleEnemy(),
            [target.InstanceId]);

        BattleActionExecutionResult result = await Execute(executor, command, actor, [actor, target]);

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(30, target.GetRequiredResource(Hp).Current);
        Assert.Equal(ActionTurnConsumptionKind.PressTurn, result.TurnConsumption.Kind);
        Assert.Equal(PressTurnOutcome.Weakness, result.TurnConsumption.PressTurn!.Outcome);
        Assert.Equal(ElementalAffinity.Weak, Assert.Single(result.Effects).ResolvedAffinity);
    }

    [Fact]
    public async Task SkillAction_SharesAssessmentWithExecutionAndCommitsCosts()
    {
        BattleActionExecutor executor = Executor();
        BattleActorState actor = Actor("actor", TeamA, sp: 10);
        BattleActorState target = Actor("target", TeamB);
        SkillDefinition skill = ActiveSkill(
            "frost",
            [new SkillCostDefinition(Sp, new FlatAmountDefinition(3))],
            [new DamageEffectDefinition(DamageElement.Ice, 7, 100, new NeverCriticalDefinition(), new HitCountDefinition(1, 1))]);
        var command = new SkillBattleActionCommand(skill, [target.InstanceId]);
        var request = Request(command, actor, [actor, target]);

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult result = await executor.ExecuteAsync(request);

        Assert.True(assessment.CanExecute);
        Assert.Equal(7, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(ActionTurnConsumptionKind.PressTurn, result.TurnConsumption.Kind);
    }

    [Fact]
    public async Task ItemAction_ReservesAndCommitsOnlyWhenConsumptionSucceeds()
    {
        BattleActionExecutor executor = Executor();
        BattleActorState actor = Actor("actor", TeamA);
        BattleActorState target = Actor("target", TeamA, hp: 20);
        ItemDefinition medicine = ConsumableItem("medicine", new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(medicine, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(ItemConsumptionDecision.ConsumeOne, result.ItemConsumption);
        Assert.True(result.ItemConsumptionCommitted);
        Assert.Equal(0, inventory.Quantity);
        Assert.Equal(40, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task ItemAction_DoesNotReserveWhenAssessmentRejects()
    {
        BattleActionExecutor executor = Executor();
        BattleActorState actor = Actor("actor", TeamA);
        BattleActorState target = Actor("target", TeamA, hp: 100);
        ItemDefinition medicine = ConsumableItem("medicine", new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(medicine, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(0, inventory.ReservationsCreated);
    }

    [Fact]
    public async Task ItemAction_CancellationOccursBeforeReservation()
    {
        BattleActionExecutor executor = Executor();
        BattleActorState actor = Actor("actor", TeamA);
        BattleActorState target = Actor("target", TeamA, hp: 20);
        ItemDefinition medicine = ConsumableItem("medicine", new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await executor.ExecuteAsync(
                Request(new ItemBattleActionCommand(medicine, [target.InstanceId]), actor, [actor, target], inventory),
                cancellation.Token));
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(0, inventory.ReservationsCreated);
    }

    [Fact]
    public async Task AnalyzeEscapeHostAndPartyCommands_ReturnStructuredResults()
    {
        ContentId escapeRule = Id("standard_escape");
        BattleActionExecutor executor = Executor(escapeRules: [new(escapeRule, new AlwaysEscapeRule())]);
        BattleActorState actor = Actor("actor", TeamA);
        BattleActorState target = Actor("target", TeamB);
        RuntimePartyStockSnapshot stock = PartyStock();

        BattleActionExecutionResult analyze = await Execute(
            executor,
            new AnalyzeBattleActionCommand(target.InstanceId, [AnalysisLayer.Stats]),
            actor,
            [actor, target]);
        BattleActionExecutionResult escape = await Execute(
            executor,
            new EscapeAttemptBattleActionCommand(escapeRule, 100),
            actor,
            [actor, target]);
        BattleActionExecutionResult host = await Execute(
            executor,
            new HostMediatedBattleActionCommand(BattleActionKind.TacticsChange, Id("change_strategy"), ActionTurnConsumption.None),
            actor,
            [actor]);
        BattleActionExecutionResult summon = await Execute(
            executor,
            new DemonSummonBattleActionCommand(stock, RuntimeInstanceId.Parse("demon:pixie")),
            actor,
            [actor]);

        Assert.Contains(AnalysisLayer.Stats, actor.GetAnalysis(target.InstanceId));
        Assert.True(escape.EscapeRequested);
        Assert.Equal(ActionTurnConsumptionKind.None, escape.TurnConsumption.Kind);
        Assert.Equal([Id("change_strategy")], host.HostActionRequestIds);
        Assert.NotNull(summon.PartyStockTransition);
        Assert.True(summon.PartyStockTransition.Applied);
    }

    private static BattleActionExecutionRequest Request(
        BattleActionCommand command,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        IItemActionInventory? inventory = null) =>
        new(command, actor, participants, new EffectExecutionEnvironment(Battle), inventory);

    private static Task<BattleActionExecutionResult> Execute(
        BattleActionExecutor executor,
        BattleActionCommand command,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants) =>
        executor.ExecuteAsync(Request(command, actor, participants)).AsTask();

    private static BattleActionExecutor Executor(
        IEnumerable<KeyValuePair<ContentId, IEscapeRuleHandler>>? escapeRules = null)
    {
        var services = new BattleExecutionServices(
            EmptyAilments.Instance,
            new FixedDamagePolicy(),
            new NeverInstantDeathPolicy(),
            new NeverAilmentPolicy(),
            new AlwaysChancePolicy(),
            new PowerAmountPolicy(),
            new OrderedRandomTargetPolicy(),
            escapeRuleHandlers: escapeRules);
        return new BattleActionExecutor(new SkillExecutor(services), new ItemExecutor(services), services);
    }

    private static BattleActorState Actor(
        string id,
        ContentId team,
        decimal hp = 100,
        decimal sp = 20,
        CombatDefenseProfile? defense = null) =>
        new(
            Id(id),
            Id(id + "_entity"),
            team,
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, hp, 100), new BattleResourceState(Sp, sp, 20)],
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Magic, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Agility, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Luck, 10)
            ]);

    private static SkillDefinition ActiveSkill(
        string id,
        IEnumerable<SkillCostDefinition> costs,
        IEnumerable<EffectDefinition> effects) =>
        new(
            Id(id),
            id,
            id,
            SkillActivation.Active,
            SkillMenuGroup.Offense,
            InheritanceGroup.Ice,
            new SkillInheritanceDefinition(true),
            costs: costs,
            targeting: SingleEnemy(),
            effects: effects,
            availability: new SkillAvailabilityDefinition([Battle]));

    private static ItemDefinition ConsumableItem(string id, EffectDefinition effect) =>
        new(
            Id(id),
            id,
            id,
            ItemKind.Consumable,
            99,
            10,
            new ItemUsageDefinition([Battle], SingleAlly(), [effect]));

    private static TargetingDefinition SingleEnemy() =>
        new(TargetRelation.Enemy, TargetSelection.Single, TargetLifeState.Alive, false);

    private static TargetingDefinition SingleAlly() =>
        new(TargetRelation.Ally, TargetSelection.Single, TargetLifeState.Alive, true);

    private static RuntimePartyStockSnapshot PartyStock() =>
        new(
            new RuntimeActorReferenceSnapshot(RuntimeInstanceId.Parse("actor:hero"), Id("hero"), "Hero"),
            10,
            [new RuntimeActorReferenceSnapshot(RuntimeInstanceId.Parse("actor:hero"), Id("hero"), "Hero")],
            demonStock:
            [
                new RuntimeActorReferenceSnapshot(RuntimeInstanceId.Parse("demon:pixie"), Id("pixie"), "Pixie")
            ]);

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class EmptyAilments : IAilmentDefinitionRepository
    {
        public static EmptyAilments Instance { get; } = new();
        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition)
        {
            definition = null;
            return false;
        }

        public AilmentDefinition GetRequiredAilment(ContentId id) =>
            throw new KeyNotFoundException(id.ToString());
    }

    private sealed class FixedDamagePolicy : IDamageExecutionPolicy
    {
        public IReadOnlyList<DamageHitResolution> Resolve(DamagePolicyRequest request) =>
            [new DamageHitResolution(true, 10)];
    }

    private sealed class NeverInstantDeathPolicy : IInstantDeathExecutionPolicy
    {
        public bool ShouldDefeat(InstantDeathPolicyRequest request) => false;
    }

    private sealed class NeverAilmentPolicy : IAilmentApplicationPolicy
    {
        public bool ShouldApply(AilmentApplicationPolicyRequest request) => false;
    }

    private sealed class AlwaysChancePolicy : IChanceExecutionPolicy
    {
        public bool Roll(ChancePolicyRequest request) => true;
    }

    private sealed class PowerAmountPolicy : IPowerAmountPolicy
    {
        public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => amount.Power;
    }

    private sealed class OrderedRandomTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<BattleActorState> Select(
            IReadOnlyList<BattleActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) =>
            Array.AsReadOnly(candidates.Take(count.Maximum).ToArray());
    }

    private sealed class AlwaysEscapeRule : IEscapeRuleHandler
    {
        public bool CanEscape(EscapeEffectDefinition effect, EffectExecutionContext context) => true;
    }

    private sealed class TestItemInventory(ContentId itemId, int quantity) : IItemActionInventory
    {
        public int Quantity { get; private set; } = quantity;
        public int ReservationsCreated { get; private set; }

        public bool HasAvailable(ContentId requestedItemId, int requestedQuantity) =>
            requestedItemId == itemId && Quantity >= requestedQuantity;

        public IItemActionReservation Reserve(ContentId requestedItemId, int requestedQuantity)
        {
            if (!HasAvailable(requestedItemId, requestedQuantity))
            {
                throw new InvalidOperationException("Item is unavailable.");
            }

            ReservationsCreated++;
            return new Reservation(this, requestedItemId, requestedQuantity);
        }

        private sealed class Reservation(TestItemInventory inventory, ContentId itemId, int quantity) : IItemActionReservation
        {
            public ContentId ItemId { get; } = itemId;
            public int Quantity { get; } = quantity;
            public bool IsCommitted { get; private set; }
            public bool IsRolledBack { get; private set; }

            public void Commit()
            {
                if (IsCommitted || IsRolledBack) return;
                inventory.Quantity -= Quantity;
                IsCommitted = true;
            }

            public void Rollback()
            {
                if (IsCommitted || IsRolledBack) return;
                IsRolledBack = true;
            }
        }
    }
}
