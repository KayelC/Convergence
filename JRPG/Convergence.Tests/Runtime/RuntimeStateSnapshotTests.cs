using System.Reflection;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class RuntimeStateSnapshotTests
{
    [Theory]
    [InlineData(" HERO-0001 ", "hero-0001")]
    [InlineData("battle:hero.0001", "battle:hero.0001")]
    [InlineData("save_slot_1:orpheus-2", "save_slot_1:orpheus-2")]
    public void RuntimeInstanceId_NormalizesStableNonContentIdentity(string input, string expected)
    {
        var id = RuntimeInstanceId.Parse(input);

        Assert.Equal(expected, id.ToString());
        Assert.True(RuntimeInstanceId.TryParse(input, out RuntimeInstanceId parsed));
        Assert.Equal(id, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hero 1")]
    [InlineData("hero/1")]
    public void RuntimeInstanceId_RejectsEmptyWhitespaceOrUnsupportedCharacters(string input)
    {
        Assert.False(RuntimeInstanceId.TryParse(input, out _));
        Assert.Throws<ArgumentException>(() => RuntimeInstanceId.Parse(input));
    }

    [Fact]
    public void RuntimeActorSnapshot_RoundTripsEveryProtectedActorStateGroup()
    {
        List<RuntimeResourceSnapshot> resources =
        [
            new(Id("hp"), 72, 120),
            new(Id("sp"), 18, 44)
        ];
        List<ContentId> learnedSkills = [Id("agi"), Id("ice_boost")];
        List<ContentId> capabilityIds = [Id("analyze"), Id("switch_form")];
        List<RuntimeActorReferenceSnapshot> personaStock =
        [
            new(RuntimeInstanceId.Parse("persona:orpheus_1"), Id("convergence.demo:orpheus"), "Orpheus")
        ];

        RuntimeActorSnapshot snapshot = CreateCompleteSnapshot(
            resources,
            learnedSkills,
            personaStock,
            capabilityIds);

        resources.Add(new RuntimeResourceSnapshot(Id("extra"), 1, 1));
        learnedSkills.Add(Id("late_mutation"));
        capabilityIds.Add(Id("late_capability"));
        personaStock.Add(new RuntimeActorReferenceSnapshot(
            RuntimeInstanceId.Parse("persona:late_1"),
            Id("convergence.demo:late"),
            "Late"));

        RuntimeActorState restoredState = Restore(snapshot);
        RuntimeActorSnapshot roundTrip = restoredState.ToSnapshot();

        Assert.Equal(RuntimeInstanceId.Parse("actor:hero_0001"), roundTrip.Identity.InstanceId);
        Assert.Equal(Id("convergence.demo:hero"), roundTrip.Identity.EntityDefinitionId);
        Assert.Equal(Id("wild_card"), roundTrip.Identity.ActorKindId);
        Assert.Equal("Hero", roundTrip.Identity.DisplayName);
        Assert.Equal("SEES", roundTrip.Identity.DisplaySubtitle);
        Assert.Equal(Id("player"), roundTrip.Ownership.ControllerId);
        Assert.Equal(Id("party"), roundTrip.Ownership.TeamId);
        Assert.Equal(RuntimeInstanceId.Parse("save:player_profile"), roundTrip.Ownership.OwnerInstanceId);
        Assert.Equal(RuntimeActorDeployment.Deployed, roundTrip.Deployment.Deployment);
        Assert.True(roundTrip.Deployment.IsActive);
        Assert.True(roundTrip.Deployment.HasSwappedThisTurn);
        Assert.Equal(14, roundTrip.Progression.Level);
        Assert.Equal(230, roundTrip.Progression.Experience);
        Assert.Equal(880, roundTrip.Progression.LifetimeExperience);
        Assert.Equal(3, roundTrip.Progression.UnspentStatPoints);

        Assert.Equal([Id("hp"), Id("sp")], roundTrip.Resources.Select(resource => resource.ResourceId));
        Assert.Equal(72, roundTrip.Resources[0].Current);
        Assert.Equal(120, roundTrip.Resources[0].Maximum);
        Assert.Equal(10, roundTrip.Stats.BaseStats[Id("strength")]);
        Assert.Equal(13, roundTrip.Stats.EffectiveStats[Id("strength")]);
        Assert.Equal([Id("agi"), Id("ice_boost")], roundTrip.Skills.LearnedSkillIds);
        Assert.Equal([Id("agi")], roundTrip.Skills.EquippedSkillIds);
        Assert.Equal([Id("analyze"), Id("switch_form")], roundTrip.CapabilityIds);
        Assert.True(restoredState.HasCapability(Id("analyze")));
        Assert.False(restoredState.HasCapability(Id("late_capability")));
        Assert.Equal(RuntimeInstanceId.Parse("persona:orpheus_1"), roundTrip.Forms.ActiveForm!.InstanceId);
        Assert.Single(roundTrip.Forms.PersonaStock);
        Assert.Single(roundTrip.Forms.DemonStock);
        Assert.Equal(Id("convergence.demo:practice_sword"), roundTrip.Equipment.EquippedItemIds[EquipmentSlot.Weapon]);
        Assert.Equal(Id("convergence.demo:kevlar_vest"), roundTrip.Equipment.EquippedItemIds[EquipmentSlot.Armor]);

        Assert.Equal(Id("poison"), Assert.Single(roundTrip.BattleStatus.Ailments).Id);
        Assert.Equal(Id("downed"), Assert.Single(roundTrip.BattleStatus.Statuses).Id);
        Assert.Equal(2, Assert.Single(roundTrip.BattleStatus.StatStages).Stage);
        Assert.Equal(2.5m, Assert.Single(roundTrip.BattleStatus.Charges).Multiplier);
        Assert.Equal(ShieldKind.Magical, Assert.Single(roundTrip.BattleStatus.Shields).Kind);
        Assert.Equal(DamageElement.Fire, Assert.Single(roundTrip.BattleStatus.AffinityOverrides).Element);
        Assert.IsType<TurnDurationDefinition>(Assert.Single(roundTrip.BattleStatus.Ailments).Duration);
        Assert.IsType<TurnDurationDefinition>(Assert.Single(roundTrip.BattleStatus.AffinityOverrides).Duration);
        Assert.Equal(Id("hp"), roundTrip.VitalResourceId);
        Assert.True(roundTrip.BattleStatus.IsGuarding);
        Assert.Equal([AnalysisLayer.Stats, AnalysisLayer.Affinities], Assert.Single(roundTrip.BattleStatus.Analysis).Layers);
        Assert.Equal(1, Assert.Single(roundTrip.BattleActivations.PassiveActivations).ActivationCount);
        RuntimePassiveSkillStateSnapshot passiveState =
            Assert.Single(roundTrip.BattleActivations.PassiveSkillStates);
        Assert.Equal(Id("endure"), passiveState.SkillId);
        Assert.False(passiveState.IsEnabled);
        Assert.False(Assert.Single(restoredState.Passives.Entries).IsEnabled);
    }

    [Fact]
    public void RuntimeResourceTransactions_ReturnBeforeAfterSnapshotsAndRejectInvalidMutation()
    {
        RuntimeActorState actor = Restore(CreateCompleteSnapshot());
        var transactions = new RuntimeResourceTransactionService();
        ContentId hp = Id("hp");

        RuntimeMutationResult applied = transactions.SetResource(actor, hp, 40);

        Assert.True(applied.Applied);
        Assert.Equal(RuntimeMutationStatus.Applied, applied.Status);
        Assert.Empty(applied.Diagnostics);
        Assert.Equal(72, applied.Before.Resources.Single(resource => resource.ResourceId == hp).Current);
        Assert.Equal(40, applied.After.Resources.Single(resource => resource.ResourceId == hp).Current);
        Assert.Equal(40, actor.ToSnapshot().Resources.Single(resource => resource.ResourceId == hp).Current);

        RuntimeMutationResult rejected = transactions.AddResource(actor, hp, 200);

        Assert.False(rejected.Applied);
        Assert.Equal(RuntimeMutationStatus.Rejected, rejected.Status);
        Assert.Equal(RuntimeMutationErrorCode.ResourceValueOutOfRange, Assert.Single(rejected.Diagnostics).Code);
        Assert.Equal("$.resources[0].current", rejected.Diagnostics[0].Path);
        Assert.Equal(40, rejected.Before.Resources.Single(resource => resource.ResourceId == hp).Current);
        Assert.Equal(40, rejected.After.Resources.Single(resource => resource.ResourceId == hp).Current);
        Assert.Equal(40, actor.ToSnapshot().Resources.Single(resource => resource.ResourceId == hp).Current);

        RuntimeMutationResult missing = transactions.SetResource(actor, Id("mp"), 1);

        Assert.False(missing.Applied);
        Assert.Equal(RuntimeMutationErrorCode.MissingResource, Assert.Single(missing.Diagnostics).Code);
        Assert.Equal("$.resources", missing.Diagnostics[0].Path);
    }

    [Fact]
    public void RuntimeResourceTransactions_ApplyRecalculationReplacesCurrentAndMaximumValues()
    {
        RuntimeActorState actor = Restore(CreateCompleteSnapshot());
        var recalculation = new ResourceRecalculationResult(
        [
            new RuntimeResourceSnapshot(Id("hp"), 72, 150),
            new RuntimeResourceSnapshot(Id("sp"), 18, 60)
        ]);

        RuntimeMutationResult result =
            new RuntimeResourceTransactionService().ApplyRecalculation(actor, recalculation);

        Assert.True(result.Applied);
        Assert.Equal(120, result.Before.Resources.Single(resource => resource.ResourceId == Id("hp")).Maximum);
        Assert.Equal(150, result.After.Resources.Single(resource => resource.ResourceId == Id("hp")).Maximum);
        Assert.Equal(60, actor.GetRequiredResource(Id("sp")).Maximum);
    }

    [Fact]
    public void RuntimeProgressionTransactions_ApplyGrowthResultToActorSnapshot()
    {
        RuntimeActorState actor = Restore(CreateCompleteSnapshot());
        RuntimeActorSnapshot before = actor.ToSnapshot();
        var growth = new LevelGrowthResult(
            ProgressionMutationStatus.Applied,
            new RuntimeProgressionSnapshot(15, 10, 930, 4),
            new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(Id("strength"), 11)],
                [new KeyValuePair<ContentId, decimal>(Id("strength"), 14)]),
            [new RuntimeResourceSnapshot(Id("hp"), 82, 130)],
            [new KeyValuePair<ContentId, decimal>(Id("hp"), 130)],
            [new LevelUpEvent(15, statPointsAwarded: 1)]);

        RuntimeMutationResult result = new RuntimeProgressionTransactionService().ApplyLevelGrowth(actor, growth);

        Assert.True(result.Applied);
        Assert.Equal(before.Progression.Level, result.Before.Progression.Level);
        Assert.Equal(15, result.After.Progression.Level);
        Assert.Equal(10, actor.ToSnapshot().Progression.Experience);
        Assert.Equal(930, actor.ToSnapshot().Progression.LifetimeExperience);
        Assert.Equal(4, actor.ToSnapshot().Progression.UnspentStatPoints);
        Assert.Equal(11, actor.ToSnapshot().Stats.BaseStats[Id("strength")]);
        Assert.Equal(82, actor.ToSnapshot().Resources.Single(resource => resource.ResourceId == Id("hp")).Current);
        Assert.Equal(130, actor.ToSnapshot().BaseResourceValues[Id("hp")]);
    }

    [Fact]
    public void CanonicalActorState_GrowthResourcesAndBattleLifecycleMutateOneObject()
    {
        RuntimeActorState actor = Restore(CreateCompleteSnapshot());
        RuntimeActorState canonicalReference = actor;
        var resources = new RuntimeResourceTransactionService();
        var progression = new RuntimeProgressionTransactionService();
        var lifecycle = new BattleStatusLifecycleService(new FixedRandomSource());

        RuntimeMutationResult resourceResult = resources.SetResource(actor, Id("hp"), 50);
        var growth = new LevelGrowthResult(
            ProgressionMutationStatus.Applied,
            new RuntimeProgressionSnapshot(15, 10, 930, 4),
            new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(Id("strength"), 11)],
                [new KeyValuePair<ContentId, decimal>(Id("strength"), 14)]),
            [
                new RuntimeResourceSnapshot(Id("hp"), 60, 130),
                new RuntimeResourceSnapshot(Id("sp"), 18, 44)
            ],
            [new KeyValuePair<ContentId, decimal>(Id("hp"), 130)],
            [new LevelUpEvent(15, statPointsAwarded: 1)]);
        RuntimeMutationResult growthResult = progression.ApplyLevelGrowth(actor, growth);
        BattleTurnStartLifecycleResult turnStart = lifecycle.ProcessTurnStart(
            new BattleTurnStartLifecycleRequest(actor));

        Assert.Same(canonicalReference, actor);
        Assert.True(resourceResult.Applied);
        Assert.True(growthResult.Applied);
        Assert.Equal(15, actor.Progression.Level);
        Assert.Equal(60, actor.GetRequiredResource(Id("hp")).Current);
        Assert.False(actor.IsGuarding);
        Assert.Contains(turnStart.Events, item => item.Kind == BattleStatusLifecycleEventKind.GuardCleared);
        Assert.Equal(actor.Progression, growthResult.After.Progression);
        Assert.Equal(
            actor.GetRequiredResource(Id("hp")).Current,
            growthResult.After.Resources.Single(resource => resource.ResourceId == Id("hp")).Current);
    }

    [Fact]
    public void RuntimeSnapshotContracts_ExposeNoHostOrLegacyTypes()
    {
        Type[] runtimeTypes = typeof(RuntimeActorSnapshot).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == "JRPGPrototype.Logic.Runtime")
            .ToArray();

        string[] forbidden =
        [
            "System.Console",
            "System.IO",
            "System.Text.Json",
            "Newtonsoft",
            "Godot",
            "JRPGPrototype.Data.Database",
            "JRPGPrototype.Data.SkillData",
            "JRPGPrototype.Data.PersonaData",
            "JRPGPrototype.Data.ItemData",
            "JRPGPrototype.Entities.Combatant",
            "JRPGPrototype.Entities.Persona",
            "JRPGPrototype.Services.IGameIO"
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

    private static RuntimeActorSnapshot CreateCompleteSnapshot(
        IEnumerable<RuntimeResourceSnapshot>? resources = null,
        IEnumerable<ContentId>? learnedSkillIds = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? personaStock = null,
        IEnumerable<ContentId>? capabilityIds = null)
    {
        RuntimeActorReferenceSnapshot activeForm = new(
            RuntimeInstanceId.Parse("persona:orpheus_1"),
            Id("convergence.demo:orpheus"),
            "Orpheus");

        return new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(
                RuntimeInstanceId.Parse("actor:hero_0001"),
                Id("convergence.demo:hero"),
                Id("wild_card"),
                "Hero",
                "SEES"),
            new RuntimeActorOwnershipSnapshot(
                Id("player"),
                Id("party"),
                RuntimeInstanceId.Parse("save:player_profile")),
            new RuntimeActorDeploymentSnapshot(RuntimeActorDeployment.Deployed, IsActive: true, HasSwappedThisTurn: true),
            new RuntimeProgressionSnapshot(level: 14, experience: 230, lifetimeExperience: 880, unspentStatPoints: 3),
            resources ??
            [
                new RuntimeResourceSnapshot(Id("hp"), 72, 120),
                new RuntimeResourceSnapshot(Id("sp"), 18, 44)
            ],
            new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(Id("strength"), 10)],
                [new KeyValuePair<ContentId, decimal>(Id("strength"), 13)]),
            new RuntimeSkillStateSnapshot(learnedSkillIds ?? [Id("agi"), Id("ice_boost")], [Id("agi")]),
            new RuntimeFormStockSnapshot(
                activeForm,
                personaStock ?? [activeForm],
                [
                    new RuntimeActorReferenceSnapshot(
                        RuntimeInstanceId.Parse("demon:pixie_1"),
                        Id("convergence.demo:pixie"),
                        "Pixie")
                ]),
            new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Weapon, Id("convergence.demo:practice_sword")),
                new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Armor, Id("convergence.demo:kevlar_vest"))
            ]),
            new RuntimeBattleStatusSnapshot(
                ailments: [new RuntimeTimedStateSnapshot(Id("poison"), Turns(3))],
                statuses: [new RuntimeTimedStateSnapshot(Id("downed"), Turns(1), isRemovable: false)],
                statStages: [new RuntimeStatStageSnapshot(Id("attack"), stage: 2, Turns(3))],
                charges: [new RuntimeChargeSnapshot(ChargeKind.Magical, 2.5m, Turns(1))],
                shields: [new RuntimeShieldSnapshot(ShieldKind.Magical, Turns(1))],
                affinityOverrides: [new RuntimeAffinityOverrideSnapshot(DamageElement.Fire, ElementalAffinity.Normal, Turns(2))],
                isGuarding: true,
                analysis:
                [
                    new RuntimeAnalysisSnapshot(
                        RuntimeInstanceId.Parse("enemy:shadow_1"),
                        [AnalysisLayer.Stats, AnalysisLayer.Affinities])
                ]),
            new RuntimeBattleActivationSnapshot(
                [
                    new RuntimePassiveActivationSnapshot(Id("endure"), Id("owner_would_be_defeated"), triggerIndex: 0, activationCount: 1)
                ],
                [new RuntimePassiveSkillStateSnapshot(Id("endure"), IsEnabled: false)]),
            [new KeyValuePair<ContentId, decimal>(Id("hp"), 120)],
            Id("hp"),
            capabilityIds ?? [Id("analyze"), Id("switch_form")]);
    }

    private static RuntimeActorState Restore(RuntimeActorSnapshot snapshot) =>
        RuntimeActorState.Restore(
            snapshot,
            CombatDefenseProfile.Empty,
            [new SkillDefinition(
                Id("endure"),
                "Endure",
                "Test passive.",
                SkillActivation.Passive,
                null,
                InheritanceGroup.Passive,
                new SkillInheritanceDefinition(true))],
            [new AilmentDefinition(
                Id("poison"),
                "Poison",
                "Test ailment.",
                Turns(3),
                new NormalAilmentTurnBehaviorDefinition(),
                new AilmentModifiersDefinition(1, 0, 1, 1, false),
                new AilmentRecoveryDefinition())]);

    private static TurnDurationDefinition Turns(int value) =>
        new(value, Id("owner_turn_end"), false);

    private sealed class FixedRandomSource : JRPGPrototype.Hosting.IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;
        public decimal NextUnitDecimal() => 0m;
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

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
}
