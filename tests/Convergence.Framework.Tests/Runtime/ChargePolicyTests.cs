using Convergence.Battle;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Framework.Tests.TestSupport;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class ChargePolicyTests
{
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId BattleKind = ContentId.Parse("normal_battle");
    private static readonly ContentId Phase = ContentId.Parse("neutral_phase");

    [Fact]
    public void SplitPolicy_OwnsIndependentPhysicalAndMagicalStates()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var policy = new SplitChargePolicy();

        ChargeApplicationResult physical = policy.Apply(
            new ChargeApplicationRequest(actor, ChargeKind.Physical, 2.5m));
        ChargeApplicationResult magical = policy.Apply(
            new ChargeApplicationRequest(actor, ChargeKind.Magical, 1.75m));

        Assert.True(physical.Applied);
        Assert.True(magical.Applied);
        Assert.Equal(StandardChargePolicyIds.Split, actor.ChargePolicyId);
        Assert.Equal(2.5m, policy.ResolveDamageModifier(actor, DamageElement.Physical).Multiplier);
        Assert.Equal(1.75m, policy.ResolveDamageModifier(actor, DamageElement.Fire).Multiplier);
        Assert.Equal(ChargeKind.Physical, policy.ResolveDamageModifier(actor, DamageElement.Physical).ChargeKind);
        Assert.Equal(ChargeKind.Magical, policy.ResolveDamageModifier(actor, DamageElement.Fire).ChargeKind);
    }

    [Fact]
    public void SplitPolicy_RejectsDuplicateAndUnsupportedChargeWithoutMutation()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var policy = new SplitChargePolicy();
        policy.Apply(new ChargeApplicationRequest(actor, ChargeKind.Physical, 2m));
        RuntimeChargeStateSnapshot before = Assert.IsType<RuntimeChargeStateSnapshot>(
            actor.ToSnapshot().BattleStatus.ChargeState);

        ChargeApplicationResult duplicate = policy.Apply(
            new ChargeApplicationRequest(actor, ChargeKind.Physical, 3m));
        ChargeApplicationResult unsupported = policy.Apply(
            new ChargeApplicationRequest(actor, ChargeKind.General, 3m));

        Assert.False(duplicate.Applied);
        Assert.Same(duplicate.Before, duplicate.After);
        Assert.Equal(ChargePolicyDiagnosticCode.AlreadyInEffect, Assert.Single(duplicate.Diagnostics).Code);
        Assert.False(unsupported.Applied);
        Assert.Same(unsupported.Before, unsupported.After);
        Assert.Equal(ChargePolicyDiagnosticCode.UnsupportedChargeKind, Assert.Single(unsupported.Diagnostics).Code);
        RuntimeChargeStateSnapshot after = Assert.IsType<RuntimeChargeStateSnapshot>(
            actor.ToSnapshot().BattleStatus.ChargeState);
        Assert.Equal(before.PolicyId, after.PolicyId);
        Assert.Equal(before.Charges, after.Charges);
    }

    [Fact]
    public void Policy_RejectsInvalidRetainedDurationWithoutAdoptingPolicyState()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var policy = new SplitChargePolicy();

        ChargeApplicationResult result = policy.Apply(new ChargeApplicationRequest(
            actor,
            ChargeKind.Physical,
            2m,
            new TurnDurationDefinition(0, default, false)));

        Assert.False(result.Applied);
        Assert.Null(result.Before);
        Assert.Null(result.After);
        Assert.Equal(ChargePolicyDiagnosticCode.InvalidDuration, Assert.Single(result.Diagnostics).Code);
        Assert.Null(actor.ChargePolicyId);
        Assert.Empty(actor.Charges);
    }

    [Fact]
    public void Policy_AcceptsInstantDurationForActionScopedLifecycle()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var policy = new SplitChargePolicy();

        ChargeApplicationResult result = policy.Apply(new ChargeApplicationRequest(
            actor,
            ChargeKind.Physical,
            2m,
            new InstantDurationDefinition()));

        Assert.True(result.Applied);
        Assert.IsType<InstantDurationDefinition>(
            Assert.Single(actor.Charges).Value.Duration);

        actor.ExpireInstantDurations();

        Assert.Empty(actor.Charges);
        Assert.Equal(StandardChargePolicyIds.Split, actor.ChargePolicyId);
    }

    [Fact]
    public void UnifiedPolicy_UsesOneStateForEveryDamageElement()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var policy = new UnifiedChargePolicy();

        policy.Apply(new ChargeApplicationRequest(actor, ChargeKind.General, 2.25m));

        Assert.Equal(2.25m, policy.ResolveDamageModifier(actor, DamageElement.Physical).Multiplier);
        Assert.Equal(2.25m, policy.ResolveDamageModifier(actor, DamageElement.Ice).Multiplier);
        ChargeConsumptionResult consumed = policy.CompleteAction(
            actor,
            [DamageElement.Physical, DamageElement.Ice]);
        Assert.Equal([ChargeKind.General], consumed.ConsumedChargeKinds);
        Assert.Empty(actor.Charges);
    }

    [Fact]
    public void PolicyMismatch_IsRejectedAndRetainedPolicyOwnershipPersistsAfterConsumption()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var split = new SplitChargePolicy();
        var unified = new UnifiedChargePolicy();
        split.Apply(new ChargeApplicationRequest(actor, ChargeKind.Physical, 2m));
        split.CompleteAction(actor, [DamageElement.Physical]);

        ChargeApplicationAssessment assessment = unified.Assess(
            new ChargeApplicationRequest(actor, ChargeKind.General, 2m));

        Assert.False(assessment.CanApply);
        Assert.Equal(ChargePolicyDiagnosticCode.PolicyMismatch, Assert.Single(assessment.Diagnostics).Code);
        Assert.Equal(StandardChargePolicyIds.Split, actor.ChargePolicyId);
        Assert.Throws<InvalidOperationException>(() =>
            unified.ResolveDamageModifier(actor, DamageElement.Physical));
    }

    [Fact]
    public void SnapshotAndRegistry_PreservePolicyIdentityAndImmutableOrderedCharges()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var policy = new SplitChargePolicy();
        policy.Apply(new ChargeApplicationRequest(actor, ChargeKind.Magical, 1.5m));
        policy.Apply(new ChargeApplicationRequest(actor, ChargeKind.Physical, 2m));

        RuntimeChargeStateSnapshot state = Assert.IsType<RuntimeChargeStateSnapshot>(
            actor.ToSnapshot().BattleStatus.ChargeState);
        var registry = ChargePolicyRegistry.CreateStandard();

        Assert.Equal(StandardChargePolicyIds.Split, state.PolicyId);
        Assert.Equal([ChargeKind.Physical, ChargeKind.Magical], state.Charges.Select(charge => charge.Kind));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeChargeSnapshot>)state.Charges).Clear());
        Assert.True(registry.TryResolve(state.PolicyId, out IChargePolicyService? resolved));
        Assert.IsType<SplitChargePolicy>(resolved);
        Assert.True(resolved!.ValidateState(state).IsValid);

        ChargePolicyValidationResult malformed = resolved.ValidateState(
            new RuntimeChargeStateSnapshot(
                StandardChargePolicyIds.Split,
                [new RuntimeChargeSnapshot(
                    ChargeKind.Physical,
                    2m,
                    new PhaseDurationDefinition(default))]));
        Assert.Contains(malformed.Diagnostics, diagnostic =>
            diagnostic.Code == ChargePolicyDiagnosticCode.InvalidDuration);
    }

    [Fact]
    public void DamageAction_UsesAuthoredMultiplierAndConsumesAfterEveryTarget()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState targetOne = Actor("target_one", EnemyTeam);
        RuntimeActorState targetTwo = Actor("target_two", EnemyTeam);
        var charges = new SplitChargePolicy();
        charges.Apply(new ChargeApplicationRequest(actor, ChargeKind.Magical, 2.5m));
        var damage = new RecordingDamagePolicy(request =>
            new DamagePolicyResolution(
                [new DamageHitResolution(true, 10m * request.ChargeMultiplier)],
                request.Affinity));
        SkillDefinition skill = DamageSkill(
            new DamageEffectDefinition(
                DamageElement.Ice,
                10,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1)),
            TargetSelection.All);

        SkillExecutionResult result = new SkillExecutor(Services(damage, charges)).Execute(
            Request(skill, actor, [actor, targetOne, targetTwo]));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        Assert.Equal(75m, targetOne.GetRequiredResource(Hp).Current);
        Assert.Equal(75m, targetTwo.GetRequiredResource(Hp).Current);
        Assert.Equal(2, damage.Requests.Count);
        Assert.All(damage.Requests, request =>
        {
            Assert.Equal(2.5m, request.ChargeMultiplier);
            Assert.Equal(ChargeKind.Magical, request.ChargeKind);
        });
        Assert.Empty(actor.Charges);
    }

    [Theory]
    [InlineData(ElementalAffinity.Normal, false)]
    [InlineData(ElementalAffinity.Null, true)]
    [InlineData(ElementalAffinity.Repel, true)]
    [InlineData(ElementalAffinity.Absorb, true)]
    public void ResolvedDamageAttempt_ConsumesChargeForMissAndDefensiveAffinity(
        ElementalAffinity affinity,
        bool hit)
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor(
            "target",
            EnemyTeam,
            new CombatDefenseProfile([new(DamageElement.Physical, affinity)]));
        var charges = new SplitChargePolicy();
        charges.Apply(new ChargeApplicationRequest(actor, ChargeKind.Physical, 2m));
        var damage = new RecordingDamagePolicy(request =>
            new DamagePolicyResolution([new DamageHitResolution(hit, 10)], request.Affinity));

        SkillExecutionResult result = new SkillExecutor(Services(damage, charges)).Execute(
            Request(DamageSkill(PhysicalDamage()), actor, [actor, target], [target.InstanceId]));

        Assert.NotEqual(SkillExecutionStatus.Rejected, result.Status);
        Assert.Empty(actor.Charges);
    }

    [Fact]
    public void RejectedAction_DoesNotConsumeChargeOrCommitCost()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam);
        var charges = new SplitChargePolicy();
        charges.Apply(new ChargeApplicationRequest(actor, ChargeKind.Physical, 2m));
        SkillDefinition skill = DamageSkill(PhysicalDamage());

        SkillExecutionResult result = new SkillExecutor(Services(new RecordingDamagePolicy(), charges)).Execute(
            Request(skill, actor, [actor, target]));

        Assert.Equal(SkillExecutionStatus.Rejected, result.Status);
        Assert.True(actor.Charges.ContainsKey(ChargeKind.Physical));
        Assert.Equal(100m, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void MixedDamageAction_ConsumesBothSplitChargesOnlyAfterBothEffectsResolve()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam);
        var charges = new SplitChargePolicy();
        charges.Apply(new ChargeApplicationRequest(actor, ChargeKind.Physical, 2m));
        charges.Apply(new ChargeApplicationRequest(actor, ChargeKind.Magical, 3m));
        var damage = new RecordingDamagePolicy(request =>
            new DamagePolicyResolution(
                [new DamageHitResolution(true, request.ChargeMultiplier)],
                request.Affinity));
        SkillDefinition skill = new(
            ContentId.Parse("mixed_attack"),
            "Mixed Attack",
            "Uses two typed damage effects.",
            SkillActivation.Active,
            SkillMenuGroup.Offense,
            InheritanceGroup.Physical,
            new SkillInheritanceDefinition(true),
            targeting: new TargetingDefinition(
                TargetRelation.Enemy,
                TargetSelection.Single,
                TargetLifeState.Alive,
                false),
            effects: [PhysicalDamage(), MagicalDamage()],
            availability: new SkillAvailabilityDefinition([Battle]));

        SkillExecutionResult result = new SkillExecutor(Services(damage, charges)).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        Assert.Equal([2m, 3m], damage.Requests.Select(request => request.ChargeMultiplier));
        Assert.Empty(actor.Charges);
    }

    private static RuntimeActorState Actor(
        string id,
        ContentId team,
        CombatDefenseProfile? defense = null) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            team,
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, 100, 100)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_controller"), team));

    private static BattleExecutionServices Services(
        IDamageExecutionPolicy damage,
        IChargePolicyService charges) =>
        new(
            EmptyAilments.Instance,
            damage,
            new NeverInstantDeathPolicy(),
            new NeverAilmentPolicy(),
            new AlwaysChancePolicy(),
            new PowerAmountPolicy(),
            new FirstRandomTargetPolicy(),
            new OrderedRuntimeTargetSelectionPolicy(),
            TestStatModifierPolicy.CreatePersistent(),
            charges);

    private static SkillExecutionRequest Request(
        SkillDefinition skill,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        IEnumerable<RuntimeInstanceId>? targets = null) =>
        new(skill, actor, participants, Battle, BattleKind, Phase, targets);

    private static SkillDefinition DamageSkill(
        DamageEffectDefinition effect,
        TargetSelection selection = TargetSelection.Single) =>
        new(
            ContentId.Parse("charge_test_attack"),
            "Charge Test Attack",
            "Executes typed damage.",
            SkillActivation.Active,
            SkillMenuGroup.Offense,
            InheritanceGroup.Physical,
            new SkillInheritanceDefinition(true),
            targeting: new TargetingDefinition(
                TargetRelation.Enemy,
                selection,
                TargetLifeState.Alive,
                false),
            effects: [effect],
            availability: new SkillAvailabilityDefinition([Battle]));

    private static DamageEffectDefinition PhysicalDamage() =>
        new(
            DamageElement.Physical,
            10,
            100,
            new NeverCriticalDefinition(),
            new HitCountDefinition(1, 1));

    private static DamageEffectDefinition MagicalDamage() =>
        new(
            DamageElement.Fire,
            10,
            100,
            new NeverCriticalDefinition(),
            new HitCountDefinition(1, 1));

    private sealed class RecordingDamagePolicy(
        Func<DamagePolicyRequest, DamagePolicyResolution>? resolve = null) : IDamageExecutionPolicy
    {
        private readonly Func<DamagePolicyRequest, DamagePolicyResolution> _resolve = resolve ??
            (request => new DamagePolicyResolution(
                [new DamageHitResolution(true, 10)],
                request.Affinity));

        public List<DamagePolicyRequest> Requests { get; } = [];

        public DamagePolicyResolution Resolve(DamagePolicyRequest request)
        {
            Requests.Add(request);
            return _resolve(request);
        }
    }

    private sealed class EmptyAilments : Convergence.Catalog.IAilmentDefinitionRepository
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

    private sealed class FirstRandomTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) =>
            candidates.Take(count.Minimum).ToArray();
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
}
