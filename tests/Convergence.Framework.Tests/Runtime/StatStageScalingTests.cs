using Convergence.Battle;
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Hosting;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class StatStageScalingTests
{
    [Theory]
    [InlineData(-4, 0.50)]
    [InlineData(-3, 0.625)]
    [InlineData(-2, 0.75)]
    [InlineData(-1, 0.875)]
    [InlineData(0, 1.00)]
    [InlineData(1, 1.25)]
    [InlineData(2, 1.50)]
    [InlineData(3, 1.75)]
    [InlineData(4, 2.00)]
    public void StandardPolicy_ResolvesEveryOffenseStageDistinctly(int stage, double expected)
    {
        var policy = new StandardStatStageScalingPolicy();

        StatStageScalingResult result = policy.Resolve(new StatStageScalingRequest(
            StatStageScalingChannel.PhysicalDamageDealt,
            [new RuntimeStatStageSnapshot(StandardProgressionIds.PhysicalAttack, stage)]));

        Assert.Equal(Convert.ToDecimal(expected), result.Multiplier);
        if (stage == 0)
        {
            Assert.Empty(result.AppliedMultipliers);
        }
        else
        {
            AppliedStatStageMultiplier applied = Assert.Single(result.AppliedMultipliers);
            Assert.Equal(StandardProgressionIds.PhysicalAttack, applied.TrackId);
            Assert.Equal(stage, applied.Stage);
        }
    }

    [Theory]
    [InlineData(-4, 2.00)]
    [InlineData(-3, 1.75)]
    [InlineData(-2, 1.50)]
    [InlineData(-1, 1.25)]
    [InlineData(0, 1.00)]
    [InlineData(1, 0.875)]
    [InlineData(2, 0.75)]
    [InlineData(3, 0.625)]
    [InlineData(4, 0.50)]
    public void StandardPolicy_ResolvesEveryDefenseStageAsDamageTaken(int stage, double expected)
    {
        var policy = new StandardStatStageScalingPolicy();

        StatStageScalingResult result = policy.Resolve(new StatStageScalingRequest(
            StatStageScalingChannel.DamageTaken,
            [new RuntimeStatStageSnapshot(StandardProgressionIds.Defense, stage)]));

        Assert.Equal(Convert.ToDecimal(expected), result.Multiplier);
    }

    [Fact]
    public void StandardPolicy_TracksAffectOnlyTheirApprovedChannels()
    {
        var policy = new StandardStatStageScalingPolicy();
        RuntimeStatStageSnapshot[] stages =
        [
            new(StandardProgressionIds.PhysicalAttack, 1),
            new(StandardProgressionIds.MagicalAttack, 2),
            new(StandardProgressionIds.Attack, 1),
            new(StandardProgressionIds.Defense, 1),
            new(StandardProgressionIds.AgilityTrack, 1)
        ];

        Assert.Equal(1.5625m, Resolve(StatStageScalingChannel.PhysicalDamageDealt));
        Assert.Equal(1.875m, Resolve(StatStageScalingChannel.MagicalDamageDealt));
        Assert.Equal(0.875m, Resolve(StatStageScalingChannel.DamageTaken));
        Assert.Equal(1.25m, Resolve(StatStageScalingChannel.HitChance));
        Assert.Equal(1.25m, Resolve(StatStageScalingChannel.Evasion));

        decimal Resolve(StatStageScalingChannel channel) =>
            policy.Resolve(new StatStageScalingRequest(channel, stages)).Multiplier;
    }

    [Fact]
    public void StandardPolicy_AllowsOneTableOverrideWithoutChangingOtherChannels()
    {
        var customPhysical = new StatStageScalingTable(
            StandardProgressionIds.PhysicalAttack,
            StatStageScalingChannel.PhysicalDamageDealt,
            CompleteTable(stage => stage == 1 ? 3m : 1m));
        var policy = new StandardStatStageScalingPolicy([customPhysical]);
        RuntimeStatStageSnapshot stage = new(StandardProgressionIds.PhysicalAttack, 1);

        Assert.Equal(3m, policy.Resolve(new StatStageScalingRequest(
            StatStageScalingChannel.PhysicalDamageDealt,
            [stage])).Multiplier);
        Assert.Equal(1m, policy.Resolve(new StatStageScalingRequest(
            StatStageScalingChannel.MagicalDamageDealt,
            [stage])).Multiplier);
    }

    [Fact]
    public void StandardPolicy_RejectsMalformedOrUnsupportedTables()
    {
        Assert.Throws<ArgumentException>(() => new StatStageScalingTable(
            StandardProgressionIds.Attack,
            StatStageScalingChannel.PhysicalDamageDealt,
            CompleteTable(_ => 1m).Append(new StatStageMultiplier(4, 2m))));
        Assert.Throws<ArgumentException>(() => new StatStageScalingTable(
            StandardProgressionIds.Attack,
            StatStageScalingChannel.PhysicalDamageDealt,
            CompleteTable(_ => 1m).Where(entry => entry.Stage != 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StatStageMultiplier(1, 0m));

        var unsupported = new StatStageScalingTable(
            ContentId.Parse("luck"),
            StatStageScalingChannel.DamageTaken,
            CompleteTable(_ => 1m));
        Assert.Throws<ArgumentException>(() => new StandardStatStageScalingPolicy([unsupported]));
        Assert.Throws<ArgumentException>(() => new StandardStatStageScalingPolicy(
        [
            Override(StandardProgressionIds.Attack, StatStageScalingChannel.PhysicalDamageDealt),
            Override(StandardProgressionIds.Attack, StatStageScalingChannel.PhysicalDamageDealt)
        ]));
    }

    [Fact]
    public void StageDuration_DoesNotChangeMagnitude()
    {
        var policy = new StandardStatStageScalingPolicy();
        RuntimeStatStageSnapshot permanent = new(StandardProgressionIds.Attack, 2);
        RuntimeStatStageSnapshot timed = new(
            StandardProgressionIds.Attack,
            2,
            new TurnDurationDefinition(3, ContentId.Parse("owner_turn_end"), SuspendWhileReserve: true));

        Assert.Equal(
            policy.Resolve(new StatStageScalingRequest(
                StatStageScalingChannel.MagicalDamageDealt,
                [permanent])).Multiplier,
            policy.Resolve(new StatStageScalingRequest(
                StatStageScalingChannel.MagicalDamageDealt,
                [timed])).Multiplier);
    }

    [Fact]
    public void CombatProfiles_ApplyStageChannelsWithoutMutatingRawStatsOrResources()
    {
        RuntimeActorState attacker = Actor("attacker");
        RuntimeActorState defender = Actor("defender");
        attacker.ChangeStatStage(StandardProgressionIds.PhysicalAttack, 1, duration: null);
        attacker.ChangeStatStage(StandardProgressionIds.AgilityTrack, 1, duration: null);
        defender.ChangeStatStage(StandardProgressionIds.Defense, 1, duration: null);
        defender.ChangeStatStage(StandardProgressionIds.AgilityTrack, 1, duration: null);
        RuntimeActorSnapshot attackerBefore = attacker.ToSnapshot();
        RuntimeActorSnapshot defenderBefore = defender.ToSnapshot();
        var ruleset = new ProductionCombatRuleset(new MinimumRandomSource());

        ProductionCombatantProfile attackerProfile = ruleset.CreateCombatantProfile(attacker);
        ProductionCombatantProfile defenderProfile = ruleset.CreateCombatantProfile(defender);

        Assert.Equal(1.25m, attackerProfile.Modifiers.PhysicalDamageDealtMultiplier);
        Assert.Equal(1m, attackerProfile.Modifiers.MagicalDamageDealtMultiplier);
        Assert.Equal(1.25m, attackerProfile.Modifiers.HitMultiplier);
        Assert.Equal(0.875m, defenderProfile.Modifiers.DamageTakenMultiplier);
        Assert.Equal(1.25m, defenderProfile.Modifiers.EvasionMultiplier);
        Assert.Equal(
            attackerBefore.Stats.BaseStats.OrderBy(pair => pair.Key.Value),
            attacker.ToSnapshot().Stats.BaseStats.OrderBy(pair => pair.Key.Value));
        Assert.Equal(
            attackerBefore.Stats.EffectiveStats.OrderBy(pair => pair.Key.Value),
            attacker.ToSnapshot().Stats.EffectiveStats.OrderBy(pair => pair.Key.Value));
        Assert.Equal(attackerBefore.Resources.ToArray(), attacker.ToSnapshot().Resources.ToArray());
        Assert.Equal(
            defenderBefore.Stats.BaseStats.OrderBy(pair => pair.Key.Value),
            defender.ToSnapshot().Stats.BaseStats.OrderBy(pair => pair.Key.Value));
        Assert.Equal(
            defenderBefore.Stats.EffectiveStats.OrderBy(pair => pair.Key.Value),
            defender.ToSnapshot().Stats.EffectiveStats.OrderBy(pair => pair.Key.Value));
        Assert.Equal(defenderBefore.Resources.ToArray(), defender.ToSnapshot().Resources.ToArray());
    }

    [Fact]
    public void StatRulesetBinding_UsesDefaultsAndAcceptsTypedTableOverrides()
    {
        ContentId rulesetId = ContentId.Parse("test.pack:custom_stat");
        var definition = new RulesetDefinition(
            rulesetId,
            "Custom stat",
            "Overrides one standard stage table.",
            RulesetCategory.Stat,
            StandardRulesetPolicyIds.StandardStat,
            [new KeyValuePair<string, object?>("stageTables", AuthoredTables())]);
        var catalog = new GameDataCatalog(
            [],
            [],
            [],
            [],
            [],
            rulesets: [KeyValuePair.Create(rulesetId, definition)]);
        var resolver = new RuntimeRulesetBindingResolver(RuntimeRulesetPolicyFactoryRegistry.CreateStandard());

        StatRulesetServices services = resolver.BindStatServices(catalog, rulesetId).RequireService();

        Assert.IsType<StandardStatResolutionPolicy>(services.StatResolutionPolicy);
        Assert.Equal(3m, services.StageScalingPolicy.Resolve(new StatStageScalingRequest(
            StatStageScalingChannel.PhysicalDamageDealt,
            [new RuntimeStatStageSnapshot(StandardProgressionIds.PhysicalAttack, 1)])).Multiplier);
    }

    [Fact]
    public void StatRulesetBinding_RejectsUnknownAndIncompleteAuthoredTables()
    {
        RulesetBindingResult<StatRulesetServices> unknown = Bind(
            [new KeyValuePair<string, object?>("mystery", 1L)]);
        RulesetBindingResult<StatRulesetServices> incomplete = Bind(
        [
            new KeyValuePair<string, object?>(
                "stageTables",
                Array.AsReadOnly<object?>(
                [
                    new Dictionary<string, object?>
                    {
                        ["trackId"] = "attack",
                        ["channel"] = "physical_damage_dealt",
                        ["multipliers"] = Array.AsReadOnly<object?>(
                        [
                            new Dictionary<string, object?>
                            {
                                ["stage"] = 1L,
                                ["multiplier"] = 1.25m
                            }
                        ])
                    }
                ]))
        ]);

        Assert.Contains(unknown.Diagnostics, diagnostic =>
            diagnostic.Code == RulesetBindingDiagnosticCode.UnknownParameter);
        Assert.Contains(incomplete.Diagnostics, diagnostic =>
            diagnostic.Code == RulesetBindingDiagnosticCode.InvalidParameterValue &&
            diagnostic.ParameterName == "stageTables");
    }

    private static RulesetBindingResult<StatRulesetServices> Bind(
        IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        ContentId id = ContentId.Parse("test.pack:stat");
        var catalog = new GameDataCatalog(
            [],
            [],
            [],
            [],
            [],
            rulesets:
            [
                KeyValuePair.Create(
                    id,
                    new RulesetDefinition(
                        id,
                        "Stat",
                        "Test stat policy.",
                        RulesetCategory.Stat,
                        StandardRulesetPolicyIds.StandardStat,
                        parameters))
            ]);
        return new RuntimeRulesetBindingResolver(RuntimeRulesetPolicyFactoryRegistry.CreateStandard())
            .BindStatServices(catalog, id);
    }

    private static IReadOnlyList<object?> AuthoredTables() =>
        Array.AsReadOnly<object?>(
        [
            new Dictionary<string, object?>
            {
                ["trackId"] = "physical_attack",
                ["channel"] = "physical_damage_dealt",
                ["multipliers"] = Array.AsReadOnly<object?>(CompleteTable(stage => stage == 1 ? 3m : 1m)
                    .Select(entry => (object?)new Dictionary<string, object?>
                    {
                        ["stage"] = (long)entry.Stage,
                        ["multiplier"] = entry.Multiplier
                    })
                    .ToArray())
            }
        ]);

    private static StatStageScalingTable Override(
        ContentId trackId,
        StatStageScalingChannel channel) =>
        new(trackId, channel, CompleteTable(_ => 1m));

    private static StatStageMultiplier[] CompleteTable(Func<int, decimal> multiplier) =>
        Enumerable.Range(
                BattleStatStageRange.Minimum,
                BattleStatStageRange.Maximum - BattleStatStageRange.Minimum + 1)
            .Select(stage => new StatStageMultiplier(stage, multiplier(stage)))
            .ToArray();

    private static RuntimeActorState Actor(string id) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            ContentId.Parse("team"),
            StandardProgressionIds.Hp,
            CombatDefenseProfile.Empty,
            [new BattleResourceState(StandardProgressionIds.Hp, 100m, 100m)],
            stats:
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 10m),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Magic, 10m),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 10m),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Agility, 10m),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Luck, 10m)
            ]);

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;
        public decimal NextUnitDecimal() => 0m;
    }
}
