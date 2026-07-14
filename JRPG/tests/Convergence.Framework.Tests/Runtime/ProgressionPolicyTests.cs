using System.Reflection;
using Convergence.Content;
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
    [InlineData("human", 10)]
    [InlineData("operator", 10)]
    [InlineData("persona_user", 18)]
    [InlineData("wild_card", 18)]
    [InlineData("demon", 20)]
    public void StatPolicy_ResolvesClassSpecificStrengthComposition(string actorKind, int expected)
    {
        StatResolutionResult result = _stats.Resolve(new StatResolutionRequest(
            Id(actorKind),
            StandardProgressionIds.Strength,
            BaseStats(10),
            ActiveFormStats(20)));

        Assert.Equal(expected, result.FinalValue);
    }

    [Theory]
    [InlineData("vitality", 15)]
    [InlineData("agility", 15)]
    [InlineData("luck", 20)]
    public void StatPolicy_UsesApprovedPersonaWeights(string stat, int expected)
    {
        StatResolutionResult result = _stats.Resolve(new StatResolutionRequest(
            StandardProgressionIds.WildCard,
            Id(stat),
            BaseStats(10),
            ActiveFormStats(20)));

        Assert.Equal(expected, result.FinalValue);
    }

    [Fact]
    public void StatPolicy_AppliesAccessoryModifiersBeforeCapAndStagesAfterCap()
    {
        StatResolutionResult result = _stats.Resolve(new StatResolutionRequest(
            StandardProgressionIds.Human,
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
                StandardProgressionIds.Human,
                stat,
                BaseStats(10),
                statStages: [stage])).FinalValue;
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
            buffMultiplier: decimal.MaxValue,
            activeFormWeights:
            [
                new KeyValuePair<ContentId, decimal>(
                    StandardProgressionIds.Strength,
                    decimal.MaxValue)
            ]);
        var stats = new StandardStatResolutionPolicy(extremeConfig);

        StatResolutionResult stat = stats.Resolve(new StatResolutionRequest(
            StandardProgressionIds.WildCard,
            StandardProgressionIds.Strength,
            BaseStats(maximumStat),
            ActiveFormStats(maximumStat),
            equipmentStatModifiers: BaseStats(maximumStat),
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
    public void LevelGrowth_AppliesMultiLevelHumanoidGrowthWithDeterministicBaseResourceRolls()
    {
        var growth = new StandardLevelGrowthPolicy(_curve, _resources);

        LevelGrowthResult result = growth.ApplyExperience(new LevelGrowthRequest(
            new RuntimeProgressionSnapshot(1, 0, 0, 0),
            new RuntimeStatBlockSnapshot(BaseStats(2), BaseStats(2)),
            StandardProgressionIds.Human,
            experienceAward: 13,
            new SequenceRandomSource(6, 3, 10, 7),
            ProgressionSubjectKind.Actor,
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
    public void LevelGrowth_FormGrowthIncrementsRandomStatAndRespectsCap()
    {
        var growth = new StandardLevelGrowthPolicy(_curve, _resources);

        LevelGrowthResult result = growth.ApplyExperience(new LevelGrowthRequest(
            new RuntimeProgressionSnapshot(1, 0, 0, 0),
            new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 39)],
                [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 39)]),
            StandardProgressionIds.WildCard,
            experienceAward: 13,
            new SequenceRandomSource(0, 0),
            ProgressionSubjectKind.Form));

        Assert.True(result.Applied);
        Assert.Equal(3, result.Progression.Level);
        Assert.Equal(40, result.Stats.BaseStats[StandardProgressionIds.Strength]);
        Assert.Equal(1, result.LevelUps[0].StatIncreases[StandardProgressionIds.Strength]);
        Assert.Empty(result.LevelUps[1].StatIncreases);
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
            StandardProgressionIds.Human,
            experienceAward: -1,
            new SequenceRandomSource(),
            ProgressionSubjectKind.Actor));

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
            StandardProgressionIds.Human,
            experienceAward: 1,
            new SequenceRandomSource(),
            ProgressionSubjectKind.Actor));

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
            StandardProgressionIds.Human,
            experienceAward: 1,
            new SequenceRandomSource(),
            ProgressionSubjectKind.Actor));

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

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static KeyValuePair<ContentId, decimal>[] BaseStats(decimal value) =>
    [
        new(StandardProgressionIds.Strength, value),
        new(StandardProgressionIds.Magic, value),
        new(StandardProgressionIds.Vitality, value),
        new(StandardProgressionIds.Agility, value),
        new(StandardProgressionIds.Luck, value)
    ];

    private static KeyValuePair<ContentId, decimal>[] ActiveFormStats(decimal value) => BaseStats(value);

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
}
