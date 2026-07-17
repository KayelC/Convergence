using System.Reflection;
using Convergence.Battle;
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Hosting;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class ProgressionPolicyTests
{
    private readonly IStatResolutionPolicy _stats = new StandardStatResolutionPolicy();
    private readonly IResourceGrowthPolicy _resources = new StandardResourceGrowthPolicy();
    private readonly IExperienceCurve _curve = new CubicExperienceCurve();

    [Theory]
    [InlineData(RuntimeStatSourceKind.Actor, 10)]
    [InlineData(RuntimeStatSourceKind.ActiveHostedEntity, 20)]
    public void StatPolicy_UsesOnlyTheExplicitStatSource(
        RuntimeStatSourceKind sourceKind,
        int expected)
    {
        StatResolutionResult result = _stats.Resolve(new StatResolutionRequest(
            sourceKind,
            StandardProgressionIds.Strength,
            BaseStats(10),
            ActiveHostedEntityStats(20)));

        Assert.Equal(expected, result.FinalValue);
    }

    [Fact]
    public void StatPolicy_UsesTheEntireHostedEntityStatBlockWithoutWeights()
    {
        foreach (ContentId statId in StandardProgressionIds.CoreStats)
        {
            StatResolutionResult result = _stats.Resolve(new StatResolutionRequest(
                RuntimeStatSourceKind.ActiveHostedEntity,
                statId,
                BaseStats(10),
                ActiveHostedEntityStats(20)));

            Assert.Equal(20, result.FinalValue);
        }
    }

    [Fact]
    public void StatPolicy_AppliesEquipmentModifiersBeforeCapWithoutApplyingBattleStages()
    {
        StatResolutionResult result = _stats.Resolve(new StatResolutionRequest(
            RuntimeStatSourceKind.Actor,
            StandardProgressionIds.Strength,
            BaseStats(38),
            equipmentStatModifiers: [new(StandardProgressionIds.Strength, 10)]));

        Assert.Equal(48, result.RawValue);
        Assert.Equal(40, result.CappedValue);
        Assert.Equal(40, result.FinalValue);
    }

    [Fact]
    public void StagePolicy_GenericAttackAffectsPhysicalAndMagicalDamageOnly()
    {
        RuntimeStatStageSnapshot attackUp = new(StandardProgressionIds.Attack, 1);
        var stages = new StandardStatStageScalingPolicy();

        Assert.Equal(1.25m, Resolve(StatStageScalingChannel.PhysicalDamageDealt));
        Assert.Equal(1.25m, Resolve(StatStageScalingChannel.MagicalDamageDealt));
        Assert.Equal(1m, Resolve(StatStageScalingChannel.DamageTaken));
        Assert.Equal(1m, Resolve(StatStageScalingChannel.HitChance));
        Assert.Equal(1m, Resolve(StatStageScalingChannel.Evasion));

        decimal Resolve(StatStageScalingChannel channel) =>
            stages.Resolve(new StatStageScalingRequest(channel, [attackUp])).Multiplier;
    }

    [Fact]
    public void ActorComposition_UsesHostedStatsEquipmentStagesAndPreservesCurrentResources()
    {
        RuntimeActorState hostedEntity = CreateActor("hosted", 20m);
        RuntimeActorState vessel = CreateActor("vessel", 5m, hpCurrent: 90m);
        RuntimePartyRosterSnapshot partyRoster = PartyRoster(vessel, hostedEntity);
        TestStatModifierPolicy.ApplyPersistent(vessel, StandardProgressionIds.Attack, 1);
        var service = new RuntimeActorCombatProfileCompositionService(
            _stats,
            _resources,
            new SkillRepository());

        RuntimeActorCombatProfileCompositionResult result = service.Compose(
            new RuntimeActorCombatProfileCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                partyRoster,
                [hostedEntity],
                equipmentStatModifiers:
                [
                    new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 2m),
                    new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 1m)
                ]));

        Assert.True(result.Applied);
        Assert.Equal(RuntimeStatSourceKind.ActiveHostedEntity, result.ResolvedSourceKind);
        Assert.Equal(22m, vessel.Stats[StandardProgressionIds.Strength]);
        Assert.Equal(21m, vessel.Stats[StandardProgressionIds.Vitality]);
        Assert.Equal(90m, vessel.Resources[StandardProgressionIds.Hp].Current);
        Assert.Equal(125m, vessel.Resources[StandardProgressionIds.Hp].Maximum);
        Assert.Equal(20m, vessel.Resources[StandardProgressionIds.Sp].Current);
        Assert.Equal(66m, vessel.Resources[StandardProgressionIds.Sp].Maximum);
        Assert.Equal(hostedEntity.InstanceId, partyRoster.ActiveHostedEntity!.InstanceId);
    }

    [Fact]
    public void ActorComposition_MissingHostedEntityPolicyEitherRejectsOrUsesActorStats()
    {
        RuntimeActorState rejectedActor = CreateActor("rejected_vessel", 7m);
        RuntimePartyRosterSnapshot rejectedRoster = PartyRoster(rejectedActor);
        RuntimeActorSnapshot rejectedBefore = rejectedActor.ToSnapshot();
        var service = new RuntimeActorCombatProfileCompositionService(
            _stats,
            _resources,
            new SkillRepository());

        RuntimeActorCombatProfileCompositionResult rejected = service.Compose(
            new RuntimeActorCombatProfileCompositionRequest(
                rejectedActor,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                partyRoster: rejectedRoster));

        Assert.False(rejected.Applied);
        Assert.Equal(
            RuntimeActorCombatProfileCompositionDiagnosticCode.MissingActiveHostedEntity,
            Assert.Single(rejected.Diagnostics).Code);
        AssertCompositionStateUnchanged(rejectedBefore, rejectedActor.ToSnapshot());

        RuntimeActorState fallbackActor = CreateActor("fallback_vessel", 7m, hpCurrent: 80m);
        RuntimePartyRosterSnapshot fallbackRoster = PartyRoster(fallbackActor);
        RuntimeActorCombatProfileCompositionResult fallback = service.Compose(
            new RuntimeActorCombatProfileCompositionRequest(
                fallbackActor,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.UseActorBaseStats,
                partyRoster: fallbackRoster));

        Assert.True(fallback.Applied);
        Assert.Equal(RuntimeStatSourceKind.Actor, fallback.ResolvedSourceKind);
        Assert.Equal(7m, fallbackActor.Stats[StandardProgressionIds.Strength]);
        Assert.Equal(55m, fallbackActor.Resources[StandardProgressionIds.Hp].Maximum);
        Assert.Equal(55m, fallbackActor.Resources[StandardProgressionIds.Hp].Current);
    }

    [Fact]
    public void ActorComposition_RejectsHostedEntityIdentityMismatchWithoutMutation()
    {
        RuntimeActorState expected = CreateActor("expected_hosted", 20m);
        RuntimeActorState supplied = CreateActor(
            "expected_hosted",
            30m,
            entityId: "supplied_hosted_entity");
        RuntimeActorState vessel = CreateActor("mismatched_vessel", 5m);
        RuntimePartyRosterSnapshot partyRoster = PartyRoster(vessel, expected);
        RuntimeActorSnapshot before = vessel.ToSnapshot();

        RuntimeActorCombatProfileCompositionResult result =
            new RuntimeActorCombatProfileCompositionService(new SkillRepository()).Compose(
            new RuntimeActorCombatProfileCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                partyRoster,
                [supplied]));

        Assert.False(result.Applied);
        Assert.Equal(
            RuntimeActorCombatProfileCompositionDiagnosticCode.ActiveHostedEntityIdentityMismatch,
            Assert.Single(result.Diagnostics).Code);
        AssertCompositionStateUnchanged(before, vessel.ToSnapshot());
    }

    [Fact]
    public void ActorComposition_RejectsEveryPartyRosterInvariantWithoutMutation()
    {
        RuntimeActorState actor = CreateActor("roster_owner", 5m);
        RuntimeActorReferenceSnapshot owner = Reference(actor);
        RuntimeActorReferenceSnapshot first = ActorReference("owned_first");
        RuntimeActorReferenceSnapshot second = ActorReference("owned_second");
        RuntimePartyRosterSnapshot[] invalidRosters =
        [
            new(owner, hostedEntityRoster: [first, first]),
            new(owner, companionRoster: [first, first]),
            new(owner, hostedEntityRoster: [first], companionRoster: [first]),
            new(owner, activeHostedEntity: second, hostedEntityRoster: [first])
        ];

        for (int index = 0; index < invalidRosters.Length; index++)
        {
            RuntimeActorSnapshot before = actor.ToSnapshot();

            RuntimeActorCombatProfileCompositionResult result =
                new RuntimeActorCombatProfileCompositionService(new SkillRepository()).Compose(
                new RuntimeActorCombatProfileCompositionRequest(
                    actor,
                    RuntimeStatSourceKind.Actor,
                    MissingHostedEntityBehavior.UseActorBaseStats,
                    partyRoster: invalidRosters[index]));

            Assert.False(result.Applied);
            Assert.Equal(
                RuntimeActorCombatProfileCompositionDiagnosticCode.RosterInvariantViolation,
                Assert.Single(result.Diagnostics).Code);
            AssertCompositionStateUnchanged(before, actor.ToSnapshot());
        }
    }

    [Fact]
    public void PartyRosterInvariantRules_ReturnOrderedImmutableDiagnostics()
    {
        RuntimeActorReferenceSnapshot owner = ActorReference("owner");
        RuntimeActorReferenceSnapshot reference = ActorReference("repeated_actor");
        var roster = new RuntimePartyRosterSnapshot(
            owner,
            activeHostedEntity: reference,
            hostedEntityRoster: [reference, reference],
            companionRoster: [reference, reference]);

        IReadOnlyList<RuntimePartyRosterInvariantDiagnostic> diagnostics =
            RuntimePartyRosterInvariantRules.Validate(
                roster,
                ownerActor: null,
                NoLimitRosterCapacityPolicy.Instance);

        Assert.Equal(
            [
                RuntimePartyRosterInvariantCode.DuplicateHostedEntityReference,
                RuntimePartyRosterInvariantCode.DuplicateCompanionReference,
                RuntimePartyRosterInvariantCode.HostedEntityCompanionRoleCollision,
                RuntimePartyRosterInvariantCode.HostedEntityCompanionRoleCollision
            ],
            diagnostics.Select(diagnostic => diagnostic.Code));
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<RuntimePartyRosterInvariantDiagnostic>>(
            diagnostics);
    }

    [Fact]
    public void ActorComposition_ResolutionFailureIsAtomic()
    {
        RuntimeActorState hostedEntity = CreateActor("atomic_hosted", 20m);
        RuntimeActorState vessel = CreateActor("atomic_vessel", 5m);
        RuntimePartyRosterSnapshot partyRoster = PartyRoster(vessel, hostedEntity);
        RuntimeActorSnapshot before = vessel.ToSnapshot();
        var service = new RuntimeActorCombatProfileCompositionService(
            new ThrowingStatResolutionPolicy(StandardProgressionIds.Magic),
            _resources,
            new SkillRepository());

        RuntimeActorCombatProfileCompositionResult result = service.Compose(
            new RuntimeActorCombatProfileCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                partyRoster,
                [hostedEntity]));

        Assert.False(result.Applied);
        Assert.Equal(
            RuntimeActorCombatProfileCompositionDiagnosticCode.StatResolutionFailed,
            Assert.Single(result.Diagnostics).Code);
        AssertCompositionStateUnchanged(before, vessel.ToSnapshot());
    }

    [Fact]
    public void ActorComposition_DrivesBattleDamageFromTheHostedEntityStats()
    {
        RuntimeActorState hostedEntity = CreateActor("damage_hosted", 20m);
        RuntimeActorState vessel = CreateActor("damage_vessel", 5m);
        RuntimePartyRosterSnapshot partyRoster = PartyRoster(vessel, hostedEntity);
        RuntimeActorState actorSourced = CreateActor("damage_actor_source", 5m);
        RuntimeActorState target = CreateActor("damage_target", 5m);
        RuntimeActorCombatProfileCompositionResult composition =
            new RuntimeActorCombatProfileCompositionService(new SkillRepository()).Compose(
            new RuntimeActorCombatProfileCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                partyRoster,
                [hostedEntity]));
        Assert.True(composition.Applied);

        var ruleset = new ProductionCombatRuleset(new MinimumRandomSource());
        ProductionDamageResolutionResult composedDamage = ruleset.ResolveDamage(
            DamageRequest(vessel, target));
        ProductionDamageResolutionResult actorDamage = ruleset.ResolveDamage(
            DamageRequest(actorSourced, target));

        Assert.True(composedDamage.TotalDamage > actorDamage.TotalDamage);

        ProductionDamageResolutionRequest DamageRequest(
            RuntimeActorState attacker,
            RuntimeActorState defender) =>
            new(
                ruleset.CreateCombatantProfile(attacker),
                ruleset.CreateCombatantProfile(defender),
                DamageElement.Physical,
                ElementalAffinity.Normal,
                Power: 20,
                Accuracy: 100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1));
    }

    [Fact]
    public void ActorCombatProfileComposition_SwitchesStatsDefensesSkillsAndPassivesTogether()
    {
        ContentId ownerTurnEnd = ContentId.Parse("owner_turn_end");
        SkillDefinition iceSkill = ActiveSkill("test.pack:ice_action", DamageElement.Ice);
        SkillDefinition fireSkill = ActiveSkill("test.pack:fire_action", DamageElement.Fire);
        SkillDefinition recoveryPassive = PassiveSkill(
            "test.pack:recovery_passive",
            ownerTurnEnd,
            restoreAmount: 5m);
        SkillDefinition vesselSkill = ActiveSkill("test.pack:vessel_action", DamageElement.Physical);
        var skills = new SkillRepository(iceSkill, fireSkill, recoveryPassive, vesselSkill);
        RuntimeActorState firstHostedEntity = CreateActor(
            "first_hosted",
            20m,
            defenseProfile: new CombatDefenseProfile(
                [new KeyValuePair<DamageElement, ElementalAffinity>(
                    DamageElement.Ice,
                    ElementalAffinity.Weak)]),
            skills: [iceSkill, recoveryPassive]);
        RuntimeActorState secondHostedEntity = CreateActor(
            "second_hosted",
            30m,
            defenseProfile: new CombatDefenseProfile(
                [new KeyValuePair<DamageElement, ElementalAffinity>(
                    DamageElement.Fire,
                    ElementalAffinity.Null)]),
            skills: [fireSkill]);
        RuntimeEquipmentSnapshot equipment = new(
        [
            new KeyValuePair<EquipmentSlot, ContentId>(
                EquipmentSlot.Weapon,
                ContentId.Parse("test.pack:practice_weapon"))
        ]);
        RuntimeActorState vessel = CreateActor(
            "profile_vessel",
            5m,
            hpCurrent: 90m,
            skills: [vesselSkill],
            equipment: equipment);
        vessel.SetGuarding(true);
        ContentId focusStatus = ContentId.Parse("focus_status");
        vessel.AddOtherStatus(focusStatus);
        RuntimeActorAffiliationSnapshot affiliation = vessel.Affiliation;
        RuntimeEncounterPresenceSnapshot encounterPresence = vessel.EncounterPresence;

        var service = new RuntimeActorCombatProfileCompositionService(_stats, _resources, skills);
        RuntimeActorCombatProfileCompositionResult first = service.Compose(
            new RuntimeActorCombatProfileCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                PartyRoster(vessel, firstHostedEntity),
                [firstHostedEntity, secondHostedEntity]));

        Assert.True(first.Applied);
        Assert.Equal(firstHostedEntity.InstanceId, first.SourceActorId);
        Assert.Equal(20m, vessel.Stats[StandardProgressionIds.Strength]);
        Assert.Equal(ElementalAffinity.Weak, vessel.GetElementalAffinity(DamageElement.Ice));
        Assert.Equal(
            [iceSkill.Id, recoveryPassive.Id],
            vessel.Skills.EquippedSkillIds);
        Assert.True(vessel.HasSkill(iceSkill.Id));
        Assert.False(vessel.HasSkill(vesselSkill.Id));
        Assert.Equal(recoveryPassive.Id, Assert.Single(vessel.Passives.Entries).Skill.Id);
        Assert.Equal(equipment, vessel.Equipment);
        Assert.True(vessel.IsGuarding);
        Assert.Contains(focusStatus, vessel.OtherStatuses);
        Assert.Equal(affiliation, vessel.Affiliation);
        Assert.Equal(encounterPresence, vessel.EncounterPresence);
        Assert.Equal(90m, vessel.GetRequiredResource(StandardProgressionIds.Hp).Current);

        vessel.SetResource(StandardProgressionIds.Hp, 50m);
        new BattleStatusLifecycleService(new MinimumRandomSource()).ProcessTurnEnd(
            new BattleTurnEndLifecycleRequest(
                vessel,
                [vessel],
                ContentId.Parse("battle"),
                ownerTurnEnd),
            ExecutionServices());
        Assert.Equal(55m, vessel.GetRequiredResource(StandardProgressionIds.Hp).Current);

        RuntimeActorCombatProfileCompositionResult second = service.Compose(
            new RuntimeActorCombatProfileCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                PartyRoster(vessel, secondHostedEntity),
                [firstHostedEntity, secondHostedEntity]));

        Assert.True(second.Applied);
        Assert.Equal(secondHostedEntity.InstanceId, second.SourceActorId);
        Assert.Equal(30m, vessel.Stats[StandardProgressionIds.Strength]);
        Assert.Equal(ElementalAffinity.Null, vessel.GetElementalAffinity(DamageElement.Fire));
        Assert.Equal([fireSkill.Id], vessel.Skills.EquippedSkillIds);
        Assert.True(vessel.HasSkill(fireSkill.Id));
        Assert.False(vessel.HasSkill(iceSkill.Id));
        Assert.Empty(vessel.Passives.Entries);
        Assert.Equal(equipment, vessel.Equipment);
        Assert.True(vessel.IsGuarding);
        Assert.Contains(focusStatus, vessel.OtherStatuses);
        Assert.Equal(affiliation, vessel.Affiliation);
        Assert.Equal(encounterPresence, vessel.EncounterPresence);
    }

    [Fact]
    public void ActorCombatProfileComposition_MissingSkillDefinitionRejectsWithoutMutation()
    {
        SkillDefinition missing = ActiveSkill("test.pack:missing_profile_skill", DamageElement.Ice);
        RuntimeActorState hostedEntity = CreateActor(
            "missing_skill_hosted",
            20m,
            defenseProfile: new CombatDefenseProfile(
                [new KeyValuePair<DamageElement, ElementalAffinity>(
                    DamageElement.Ice,
                    ElementalAffinity.Weak)]),
            skills: [missing]);
        RuntimeActorState vessel = CreateActor("missing_skill_vessel", 5m);
        RuntimeActorSnapshot before = vessel.ToSnapshot();
        CombatDefenseProfile defenseBefore = vessel.DefenseProfile;

        RuntimeActorCombatProfileCompositionResult result =
            new RuntimeActorCombatProfileCompositionService(_stats, _resources, new SkillRepository())
            .Compose(new RuntimeActorCombatProfileCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                PartyRoster(vessel, hostedEntity),
                [hostedEntity]));

        Assert.False(result.Applied);
        RuntimeActorCombatProfileCompositionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            RuntimeActorCombatProfileCompositionDiagnosticCode.SkillDefinitionMissing,
            diagnostic.Code);
        Assert.Equal(missing.Id, diagnostic.SkillId);
        Assert.Same(defenseBefore, vessel.DefenseProfile);
        AssertCompositionStateUnchanged(before, vessel.ToSnapshot());
    }

    [Fact]
    public void ResourcePolicy_PreservesCurrentValuesAndCapsToNewMaximums()
    {
        ResourceRecalculationResult result = _resources.Recalculate(new ResourceRecalculationRequest(
            [
                new RuntimeResourceSnapshot(StandardProgressionIds.Hp, 100, 100),
                new RuntimeResourceSnapshot(StandardProgressionIds.Sp, 20, 50)
            ],
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, 20),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, 6)
            ],
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Magic, 10)
            ]));

        RuntimeResourceSnapshot hp = result.GetRequired(StandardProgressionIds.Hp);
        RuntimeResourceSnapshot sp = result.GetRequired(StandardProgressionIds.Sp);
        Assert.Equal(70, hp.Maximum);
        Assert.Equal(70, hp.Current);
        Assert.Equal(36, sp.Maximum);
        Assert.Equal(20, sp.Current);
    }

    [Fact]
    public void ResourcePolicy_LevelUpDeltaHealsByMaximumIncrease()
    {
        ResourceRecalculationResult result = _resources.Recalculate(new ResourceRecalculationRequest(
            [new RuntimeResourceSnapshot(StandardProgressionIds.Hp, 50, 100)],
            [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, 120)],
            [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 0)],
            ResourceCurrentAdjustmentMode.LevelUpDelta));

        RuntimeResourceSnapshot hp = result.GetRequired(StandardProgressionIds.Hp);
        Assert.Equal(120, hp.Maximum);
        Assert.Equal(70, hp.Current);
    }

    [Fact]
    public void StandardPolicies_SaturateBoundaryArithmeticInsteadOfThrowing()
    {
        decimal maximumStat = RuntimeActorNumericDomain.MaximumStatValue;
        var extremeConfig = new StandardStatPolicyConfig(statCap: int.MaxValue);
        var stats = new StandardStatResolutionPolicy(extremeConfig);

        StatResolutionResult stat = stats.Resolve(new StatResolutionRequest(
            RuntimeStatSourceKind.ActiveHostedEntity,
            StandardProgressionIds.Strength,
            BaseStats(maximumStat),
            ActiveHostedEntityStats(decimal.MaxValue),
            equipmentStatModifiers: BaseStats(decimal.MaxValue)));
        ResourceRecalculationResult resources = _resources.Recalculate(new ResourceRecalculationRequest(
            [
                new RuntimeResourceSnapshot(StandardProgressionIds.Hp, decimal.MaxValue, decimal.MaxValue),
                new RuntimeResourceSnapshot(StandardProgressionIds.Sp, decimal.MaxValue, decimal.MaxValue)
            ],
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, decimal.MaxValue),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, decimal.MaxValue)
            ],
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, maximumStat),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Magic, maximumStat)
            ]));

        Assert.Equal(decimal.MaxValue, stat.RawValue);
        Assert.Equal(int.MaxValue, stat.CappedValue);
        Assert.Equal(int.MaxValue, stat.FinalValue);
        Assert.Equal(666m, resources.GetRequired(StandardProgressionIds.Hp).Maximum);
        Assert.Equal(333m, resources.GetRequired(StandardProgressionIds.Sp).Maximum);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2147483648d)]
    public void ResourcePolicy_RejectsStatsOutsideTheRuntimeNumericDomain(double value)
    {
        decimal stat = Convert.ToDecimal(value);

        Assert.Throws<ArgumentOutOfRangeException>(() => _resources.Recalculate(
            new ResourceRecalculationRequest(
                [],
                [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, 1m)],
                [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, stat)])));
    }

    [Fact]
    public void ResourcePolicy_RejectsNegativeBaseResourcesExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _resources.Recalculate(
            new ResourceRecalculationRequest(
                [],
                [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, -1m)],
                [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 1m)])));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 12)]
    [InlineData(10, 1500)]
    public void ExperienceCurve_PreservesConfiguredCubicRequirement(int level, int expected)
    {
        Assert.Equal(expected, _curve.GetRequiredExperience(level));
    }

    [Fact]
    public void ExperienceCurve_SaturatesAtTheSupportedLongRange()
    {
        Assert.Equal(long.MaxValue, _curve.GetRequiredExperience(int.MaxValue));
    }

    [Fact]
    public void LevelGrowth_IndependentActorAwardsPointsAndDeterministicBaseResourceGrowth()
    {
        var growth = new StandardLevelGrowthPolicy(_curve, _resources);

        LevelGrowthResult result = growth.ApplyExperience(new LevelGrowthRequest(
            new RuntimeProgressionSnapshot(1, 0, 0, 0),
            new RuntimeStatBlockSnapshot(BaseStats(2), BaseStats(2)),
            StandardLevelGrowthProfiles.IndependentActor,
            experienceAward: 13,
            new SequenceRandomSource(6, 3, 10, 7),
            [
                new RuntimeResourceSnapshot(StandardProgressionIds.Hp, 10, 30),
                new RuntimeResourceSnapshot(StandardProgressionIds.Sp, 5, 12)
            ],
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, 20),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, 6)
            ]));

        Assert.True(result.Applied);
        Assert.Equal(3, result.Progression.Level);
        Assert.Equal(0, result.Progression.Experience);
        Assert.Equal(13, result.Progression.LifetimeExperience);
        Assert.Equal(2, result.Progression.UnspentStatPoints);
        Assert.Equal(36, result.BaseResourceValues[StandardProgressionIds.Hp]);
        Assert.Equal(16, result.BaseResourceValues[StandardProgressionIds.Sp]);
        Assert.Equal(46, result.Resources.Single(resource => resource.ResourceId == StandardProgressionIds.Hp).Maximum);
        Assert.Equal(26, result.Resources.Single(resource => resource.ResourceId == StandardProgressionIds.Hp).Current);
        Assert.Equal(2, result.LevelUps.Count);
    }

    [Fact]
    public void LevelGrowth_OwnedEntityIncrementsRandomStatAndRespectsCap()
    {
        var growth = new StandardLevelGrowthPolicy(_curve, _resources);

        LevelGrowthResult result = growth.ApplyExperience(new LevelGrowthRequest(
            new RuntimeProgressionSnapshot(1, 0, 0, 0),
            new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 39)],
                [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 39)]),
            StandardLevelGrowthProfiles.OwnedEntity,
            experienceAward: 13,
            new SequenceRandomSource(0, 0)));

        Assert.True(result.Applied);
        Assert.Equal(3, result.Progression.Level);
        Assert.Equal(40, result.Stats.BaseStats[StandardProgressionIds.Strength]);
        Assert.Equal(1, result.LevelUps[0].StatIncreases[StandardProgressionIds.Strength]);
        Assert.Empty(result.LevelUps[1].StatIncreases);
    }

    [Fact]
    public void LevelGrowth_VesselGrowsBaseResourcesWithoutManualStatPoints()
    {
        var growth = new StandardLevelGrowthPolicy(_curve, _resources);

        LevelGrowthResult result = growth.ApplyExperience(new LevelGrowthRequest(
            new RuntimeProgressionSnapshot(1, 0, 0, 0),
            new RuntimeStatBlockSnapshot(BaseStats(20), BaseStats(20)),
            StandardLevelGrowthProfiles.Vessel,
            experienceAward: 1,
            new SequenceRandomSource(6, 3),
            [
                new RuntimeResourceSnapshot(StandardProgressionIds.Hp, 50, 120),
                new RuntimeResourceSnapshot(StandardProgressionIds.Sp, 20, 66)
            ],
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, 20),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, 6)
            ]));

        Assert.True(result.Applied);
        Assert.Equal(2, result.Progression.Level);
        Assert.Equal(0, result.Progression.UnspentStatPoints);
        Assert.Equal(26m, result.BaseResourceValues[StandardProgressionIds.Hp]);
        Assert.Equal(9m, result.BaseResourceValues[StandardProgressionIds.Sp]);
        Assert.Empty(Assert.Single(result.LevelUps).StatIncreases);
    }

    [Fact]
    public void LevelGrowth_RejectsNegativeExperienceWithoutMutation()
    {
        var progression = new RuntimeProgressionSnapshot(5, 10, 100, 2);
        var stats = new RuntimeStatBlockSnapshot(BaseStats(8), BaseStats(8));
        var growth = new StandardLevelGrowthPolicy(_curve, _resources);

        LevelGrowthResult result = growth.ApplyExperience(new LevelGrowthRequest(
            progression,
            stats,
            StandardLevelGrowthProfiles.IndependentActor,
            experienceAward: -1,
            new SequenceRandomSource()));

        Assert.False(result.Applied);
        Assert.Equal(progression, result.Progression);
        Assert.Equal(ProgressionMutationErrorCode.NegativeExperience, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void LevelGrowth_RejectsOverflowWithoutPublishingPartialProgression()
    {
        var progression = new RuntimeProgressionSnapshot(5, 10, long.MaxValue, 2);
        var stats = new RuntimeStatBlockSnapshot(BaseStats(8), BaseStats(8));
        var growth = new StandardLevelGrowthPolicy(_curve, _resources);

        LevelGrowthResult result = growth.ApplyExperience(new LevelGrowthRequest(
            progression,
            stats,
            StandardLevelGrowthProfiles.IndependentActor,
            experienceAward: 1,
            new SequenceRandomSource()));

        Assert.False(result.Applied);
        Assert.Same(progression, result.Progression);
        Assert.Same(stats, result.Stats);
        Assert.Equal(
            ProgressionMutationErrorCode.NumericOverflow,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void LevelGrowth_RejectsNonpositiveExperienceCurveResults()
    {
        var progression = new RuntimeProgressionSnapshot(1, 0, 0, 0);
        var stats = new RuntimeStatBlockSnapshot(BaseStats(1), BaseStats(1));
        var growth = new StandardLevelGrowthPolicy(new ZeroExperienceCurve(), _resources);

        LevelGrowthResult result = growth.ApplyExperience(new LevelGrowthRequest(
            progression,
            stats,
            StandardLevelGrowthProfiles.IndependentActor,
            experienceAward: 1,
            new SequenceRandomSource()));

        Assert.False(result.Applied);
        Assert.Equal(
            ProgressionMutationErrorCode.InvalidExperienceRequirement,
            Assert.Single(result.Diagnostics).Code);
        Assert.Same(progression, result.Progression);
    }

    [Fact]
    public void LevelGrowthPolicy_RejectsNonpositiveStatCapAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StandardLevelGrowthPolicy(_curve, _resources, statCap: 0));
    }

    [Fact]
    public void StatAllocation_AllocatesRecalculatesRejectsAndRollsBack()
    {
        var allocation = new StatAllocationService(_resources);
        var progression = new RuntimeProgressionSnapshot(1, 0, 0, 1);
        var stats = new RuntimeStatBlockSnapshot(
            [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 9)],
            [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 9)]);
        RuntimeResourceSnapshot[] resources = [new(StandardProgressionIds.Hp, 40, 65)];
        KeyValuePair<ContentId, decimal>[] baseResources = [new(StandardProgressionIds.Hp, 20)];

        StatAllocationResult applied = allocation.Allocate(new StatAllocationRequest(
            progression,
            stats,
            StandardProgressionIds.Vitality,
            resources,
            baseResources));

        Assert.True(applied.Applied);
        Assert.Equal(0, applied.Progression.UnspentStatPoints);
        Assert.Equal(10, applied.Stats.BaseStats[StandardProgressionIds.Vitality]);
        Assert.Equal(70, applied.Resources.Single(resource => resource.ResourceId == StandardProgressionIds.Hp).Maximum);

        StatAllocationResult noPoints = allocation.Allocate(new StatAllocationRequest(
            applied.Progression,
            applied.Stats,
            StandardProgressionIds.Vitality,
            applied.Resources,
            baseResources));
        Assert.False(noPoints.Applied);
        Assert.Equal(ProgressionMutationErrorCode.MissingStatPoints, Assert.Single(noPoints.Diagnostics).Code);

        StatAllocationResult rolledBack = allocation.Rollback(new StatRollbackRequest(
            applied.Progression,
            progression,
            stats,
            applied.Resources,
            baseResources));
        Assert.Equal(1, rolledBack.Progression.UnspentStatPoints);
        Assert.Equal(9, rolledBack.Stats.BaseStats[StandardProgressionIds.Vitality]);
        Assert.Equal(65, rolledBack.Resources.Single(resource => resource.ResourceId == StandardProgressionIds.Hp).Maximum);
    }

    [Fact]
    public void ProgressionPublicApi_ExposesNoHostSerializerOrLegacyRuntimeTypes()
    {
        Type[] runtimeTypes = typeof(StandardLevelGrowthPolicy).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == "Convergence.Runtime")
            .ToArray();
        string[] forbidden =
        [
            "System.Console",
            "System.IO",
            "System.Text.Json",
            "Newtonsoft",
            "Godot"
        ];

        foreach (Type type in runtimeTypes)
        {
            AssertAllowed(type, forbidden);
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                switch (member)
                {
                    case MethodInfo method:
                        AssertAllowed(method.ReturnType, forbidden);
                        foreach (ParameterInfo parameter in method.GetParameters())
                        {
                            AssertAllowed(parameter.ParameterType, forbidden);
                        }
                        break;
                    case PropertyInfo property:
                        AssertAllowed(property.PropertyType, forbidden);
                        break;
                    case FieldInfo field:
                        AssertAllowed(field.FieldType, forbidden);
                        break;
                }
            }
        }
    }

    private static KeyValuePair<ContentId, decimal>[] BaseStats(decimal value) =>
    [
        new(StandardProgressionIds.Strength, value),
        new(StandardProgressionIds.Magic, value),
        new(StandardProgressionIds.Vitality, value),
        new(StandardProgressionIds.Agility, value),
        new(StandardProgressionIds.Luck, value)
    ];

    private static KeyValuePair<ContentId, decimal>[] ActiveHostedEntityStats(decimal value) => BaseStats(value);

    private static RuntimeActorState CreateActor(
        string id,
        decimal statValue,
        decimal hpCurrent = 50m,
        string? entityId = null,
        CombatDefenseProfile? defenseProfile = null,
        IEnumerable<SkillDefinition>? skills = null,
        RuntimeEquipmentSnapshot? equipment = null)
    {
        ContentId resolvedEntityId = ContentId.Parse(entityId ?? $"{id}_entity");
        SkillDefinition[] skillDefinitions = (skills ?? []).ToArray();
        return
        new(
            RuntimeInstanceId.Parse(id),
            resolvedEntityId,
            ContentId.Parse("player_team"),
            StandardProgressionIds.Hp,
            defenseProfile ?? CombatDefenseProfile.Empty,
            [
                new BattleResourceState(StandardProgressionIds.Hp, hpCurrent, 100m),
                new BattleResourceState(StandardProgressionIds.Sp, 20m, 30m)
            ],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_host"), ContentId.Parse("player_team")),
            stats: BaseStats(statValue),
            skillIds: skillDefinitions.Select(skill => skill.Id),
            passiveSkills: skillDefinitions.Where(skill => skill.Activation == SkillActivation.Passive),
            identity: new RuntimeActorIdentitySnapshot(
                RuntimeInstanceId.Parse(id),
                resolvedEntityId,
                StandardProgressionIds.Vessel,
                id),
            baseResourceValues:
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, 20m),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, 6m)
            ],
            baseStats: BaseStats(statValue),
            skillState: new RuntimeSkillStateSnapshot(
                skillDefinitions.Select(skill => skill.Id),
                skillDefinitions.Select(skill => skill.Id)),
            equipment: equipment);
    }

    private static RuntimePartyRosterSnapshot PartyRoster(
        RuntimeActorState owner,
        RuntimeActorState? activeHostedEntity = null)
    {
        RuntimeActorReferenceSnapshot ownerReference = Reference(owner);
        RuntimeActorReferenceSnapshot? activeReference =
            activeHostedEntity is null ? null : Reference(activeHostedEntity);
        return new RuntimePartyRosterSnapshot(
            ownerReference,
            activeParty: [ownerReference],
            activeHostedEntity: activeReference,
            hostedEntityRoster: activeReference is null ? [] : [activeReference]);
    }

    private static RuntimeActorReferenceSnapshot Reference(RuntimeActorState actor) =>
        new(actor.InstanceId, actor.EntityId, actor.Identity.DisplayName);

    private static RuntimeActorReferenceSnapshot ActorReference(string id) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            id);

    private static void AssertCompositionStateUnchanged(
        RuntimeActorSnapshot expected,
        RuntimeActorSnapshot actual)
    {
        Assert.Equal(expected.Identity, actual.Identity);
        Assert.Equal(expected.Affiliation, actual.Affiliation);
        Assert.Equal(expected.EncounterPresence, actual.EncounterPresence);
        Assert.Equal(expected.Progression, actual.Progression);
        Assert.Equal(expected.VitalResourceId, actual.VitalResourceId);
        Assert.Equal(expected.Resources.ToArray(), actual.Resources.ToArray());
        Assert.Equal(
            expected.BaseResourceValues.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal).ToArray(),
            actual.BaseResourceValues.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal).ToArray());
        Assert.Equal(
            expected.Stats.BaseStats.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal).ToArray(),
            actual.Stats.BaseStats.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal).ToArray());
        Assert.Equal(
            expected.Stats.EffectiveStats.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal).ToArray(),
            actual.Stats.EffectiveStats.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal).ToArray());
        Assert.Equal(
            expected.Equipment.EquippedItemIds.OrderBy(pair => pair.Key).ToArray(),
            actual.Equipment.EquippedItemIds.OrderBy(pair => pair.Key).ToArray());
        Assert.Equal(expected.Skills.LearnedSkillIds, actual.Skills.LearnedSkillIds);
        Assert.Equal(expected.Skills.EquippedSkillIds, actual.Skills.EquippedSkillIds);
        Assert.Equal(expected.BattleStatus, actual.BattleStatus);
        Assert.Equal(expected.BattleActivations, actual.BattleActivations);
    }

    private static SkillDefinition ActiveSkill(string id, DamageElement element) =>
        new(
            ContentId.Parse(id),
            id,
            id,
            SkillActivation.Active,
            SkillMenuGroup.Offense,
            element == DamageElement.Ice
                ? InheritanceGroup.Ice
                : element == DamageElement.Fire
                    ? InheritanceGroup.Fire
                    : InheritanceGroup.Physical,
            new SkillInheritanceDefinition(true),
            targeting: new TargetingDefinition(
                TargetRelation.Enemy,
                TargetSelection.Single,
                TargetLifeState.Alive,
                false),
            effects:
            [
                new DamageEffectDefinition(
                    element,
                    10,
                    100,
                    new NeverCriticalDefinition(),
                    new HitCountDefinition(1, 1))
            ],
            availability: new SkillAvailabilityDefinition([ContentId.Parse("battle")]));

    private static SkillDefinition PassiveSkill(
        string id,
        ContentId eventId,
        decimal restoreAmount) =>
        new(
            ContentId.Parse(id),
            id,
            id,
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            triggers:
            [
                new PassiveTriggerDefinition(
                    eventId,
                    [
                        new RestoreResourceEffectDefinition(
                            StandardProgressionIds.Hp,
                            new FlatAmountDefinition(restoreAmount))
                    ])
            ]);

    private static BattleExecutionServices ExecutionServices()
    {
        var ruleset = new ProductionCombatRuleset(new MinimumRandomSource());
        return new BattleExecutionServices(
            new EmptyAilmentRepository(),
            ruleset,
            ruleset,
            ruleset,
            ruleset,
            ruleset,
            new FirstTargetPolicy(),
            new OrderedRuntimeTargetSelectionPolicy(),
            TestStatModifierPolicy.CreatePersistent());
    }

    private static void AssertAllowed(Type type, IReadOnlyList<string> forbidden)
    {
        foreach (Type candidate in Expand(type))
        {
            string identity = candidate.FullName ?? candidate.Name;
            Assert.DoesNotContain(forbidden, fragment => identity.Contains(fragment, StringComparison.Ordinal));
        }
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is Type element)
        {
            foreach (Type nested in Expand(element))
            {
                yield return nested;
            }
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Expand(argument))
            {
                yield return nested;
            }
        }
    }

    private sealed class SequenceRandomSource(params int[] values) : IRandomSource
    {
        private int _index;

        public int NextInt32(int minimumInclusive, int maximumExclusive)
        {
            int value = values[_index++];
            Assert.InRange(value, minimumInclusive, maximumExclusive - 1);
            return value;
        }

        public decimal NextUnitDecimal() => 0m;
    }

    private sealed class SkillRepository(params SkillDefinition[] skills) :
        ISkillDefinitionRepository
    {
        private readonly IReadOnlyDictionary<ContentId, SkillDefinition> _skills =
            skills.ToDictionary(skill => skill.Id);

        public bool TryGetSkill(ContentId id, out SkillDefinition? definition) =>
            _skills.TryGetValue(id, out definition);

        public SkillDefinition GetRequiredSkill(ContentId id) => _skills[id];
    }

    private sealed class EmptyAilmentRepository : IAilmentDefinitionRepository
    {
        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition)
        {
            definition = null;
            return false;
        }

        public AilmentDefinition GetRequiredAilment(ContentId id) =>
            throw new KeyNotFoundException(id.ToString());
    }

    private sealed class FirstTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) =>
            Array.AsReadOnly(candidates.Take(count.Maximum).ToArray());
    }

    private sealed class ZeroExperienceCurve : IExperienceCurve
    {
        public long GetRequiredExperience(int level) => 0;
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

        public decimal NextUnitDecimal() => 0m;
    }

    private sealed class ThrowingStatResolutionPolicy(ContentId rejectedStatId) : IStatResolutionPolicy
    {
        private readonly StandardStatResolutionPolicy _inner = new();

        public StatResolutionResult Resolve(StatResolutionRequest request)
        {
            if (request.StatId == rejectedStatId)
            {
                throw new InvalidOperationException("Test policy rejected the stat.");
            }

            return _inner.Resolve(request);
        }
    }
}
