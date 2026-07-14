using System.Reflection;
using Convergence.Battle;
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
    public void StatPolicy_AppliesAccessoryModifiersBeforeCapAndStagesAfterCap()
    {
        StatResolutionResult result = _stats.Resolve(new StatResolutionRequest(
            RuntimeStatSourceKind.Actor,
            StandardProgressionIds.Strength,
            BaseStats(38),
            equipmentStatModifiers: [new(StandardProgressionIds.Strength, 10)],
            statStages:
            [
                new RuntimeStatStageSnapshot(StandardProgressionIds.PhysicalAttack, 1),
                new RuntimeStatStageSnapshot(StandardProgressionIds.PhysicalAttack, -1)
            ]));

        Assert.Equal(48, result.RawValue);
        Assert.Equal(40, result.CappedValue);
        Assert.Equal(33, result.FinalValue);
    }

    [Fact]
    public void StatPolicy_GenericAttackAffectsStrengthAndMagicButNotLuck()
    {
        RuntimeStatStageSnapshot attackUp = new(StandardProgressionIds.Attack, 1);

        Assert.Equal(14, Resolve(StandardProgressionIds.Strength, attackUp));
        Assert.Equal(14, Resolve(StandardProgressionIds.Magic, attackUp));
        Assert.Equal(10, Resolve(StandardProgressionIds.Luck, attackUp));

        int Resolve(ContentId stat, RuntimeStatStageSnapshot stage) =>
            _stats.Resolve(new StatResolutionRequest(
                RuntimeStatSourceKind.Actor,
                stat,
                BaseStats(10),
                statStages: [stage])).FinalValue;
    }

    [Fact]
    public void ActorComposition_UsesHostedStatsEquipmentStagesAndPreservesCurrentResources()
    {
        RuntimeActorState hostedEntity = CreateActor("hosted", 20m);
        RuntimeActorReferenceSnapshot hostedReference = Reference(hostedEntity);
        RuntimeActorRosterSnapshot rosters = new(activeHostedEntity: hostedReference);
        RuntimeActorState vessel = CreateActor("vessel", 5m, rosters, hpCurrent: 90m);
        vessel.ChangeStatStage(StandardProgressionIds.Attack, 1, duration: null);
        var service = new RuntimeActorStatCompositionService(_stats, _resources);

        RuntimeActorStatCompositionResult result = service.Compose(
            new RuntimeActorStatCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                hostedEntity,
                equipmentStatModifiers:
                [
                    new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 2m),
                    new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 1m)
                ]));

        Assert.True(result.Applied);
        Assert.Equal(RuntimeStatSourceKind.ActiveHostedEntity, result.ResolvedSourceKind);
        Assert.Equal(30m, vessel.Stats[StandardProgressionIds.Strength]);
        Assert.Equal(21m, vessel.Stats[StandardProgressionIds.Vitality]);
        Assert.Equal(90m, vessel.Resources[StandardProgressionIds.Hp].Current);
        Assert.Equal(125m, vessel.Resources[StandardProgressionIds.Hp].Maximum);
        Assert.Equal(20m, vessel.Resources[StandardProgressionIds.Sp].Current);
        Assert.Equal(90m, vessel.Resources[StandardProgressionIds.Sp].Maximum);
        Assert.Equal(rosters.ActiveHostedEntity, vessel.Rosters.ActiveHostedEntity);
    }

    [Fact]
    public void ActorComposition_MissingHostedEntityPolicyEitherRejectsOrUsesActorStats()
    {
        RuntimeActorState rejectedActor = CreateActor("rejected_vessel", 7m);
        RuntimeActorSnapshot rejectedBefore = rejectedActor.ToSnapshot();
        var service = new RuntimeActorStatCompositionService(_stats, _resources);

        RuntimeActorStatCompositionResult rejected = service.Compose(
            new RuntimeActorStatCompositionRequest(
                rejectedActor,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution));

        Assert.False(rejected.Applied);
        Assert.Equal(
            RuntimeActorStatCompositionDiagnosticCode.MissingActiveHostedEntity,
            Assert.Single(rejected.Diagnostics).Code);
        AssertCompositionStateUnchanged(rejectedBefore, rejectedActor.ToSnapshot());

        RuntimeActorState fallbackActor = CreateActor("fallback_vessel", 7m, hpCurrent: 80m);
        RuntimeActorStatCompositionResult fallback = service.Compose(
            new RuntimeActorStatCompositionRequest(
                fallbackActor,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.UseActorBaseStats));

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
        RuntimeActorState supplied = CreateActor("supplied_hosted", 30m);
        RuntimeActorState vessel = CreateActor(
            "mismatched_vessel",
            5m,
            new RuntimeActorRosterSnapshot(activeHostedEntity: Reference(expected)));
        RuntimeActorSnapshot before = vessel.ToSnapshot();

        RuntimeActorStatCompositionResult result = new RuntimeActorStatCompositionService().Compose(
            new RuntimeActorStatCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                supplied));

        Assert.False(result.Applied);
        Assert.Equal(
            RuntimeActorStatCompositionDiagnosticCode.ActiveHostedEntityIdentityMismatch,
            Assert.Single(result.Diagnostics).Code);
        AssertCompositionStateUnchanged(before, vessel.ToSnapshot());
    }

    [Fact]
    public void ActorComposition_RejectsEveryActorRosterInvariantWithoutMutation()
    {
        RuntimeActorReferenceSnapshot first = ActorReference("owned_first");
        RuntimeActorReferenceSnapshot second = ActorReference("owned_second");
        RuntimeActorRosterSnapshot[] invalidRosters =
        [
            new(hostedEntityRoster: [first, first]),
            new(companionRoster: [first, first]),
            new(hostedEntityRoster: [first], companionRoster: [first]),
            new(activeHostedEntity: second, hostedEntityRoster: [second])
        ];

        Assert.Throws<ArgumentException>(() =>
            CreateActor("invalid_constructor_roster", 5m, invalidRosters[0]));

        for (int index = 0; index < invalidRosters.Length; index++)
        {
            RuntimeActorState actor = CreateActor($"invalid_roster_{index}", 5m);
            RuntimeActorSnapshot before = actor.ToSnapshot();

            RuntimeActorStatCompositionResult result = new RuntimeActorStatCompositionService().Compose(
                new RuntimeActorStatCompositionRequest(
                    actor,
                    RuntimeStatSourceKind.Actor,
                    MissingHostedEntityBehavior.UseActorBaseStats,
                    rosters: invalidRosters[index]));

            Assert.False(result.Applied);
            Assert.Equal(
                RuntimeActorStatCompositionDiagnosticCode.RosterInvariantViolation,
                Assert.Single(result.Diagnostics).Code);
            AssertCompositionStateUnchanged(before, actor.ToSnapshot());
        }
    }

    [Fact]
    public void ActorRosterInvariantRules_ReturnOrderedImmutableDiagnostics()
    {
        RuntimeActorReferenceSnapshot reference = ActorReference("repeated_actor");
        var roster = new RuntimeActorRosterSnapshot(
            activeHostedEntity: reference,
            hostedEntityRoster: [reference, reference],
            companionRoster: [reference, reference]);

        IReadOnlyList<RuntimeActorRosterInvariantDiagnostic> diagnostics =
            RuntimeActorRosterInvariantRules.Validate(roster);

        Assert.Equal(
            [
                RuntimeActorRosterInvariantCode.DuplicateHostedEntityReference,
                RuntimeActorRosterInvariantCode.DuplicateCompanionReference,
                RuntimeActorRosterInvariantCode.ActiveHostedEntityDuplicatedInRoster,
                RuntimeActorRosterInvariantCode.ActiveHostedEntityDuplicatedInRoster,
                RuntimeActorRosterInvariantCode.HostedEntityCompanionRoleCollision,
                RuntimeActorRosterInvariantCode.HostedEntityCompanionRoleCollision
            ],
            diagnostics.Select(diagnostic => diagnostic.Code));
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<RuntimeActorRosterInvariantDiagnostic>>(
            diagnostics);
    }

    [Fact]
    public void ActorComposition_ResolutionFailureIsAtomic()
    {
        RuntimeActorState hostedEntity = CreateActor("atomic_hosted", 20m);
        RuntimeActorState vessel = CreateActor(
            "atomic_vessel",
            5m,
            new RuntimeActorRosterSnapshot(activeHostedEntity: Reference(hostedEntity)));
        RuntimeActorSnapshot before = vessel.ToSnapshot();
        var service = new RuntimeActorStatCompositionService(
            new ThrowingStatResolutionPolicy(StandardProgressionIds.Magic),
            _resources);

        RuntimeActorStatCompositionResult result = service.Compose(
            new RuntimeActorStatCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                hostedEntity));

        Assert.False(result.Applied);
        Assert.Equal(
            RuntimeActorStatCompositionDiagnosticCode.StatResolutionFailed,
            Assert.Single(result.Diagnostics).Code);
        AssertCompositionStateUnchanged(before, vessel.ToSnapshot());
    }

    [Fact]
    public void ActorComposition_DrivesBattleDamageFromTheHostedEntityStats()
    {
        RuntimeActorState hostedEntity = CreateActor("damage_hosted", 20m);
        RuntimeActorState vessel = CreateActor(
            "damage_vessel",
            5m,
            new RuntimeActorRosterSnapshot(activeHostedEntity: Reference(hostedEntity)));
        RuntimeActorState actorSourced = CreateActor("damage_actor_source", 5m);
        RuntimeActorState target = CreateActor("damage_target", 5m);
        RuntimeActorStatCompositionResult composition = new RuntimeActorStatCompositionService().Compose(
            new RuntimeActorStatCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                hostedEntity));
        Assert.True(composition.Applied);

        var ruleset = new ProductionCombatRuleset(new MinimumRandomSource());
        ProductionDamageResolutionResult composedDamage = ruleset.ResolveDamage(
            DamageRequest(vessel, target));
        ProductionDamageResolutionResult actorDamage = ruleset.ResolveDamage(
            DamageRequest(actorSourced, target));

        Assert.True(composedDamage.TotalDamage > actorDamage.TotalDamage);

        static ProductionDamageResolutionRequest DamageRequest(
            RuntimeActorState attacker,
            RuntimeActorState defender) =>
            new(
                ProductionCombatRuleset.FromRuntimeActor(attacker),
                ProductionCombatRuleset.FromRuntimeActor(defender),
                DamageElement.Physical,
                ElementalAffinity.Normal,
                Power: 20,
                Accuracy: 100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1));
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
        var extremeConfig = new StandardStatPolicyConfig(
            statCap: int.MaxValue,
            buffMultiplier: decimal.MaxValue);
        var stats = new StandardStatResolutionPolicy(extremeConfig);

        StatResolutionResult stat = stats.Resolve(new StatResolutionRequest(
            RuntimeStatSourceKind.ActiveHostedEntity,
            StandardProgressionIds.Strength,
            BaseStats(maximumStat),
            ActiveHostedEntityStats(decimal.MaxValue),
            equipmentStatModifiers: BaseStats(decimal.MaxValue),
            statStages: [new RuntimeStatStageSnapshot(StandardProgressionIds.Attack, 1)]));
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
    public void ExperienceCurve_PreservesLegacyCubicRequirement(int level, int expected)
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
        RuntimeActorRosterSnapshot? rosters = null,
        decimal hpCurrent = 50m) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            ContentId.Parse("player_team"),
            StandardProgressionIds.Hp,
            CombatDefenseProfile.Empty,
            [
                new BattleResourceState(StandardProgressionIds.Hp, hpCurrent, 100m),
                new BattleResourceState(StandardProgressionIds.Sp, 20m, 30m)
            ],
            stats: BaseStats(statValue),
            identity: new RuntimeActorIdentitySnapshot(
                RuntimeInstanceId.Parse(id),
                ContentId.Parse($"{id}_entity"),
                StandardProgressionIds.Vessel,
                id),
            baseResourceValues:
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, 20m),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, 6m)
            ],
            baseStats: BaseStats(statValue),
            rosters: rosters);

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
        Assert.Equal(expected.Ownership, actual.Ownership);
        Assert.Equal(expected.Deployment, actual.Deployment);
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
        Assert.Equal(expected.Rosters.ActiveHostedEntity, actual.Rosters.ActiveHostedEntity);
        Assert.Equal(
            expected.Rosters.HostedEntityRoster.ToArray(),
            actual.Rosters.HostedEntityRoster.ToArray());
        Assert.Equal(
            expected.Rosters.CompanionRoster.ToArray(),
            actual.Rosters.CompanionRoster.ToArray());
        Assert.Equal(
            expected.Equipment.EquippedItemIds.OrderBy(pair => pair.Key).ToArray(),
            actual.Equipment.EquippedItemIds.OrderBy(pair => pair.Key).ToArray());
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
