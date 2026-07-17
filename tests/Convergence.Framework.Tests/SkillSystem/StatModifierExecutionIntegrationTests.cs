using Convergence.Battle;
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Hosting;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class StatModifierExecutionIntegrationTests
{
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId Attack = ContentId.Parse("attack");
    private static readonly ContentId Defense = ContentId.Parse("defense");
    private static readonly ContentId OwnerTurnEnd = ContentId.Parse("owner_turn_end");

    [Fact]
    public void SkillExecution_UsesTheSelectedApplyPathAndReturnsTypedTransitions()
    {
        var policy = new RecordingStatModifierPolicyService(PersistentPolicy());
        RuntimeActorState actor = Actor("actor");
        SkillDefinition skill = ActiveSkill(
            "dual_focus",
            [new ModifyStatStageEffectDefinition([Attack, Defense], 1)]);
        var executor = new SkillExecutor(Services(policy));

        SkillExecutionResult result = executor.Execute(Request(skill, actor));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        EffectExecutionResult effect = Assert.Single(result.Effects);
        Assert.Equal(EffectExecutionOutcome.Success, effect.Outcome);
        Assert.Equal(2, effect.StatModifierTransitions.Count);
        Assert.All(effect.StatModifierTransitions, transition =>
        {
            Assert.Equal(StatModifierTransitionCode.Applied, transition.Code);
            Assert.Contains(transition.Events, value =>
                value.Kind == StatModifierEventKind.AggregateStageChanged);
        });
        Assert.Equal(2, policy.ApplyCalls);
        Assert.True(policy.AssessCalls >= 2);
        Assert.Equal(policy.PolicyId, actor.StatModifierState?.PolicyId);
        Assert.Equal(1, actor.StatStages[Attack].Stage);
        Assert.Equal(1, actor.StatStages[Defense].Stage);
    }

    [Fact]
    public void MultiTrackExecution_RejectionDoesNotCommitAnEarlierAcceptedTrack()
    {
        var policy = new RejectSecondApplyPolicyService(PersistentPolicy(), Defense);
        RuntimeActorState actor = Actor("actor");
        SkillDefinition skill = ActiveSkill(
            "atomic_focus",
            [new ModifyStatStageEffectDefinition([Attack, Defense], 1)]);

        SkillExecutionResult result = new SkillExecutor(Services(policy)).Execute(Request(skill, actor));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        EffectExecutionResult effect = Assert.Single(result.Effects);
        Assert.Equal(EffectExecutionOutcome.Failure, effect.Outcome);
        Assert.Equal(2, policy.ApplyCalls);
        Assert.Null(actor.StatModifierState);
        Assert.Empty(actor.StatStages);
    }

    [Fact]
    public void TimedExclusive_WeakerSkillIsRejectedBeforeItsCostCanCommit()
    {
        IStatModifierPolicyService policy = TimedExclusivePolicy();
        RuntimeActorState actor = Actor("actor", sp: 20);
        var executor = new SkillExecutor(Services(policy));
        SkillDefinition strong = ActiveSkill(
            "strong_focus",
            [new ModifyStatStageEffectDefinition([Attack], 2, Turns(3))]);
        SkillDefinition weak = ActiveSkill(
            "weak_focus",
            [new ModifyStatStageEffectDefinition([Attack], 1, Turns(3))],
            [new SkillCostDefinition(Sp, new FlatAmountDefinition(5))]);

        SkillExecutionResult applied = executor.Execute(Request(
            strong,
            actor,
            new StatModifierLifecycleBoundary(OwnerTurnEnd, 1)));
        decimal beforeSp = actor.GetRequiredResource(Sp).Current;
        SkillExecutionRequest weakRequest = Request(
            weak,
            actor,
            new StatModifierLifecycleBoundary(OwnerTurnEnd, 2));
        SkillExecutionAssessment assessment = executor.Assess(weakRequest);
        SkillExecutionResult rejected = executor.Execute(weakRequest, assessment);

        Assert.Equal(SkillExecutionStatus.Executed, applied.Status);
        Assert.False(assessment.CanExecute);
        Assert.Contains(assessment.Diagnostics, value =>
            value.Code == SkillExecutionDiagnosticCode.NoApplicableEffect);
        Assert.Equal(SkillExecutionStatus.Rejected, rejected.Status);
        Assert.Equal(beforeSp, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(2, actor.StatStages[Attack].Stage);
    }

    [Fact]
    public void TimedExclusive_EqualItemRefreshIsMeaningfulAndRequestsConsumption()
    {
        IStatModifierPolicyService policy = TimedExclusivePolicy();
        RuntimeActorState actor = Actor("actor");
        var skillExecutor = new SkillExecutor(Services(policy));
        SkillDefinition initial = ActiveSkill(
            "initial_focus",
            [new ModifyStatStageEffectDefinition([Attack], 1, Turns(2))]);
        skillExecutor.Execute(Request(
            initial,
            actor,
            new StatModifierLifecycleBoundary(OwnerTurnEnd, 1)));
        ItemDefinition item = Consumable(
            "focus_tonic",
            new ModifyStatStageEffectDefinition([Attack], 1, Turns(3)));
        var itemExecutor = new ItemExecutor(Services(policy));
        var request = new ItemExecutionRequest(
            item,
            actor,
            [actor],
            Environment(new StatModifierLifecycleBoundary(OwnerTurnEnd, 2)),
            [actor.InstanceId]);

        ItemExecutionAssessment assessment = itemExecutor.Assess(request);
        ItemExecutionResult result = itemExecutor.Execute(request, assessment);

        Assert.True(assessment.CanExecute);
        Assert.Equal(ItemExecutionStatus.Executed, result.Status);
        Assert.Equal(ItemConsumptionDecision.ConsumeOne, result.Consumption);
        Assert.Equal(1, actor.StatStages[Attack].Stage);
        RuntimeStatModifierContributionSnapshot contribution = Assert.Single(
            Assert.Single(actor.StatModifierState!.Tracks).Contributions);
        Assert.Equal(Turns(3), contribution.Duration);
        Assert.Equal(2, contribution.LastLifecycleBoundary?.Sequence);
        Assert.Contains(
            Assert.Single(result.Effects).StatModifierTransitions.SelectMany(value => value.Events),
            value => value.Kind == StatModifierEventKind.ContributionUpdated);
    }

    [Fact]
    public void PassiveApplication_UsesItsBoundaryAndExpiresOnTheNextOwnerTurn()
    {
        IStatModifierPolicyService policy = TimedContributionPolicy();
        SkillDefinition passive = PassiveSkill(
            "opening_focus",
            ContentId.Parse("battle_start"),
            new ModifyStatStageEffectDefinition([Attack], 1, Turns(1)));
        RuntimeActorState actor = Actor("actor", passiveSkills: [passive]);
        BattleExecutionServices services = Services(policy);
        StatModifierLifecycleBoundary appliedAt = new(OwnerTurnEnd, 1);

        PassiveTriggerDispatchResult dispatch = services.PassiveTriggers.Dispatch(
            new PassiveTriggerDispatchRequest(
                ContentId.Parse("battle_start"),
                actor,
                [actor],
                [actor],
                Battle,
                NormalBattle,
                moonPhaseId: null,
                [appliedAt]),
            services);
        var lifecycle = new BattleStatusLifecycleService(new MinimumRandomSource());
        BattleTurnEndLifecycleResult sameBoundary = lifecycle.ProcessTurnEnd(
            new BattleTurnEndLifecycleRequest(
                actor,
                [actor],
                Battle,
                OwnerTurnEnd,
                NormalBattle,
                statModifierBoundary: appliedAt),
            services);
        int stageAfterSameBoundary = actor.StatStages[Attack].Stage;
        BattleTurnEndLifecycleResult nextBoundary = lifecycle.ProcessTurnEnd(
            new BattleTurnEndLifecycleRequest(
                actor,
                [actor],
                Battle,
                OwnerTurnEnd,
                NormalBattle,
                statModifierBoundary: new StatModifierLifecycleBoundary(OwnerTurnEnd, 2)),
            services);

        Assert.Equal(PassiveTriggerOutcome.Executed, Assert.Single(dispatch.Activations).Outcome);
        Assert.Equal(1, stageAfterSameBoundary);
        Assert.DoesNotContain(sameBoundary.Events, value => value.ModifierEvent is not null);
        Assert.Empty(actor.StatStages);
        Assert.Contains(nextBoundary.Events, value =>
            value.ModifierEvent?.Kind == StatModifierEventKind.ContributionExpired);
    }

    [Fact]
    public void StatusRemovalAndCleanup_UseTheSelectedPolicyAuthority()
    {
        IStatModifierPolicyService policy = PersistentPolicy();
        BattleExecutionServices services = Services(policy);
        var executor = new SkillExecutor(services);
        RuntimeActorState actor = Actor("actor");
        executor.Execute(Request(ActiveSkill(
            "mixed_stages",
            [
                new ModifyStatStageEffectDefinition([Attack], 2),
                new ModifyStatStageEffectDefinition([Defense], -1)
            ]), actor));

        SkillExecutionResult removePositive = executor.Execute(Request(ActiveSkill(
            "clear_positive",
            [new RemoveStatusEffectDefinition([StatusEffectKind.Buff], [])]), actor));

        Assert.DoesNotContain(Attack, actor.StatStages.Keys);
        Assert.Equal(-1, actor.StatStages[Defense].Stage);
        Assert.Contains(
            Assert.Single(removePositive.Effects).StatModifierTransitions.SelectMany(value => value.Events),
            value => value.Kind == StatModifierEventKind.ContributionRemoved);

        var lifecycle = new BattleStatusLifecycleService(new MinimumRandomSource());
        lifecycle.Cleanup(
            new BattleStatusCleanupRequest(actor, BattleStatusCleanupScope.Swap),
            policy);
        Assert.Equal(-1, actor.StatStages[Defense].Stage);
        lifecycle.Cleanup(
            new BattleStatusCleanupRequest(actor, BattleStatusCleanupScope.BattleEnd),
            policy);
        Assert.Empty(actor.StatStages);
    }

    [Fact]
    public void PassiveDispatchRequest_RejectsInvalidOrDuplicateModifierBoundaries()
    {
        RuntimeActorState actor = Actor("actor");
        StatModifierLifecycleBoundary boundary = new(OwnerTurnEnd, 1);

        Assert.Throws<ArgumentException>(() => new PassiveTriggerDispatchRequest(
            ContentId.Parse("battle_start"),
            actor,
            [actor],
            [actor],
            Battle,
            NormalBattle,
            moonPhaseId: null,
            [boundary, boundary]));
    }

    private static SkillExecutionRequest Request(
        SkillDefinition skill,
        RuntimeActorState actor,
        StatModifierLifecycleBoundary? boundary = null) =>
        new(
            skill,
            actor,
            [actor],
            Environment(boundary),
            [actor.InstanceId]);

    private static EffectExecutionEnvironment Environment(
        StatModifierLifecycleBoundary? boundary = null) =>
        new(
            Battle,
            NormalBattle,
            activeStatModifierBoundaries: boundary is null ? [] : [boundary]);

    private static SkillDefinition ActiveSkill(
        string id,
        IEnumerable<EffectDefinition> effects,
        IEnumerable<SkillCostDefinition>? costs = null) =>
        new(
            ContentId.Parse(id),
            id,
            id,
            SkillActivation.Active,
            SkillMenuGroup.Buff,
            InheritanceGroup.Support,
            new SkillInheritanceDefinition(true),
            costs: costs,
            targeting: SelfTargeting(),
            effects: effects,
            availability: new SkillAvailabilityDefinition([Battle]));

    private static SkillDefinition PassiveSkill(
        string id,
        ContentId eventId,
        EffectDefinition effect) =>
        new(
            ContentId.Parse(id),
            id,
            id,
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            triggers: [new PassiveTriggerDefinition(eventId, [effect])]);

    private static ItemDefinition Consumable(string id, EffectDefinition effect) =>
        new(
            ContentId.Parse(id),
            id,
            id,
            ItemKind.Consumable,
            99,
            1,
            new ItemUsageDefinition([Battle], SelfTargeting(), [effect]));

    private static TargetingDefinition SelfTargeting() =>
        new(TargetRelation.Self, TargetSelection.Single, TargetLifeState.Alive, true);

    private static RuntimeActorState Actor(
        string id,
        decimal sp = 20,
        IEnumerable<SkillDefinition>? passiveSkills = null) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            PlayerTeam,
            Hp,
            CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, 100, 100), new BattleResourceState(Sp, sp, 100)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_host"), PlayerTeam),
            passiveSkills: passiveSkills);

    private static BattleExecutionServices Services(IStatModifierPolicyService statModifiers)
    {
        var ruleset = new ProductionCombatRuleset(new MinimumRandomSource());
        return new BattleExecutionServices(
            EmptyAilments.Instance,
            ruleset,
            ruleset,
            ruleset,
            ruleset,
            ruleset,
            new FirstSkillTargetPolicy(),
            new OrderedRuntimeTargetSelectionPolicy(),
            statModifiers);
    }

    private static IStatModifierPolicyService PersistentPolicy() =>
        new StatModifierPolicyService(new PersistentStagedStatModifierPolicy(
            ContentId.Parse("integration_persistent")));

    private static IStatModifierPolicyService TimedExclusivePolicy() =>
        new StatModifierPolicyService(new TimedExclusiveStatModifierPolicy(
            ContentId.Parse("integration_timed_exclusive")));

    private static IStatModifierPolicyService TimedContributionPolicy() =>
        new StatModifierPolicyService(new TimedContributionStatModifierPolicy(
            ContentId.Parse("integration_timed_contribution")));

    private static TurnDurationDefinition Turns(int value) =>
        new(value, OwnerTurnEnd, SuspendWhileReserve: true);

    private sealed class EmptyAilments : IAilmentDefinitionRepository
    {
        internal static EmptyAilments Instance { get; } = new();

        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition)
        {
            definition = null;
            return false;
        }

        public AilmentDefinition GetRequiredAilment(ContentId id) =>
            throw new KeyNotFoundException(id.ToString());
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;
        public decimal NextUnitDecimal() => 0m;
    }

    private sealed class FirstSkillTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) =>
            Array.AsReadOnly(candidates.Take(count.Minimum).ToArray());
    }

    private class RecordingStatModifierPolicyService : IStatModifierPolicyService
    {
        protected readonly IStatModifierPolicyService Inner;

        internal RecordingStatModifierPolicyService(IStatModifierPolicyService inner)
        {
            Inner = inner;
        }

        internal int AssessCalls { get; private set; }
        internal int ApplyCalls { get; private set; }
        public ContentId PolicyId => Inner.PolicyId;
        public StatModifierValidationResult ValidateState(RuntimeStatModifierStateSnapshot state) =>
            Inner.ValidateState(state);

        public StatModifierTransitionResult AssessApplication(StatModifierApplicationRequest request)
        {
            AssessCalls++;
            return Inner.AssessApplication(request);
        }

        public virtual StatModifierTransitionResult Apply(StatModifierApplicationRequest request)
        {
            ApplyCalls++;
            return Inner.Apply(request);
        }

        public StatModifierTransitionResult Tick(StatModifierTickRequest request) => Inner.Tick(request);
        public StatModifierTransitionResult Remove(StatModifierRemovalRequest request) => Inner.Remove(request);
        public StatModifierTransitionResult Cleanup(StatModifierCleanupRequest request) => Inner.Cleanup(request);
    }

    private sealed class RejectSecondApplyPolicyService : RecordingStatModifierPolicyService
    {
        private readonly ContentId _rejectedTrack;

        internal RejectSecondApplyPolicyService(
            IStatModifierPolicyService inner,
            ContentId rejectedTrack)
            : base(inner)
        {
            _rejectedTrack = rejectedTrack;
        }

        public override StatModifierTransitionResult Apply(StatModifierApplicationRequest request)
        {
            if (request.ModifierTrackId != _rejectedTrack)
            {
                return base.Apply(request);
            }

            _ = base.Apply(request);
            return Inner.Apply(new StatModifierApplicationRequest(
                request.State,
                request.ModifierTrackId,
                stageDelta: 0,
                request.Duration,
                request.IsActorDeployed,
                request.ActiveLifecycleBoundary));
        }
    }
}
