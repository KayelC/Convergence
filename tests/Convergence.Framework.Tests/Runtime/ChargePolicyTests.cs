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
        ChargeDamageModifier physical = policy.ResolveDamageModifier(actor, DamageElement.Physical);
        ChargeDamageModifier magical = policy.ResolveDamageModifier(actor, DamageElement.Ice);
        ChargeConsumptionResult consumed = policy.CompleteAction(
            actor,
            [physical, magical]);
        Assert.Equal([ChargeKind.General], consumed.ConsumedChargeKinds);
        Assert.Empty(actor.Charges);
    }

    [Fact]
    public void CompleteAction_RejectsSourceLessChargedModifierWithoutMutation()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var policy = new SplitChargePolicy();
        policy.Apply(new ChargeApplicationRequest(actor, ChargeKind.Physical, 2m));

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            policy.CompleteAction(
                actor,
                [new ChargeDamageModifier(2m, ChargeKind.Physical)]));

        Assert.Contains("ResolveDamageModifier", exception.Message, StringComparison.Ordinal);
        Assert.Equal(StandardChargePolicyIds.Split, actor.ChargePolicyId);
        Assert.Equal(2m, Assert.Single(actor.Charges).Value.Multiplier);
    }

    [Fact]
    public void DisabledPolicy_RejectsApplicationAndKeepsDamageNeutral()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var policy = new DisabledChargePolicy();

        ChargeApplicationResult application = policy.Apply(
            new ChargeApplicationRequest(actor, ChargeKind.Physical, 2m));
        ChargeDamageModifier modifier = policy.ResolveDamageModifier(
            actor,
            DamageElement.Physical);
        ChargeConsumptionResult completion = policy.CompleteAction(actor, []);

        Assert.False(application.Applied);
        Assert.Same(application.Before, application.After);
        Assert.Null(application.Before);
        Assert.Equal(
            ChargePolicyDiagnosticCode.UnsupportedChargeKind,
            Assert.Single(application.Diagnostics).Code);
        Assert.Equal(1m, modifier.Multiplier);
        Assert.Null(modifier.ChargeKind);
        Assert.False(completion.StateChanged);
        Assert.Empty(completion.ConsumedChargeKinds);
        Assert.Null(actor.ChargePolicyId);
        Assert.Empty(actor.Charges);
    }

    [Fact]
    public void DisabledPolicy_ValidatesOnlyEmptyMatchingStateAndIsRegistered()
    {
        var policy = new DisabledChargePolicy();
        var empty = new RuntimeChargeStateSnapshot(StandardChargePolicyIds.Disabled);
        var retained = new RuntimeChargeStateSnapshot(
            StandardChargePolicyIds.Disabled,
            [new RuntimeChargeSnapshot(ChargeKind.Physical, 2m)]);
        var mismatched = new RuntimeChargeStateSnapshot(StandardChargePolicyIds.Split);

        Assert.True(policy.ValidateState(empty).IsValid);
        Assert.Contains(policy.ValidateState(retained).Diagnostics, diagnostic =>
            diagnostic.Code == ChargePolicyDiagnosticCode.UnsupportedChargeKind);
        Assert.Contains(policy.ValidateState(mismatched).Diagnostics, diagnostic =>
            diagnostic.Code == ChargePolicyDiagnosticCode.PolicyMismatch);

        ChargePolicyRegistry registry = ChargePolicyRegistry.CreateStandard();
        Assert.True(registry.TryResolve(StandardChargePolicyIds.Disabled, out IChargePolicyService? resolved));
        Assert.IsType<DisabledChargePolicy>(resolved);
    }

    [Fact]
    public void AuthoredGeneralCharge_DeserializesAndExecutesThroughUnifiedPolicy()
    {
        const string json =
            """
            {
              "schemaVersion": 6,
              "skills": [{
                "id": "unified_focus",
                "displayName": "Unified Focus",
                "description": "Grants one general charge.",
                "activation": "active",
                "menuGroup": "utility",
                "inheritanceGroupId": "utility",
                "inheritance": { "isInheritable": true },
                "targeting": {
                  "relation": "self",
                  "selection": "single",
                  "lifeState": "alive",
                  "allowSelf": true
                },
                "availability": { "contexts": ["battle"] },
                "effects": [{
                  "type": "grant_charge",
                  "charge": "general",
                  "multiplier": 2.25,
                  "duration": { "type": "battle" }
                }]
              }]
            }
            """;
        SkillDefinition skill = Assert.Single(
            new SkillSystemJsonDeserializer().DeserializeSkills(json, "unified-charge.skills.json").Records);
        GrantChargeEffectDefinition effect = Assert.IsType<GrantChargeEffectDefinition>(
            Assert.Single(skill.Effects));
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var policy = new UnifiedChargePolicy();

        ChargeApplicationResult application = policy.Apply(new ChargeApplicationRequest(
            actor,
            effect.Charge,
            effect.Multiplier,
            effect.Duration));

        Assert.True(application.Applied);
        Assert.Equal(ChargeKind.General, Assert.Single(actor.Charges).Key);
        Assert.Equal(2.25m, policy.ResolveDamageModifier(actor, DamageElement.Physical).Multiplier);
        Assert.Equal(2.25m, policy.ResolveDamageModifier(actor, DamageElement.Ice).Multiplier);
        ChargeDamageModifier physical = policy.ResolveDamageModifier(actor, DamageElement.Physical);
        ChargeDamageModifier magical = policy.ResolveDamageModifier(actor, DamageElement.Ice);
        Assert.Equal(
            [ChargeKind.General],
            policy.CompleteAction(actor, [physical, magical]).ConsumedChargeKinds);
        Assert.Empty(actor.Charges);
    }

    [Fact]
    public void PolicyMismatch_IsRejectedAndRetainedPolicyOwnershipPersistsAfterConsumption()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var split = new SplitChargePolicy();
        var unified = new UnifiedChargePolicy();
        split.Apply(new ChargeApplicationRequest(actor, ChargeKind.Physical, 2m));
        split.CompleteAction(actor, [split.ResolveDamageModifier(actor, DamageElement.Physical)]);

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
            new DamagePolicyResolution(
                [new DamageHitResolution(hit, hit ? 10 : 0)],
                request.Affinity));

        SkillExecutionResult result = new SkillExecutor(Services(damage, charges)).Execute(
            Request(DamageSkill(PhysicalDamage()), actor, [actor, target], [target.InstanceId]));

        Assert.NotEqual(SkillExecutionStatus.Rejected, result.Status);
        ChargeDamageModifier participation = Assert.IsType<ChargeDamageModifier>(
            Assert.Single(result.Effects).ParticipatingCharge);
        Assert.Equal(ChargeKind.Physical, participation.ChargeKind);
        Assert.Equal(2m, participation.Multiplier);
        Assert.Empty(actor.Charges);
    }

    [Fact]
    public void DamageThenChargeGrant_RetainsChargeThatDidNotParticipate()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var charges = new SplitChargePolicy();
        var damage = new RecordingDamagePolicy();
        SkillDefinition skill = SelfSkill(
            "damage_then_charge",
            PhysicalDamage(),
            new GrantChargeEffectDefinition(ChargeKind.Physical, 2m));

        SkillExecutionResult result = new SkillExecutor(Services(damage, charges)).Execute(
            Request(skill, actor, [actor], [actor.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        Assert.Null(Assert.Single(damage.Requests).ChargeKind);
        Assert.Equal(ChargeKind.Physical, Assert.Single(actor.Charges).Key);
        Assert.Equal(2m, Assert.Single(actor.Charges).Value.Multiplier);
    }

    [Fact]
    public void ChargeGrantThenDamage_ConsumesChargeThatParticipated()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var charges = new SplitChargePolicy();
        var damage = new RecordingDamagePolicy();
        SkillDefinition skill = SelfSkill(
            "charge_then_damage",
            new GrantChargeEffectDefinition(ChargeKind.Physical, 2m),
            PhysicalDamage());

        SkillExecutionResult result = new SkillExecutor(Services(damage, charges)).Execute(
            Request(skill, actor, [actor], [actor.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        DamagePolicyRequest request = Assert.Single(damage.Requests);
        Assert.Equal(ChargeKind.Physical, request.ChargeKind);
        Assert.Equal(2m, request.ChargeMultiplier);
        Assert.Empty(actor.Charges);
    }

    [Fact]
    public void ParticipatingChargeClearedAndReplacedLater_IsNotMistakenForReplacement()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var charges = new SplitChargePolicy();
        charges.Apply(new ChargeApplicationRequest(actor, ChargeKind.Physical, 2m));
        var damage = new RecordingDamagePolicy();
        SkillDefinition skill = SelfSkill(
            "replace_participating_charge",
            PhysicalDamage(),
            new RemoveStatusEffectDefinition([StatusEffectKind.Charge]),
            new GrantChargeEffectDefinition(ChargeKind.Physical, 3m));

        SkillExecutionResult result = new SkillExecutor(Services(damage, charges)).Execute(
            Request(skill, actor, [actor], [actor.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        Assert.Equal(2m, Assert.Single(damage.Requests).ChargeMultiplier);
        Assert.Equal(ChargeKind.Physical, Assert.Single(actor.Charges).Key);
        Assert.Equal(3m, Assert.Single(actor.Charges).Value.Multiplier);
    }

    [Fact]
    public void NestedDefeatPreventionGrantAfterUnchargedDamage_RemainsAvailable()
    {
        SkillDefinition response = new(
            ContentId.Parse("emergency_focus"),
            "Emergency Focus",
            "Grants charge when its owner would be defeated.",
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            triggers:
            [
                new PassiveTriggerDefinition(
                    ContentId.Parse("owner_would_be_defeated"),
                    [new GrantChargeEffectDefinition(ChargeKind.Physical, 2m)])
            ]);
        RuntimeActorState actor = Actor(
            "actor",
            PlayerTeam,
            hp: 10m,
            passiveSkills: [response]);
        var charges = new SplitChargePolicy();
        var damage = new RecordingDamagePolicy(request =>
            new DamagePolicyResolution(
                [new DamageHitResolution(true, 20m)],
                request.Affinity));

        SkillExecutionResult result = new SkillExecutor(Services(damage, charges)).Execute(
            Request(
                SelfSkill("self_strike", PhysicalDamage()),
                actor,
                [actor],
                [actor.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        Assert.True(actor.IsDefeated);
        Assert.Equal(PassiveTriggerOutcome.Executed, Assert.Single(result.PassiveActivations).Outcome);
        Assert.Equal(ChargeKind.Physical, Assert.Single(actor.Charges).Key);
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
    public void CustomDamageExecutor_FabricatedChargeReceiptRejectsActionWithoutLiveMutation()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam);
        var charges = new SplitChargePolicy();
        charges.Apply(new ChargeApplicationRequest(actor, ChargeKind.Physical, 2m));
        EffectExecutorRegistry executors = new EffectExecutorRegistry()
            .Register(new FabricatedChargeDamageExecutor());

        SkillExecutionResult result = new SkillExecutor(
            Services(new RecordingDamagePolicy(), charges, executors)).Execute(
                Request(
                    DamageSkill(PhysicalDamage()),
                    actor,
                    [actor, target],
                    [target.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Rejected, result.Status);
        SkillExecutionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(SkillExecutionDiagnosticCode.ExecutionFailed, diagnostic.Code);
        Assert.Contains("ResolveDamageModifier", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(2m, Assert.Single(actor.Charges).Value.Multiplier);
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

    [Fact]
    public void SharedContactComponent_StillResolvesAndConsumesItsOwnSplitCharge()
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
        EffectLocalId sourceId = EffectLocalId.Parse("physical_contact");
        DamageEffectDefinition primary = PhysicalDamage() with { EffectId = sourceId };
        DamageEffectDefinition secondary = MagicalDamage() with
        {
            ContactMode = DamageContactMode.SharedContact,
            Dependency = new EffectDependencyDefinition(
                sourceId,
                EffectDependencyRequirement.PositiveDamage,
                EffectDependencyScope.SameTarget)
        };
        SkillDefinition skill = new(
            ContentId.Parse("charged_shared_contact"),
            "Charged Shared Contact",
            "Exercises split charges across linked damage components.",
            SkillActivation.Active,
            SkillMenuGroup.Offense,
            InheritanceGroup.Physical,
            new SkillInheritanceDefinition(true),
            targeting: new TargetingDefinition(
                TargetRelation.Enemy,
                TargetSelection.Single,
                TargetLifeState.Alive,
                false),
            effects: [primary, secondary],
            availability: new SkillAvailabilityDefinition([Battle]));

        SkillExecutionResult result = new SkillExecutor(Services(damage, charges)).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        Assert.Equal([ChargeKind.Physical, ChargeKind.Magical], damage.Requests.Select(request => request.ChargeKind));
        Assert.Equal([2m, 3m], damage.Requests.Select(request => request.ChargeMultiplier));
        Assert.Equal(
            DamageContactMode.SharedContact,
            Assert.Single(result.Effects[1].DamageHits).ContactMode);
        Assert.Empty(actor.Charges);
    }

    private static RuntimeActorState Actor(
        string id,
        ContentId team,
        CombatDefenseProfile? defense = null,
        decimal hp = 100m,
        IEnumerable<SkillDefinition>? passiveSkills = null) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            team,
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, hp, 100)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_controller"), team),
            passiveSkills: passiveSkills);

    private static BattleExecutionServices Services(
        IDamageExecutionPolicy damage,
        IChargePolicyService charges,
        EffectExecutorRegistry? effectExecutors = null) =>
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
            charges,
            effectExecutors: effectExecutors);

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

    private static SkillDefinition SelfSkill(
        string id,
        params EffectDefinition[] effects) =>
        new(
            ContentId.Parse(id),
            id,
            "Executes an ordered self-targeted effect sequence.",
            SkillActivation.Active,
            SkillMenuGroup.Utility,
            InheritanceGroup.Physical,
            new SkillInheritanceDefinition(true),
            targeting: new TargetingDefinition(
                TargetRelation.Self,
                TargetSelection.Single,
                TargetLifeState.Alive,
                true),
            effects: effects,
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

    private sealed class FabricatedChargeDamageExecutor : IEffectExecutor<DamageEffectDefinition>
    {
        public EffectExecutionResult Execute(
            DamageEffectDefinition definition,
            EffectExecutionContext context) =>
            new(
                context.EffectIndex,
                context.Target?.InstanceId,
                EffectExecutionOutcome.Success,
                Value: 10m)
            {
                ParticipatingCharge = new ChargeDamageModifier(2m, ChargeKind.Physical)
            };
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
