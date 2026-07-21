using System.Reflection;
using System.Text.Json;
using Convergence.Catalog;
using Convergence.Content;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class DomainDefinitionTests
{
    [Fact]
    public void ClosedVocabularies_MatchTheApprovedContract()
    {
        Assert.Equal(8, Enum.GetValues<DamageElement>().Length);
        Assert.Equal(6, Enum.GetValues<ElementalAffinity>().Length);
        Assert.Equal(4, Enum.GetValues<ResistanceLevel>().Length);
        Assert.Equal(6, Enum.GetValues<SkillMenuGroup>().Length);
        Assert.Equal(13, Enum.GetValues<InheritanceGroup>().Length);
        Assert.DoesNotContain(Enum.GetNames<SkillActivation>(), name => name == "Special");
        Assert.DoesNotContain(Enum.GetNames<InheritanceGroup>(), name => name == "Special");
    }

    [Fact]
    public void EffectHierarchy_ContainsEveryApprovedEffectType()
    {
        string[] effectTypes = typeof(EffectDefinition).Assembly.GetTypes()
            .Where(type => type.IsSealed && type.IsSubclassOf(typeof(EffectDefinition)))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                nameof(AnalyzeEffectDefinition),
                nameof(ApplyAilmentEffectDefinition),
                nameof(BreakAffinityEffectDefinition),
                nameof(CustomEffectDefinition),
                nameof(DamageEffectDefinition),
                nameof(EscapeEffectDefinition),
                nameof(GrantChargeEffectDefinition),
                nameof(GrantShieldEffectDefinition),
                nameof(InstantKillEffectDefinition),
                nameof(ModifyStatStageEffectDefinition),
                nameof(OverrideAffinityEffectDefinition),
                nameof(ReduceResourceEffectDefinition),
                nameof(RemoveAilmentEffectDefinition),
                nameof(RemoveStatusEffectDefinition),
                nameof(RestoreResourceEffectDefinition),
                nameof(ReviveEffectDefinition),
                nameof(SetResourceEffectDefinition)
            },
            effectTypes);
    }

    [Fact]
    public void AilmentTurnBehaviorHierarchy_ContainsEveryApprovedVariant()
    {
        Assert.Equal(
            Enum.GetValues<AilmentTurnBehaviorKind>().Length,
            typeof(AilmentTurnBehaviorDefinition).Assembly.GetTypes().Count(type =>
                type.IsSealed && type.IsSubclassOf(typeof(AilmentTurnBehaviorDefinition))));
    }

    [Fact]
    public void ContentId_NormalizesLocalAndQualifiedIds()
    {
        var local = ContentId.Parse("  Ice_Boost  ");
        var qualified = ContentId.Parse(" Convergence.Core:Ice_Boost ");

        Assert.Equal("ice_boost", local.ToString());
        Assert.False(local.IsQualified);
        Assert.Equal("convergence.core:ice_boost", qualified.ToString());
        Assert.True(qualified.IsQualified);
        Assert.Equal(ContentId.Parse("ICE_BOOST"), local);
        Assert.True(ContentId.TryParse("sample.pack:valid_id", out ContentId parsed));
        Assert.Equal("sample.pack:valid_id", parsed.Value);
    }

    [Fact]
    public void ContentId_DefaultValueIsExplicitlyEmptyAndInvalid()
    {
        ContentId id = default;

        Assert.True(id.IsEmpty);
        Assert.False(id.IsValid);
        Assert.False(id.IsQualified);
        Assert.Equal(string.Empty, id.Value);
        Assert.Equal(string.Empty, id.ToString());
        Assert.True(ContentId.Parse("valid_id").IsValid);
        Assert.False(ContentId.Parse("valid_id").IsEmpty);
    }

    [Fact]
    public void EffectLocalIdsAndDependenciesAreTypedLocalValues()
    {
        EffectLocalId effectId = EffectLocalId.Parse("  Primary_Hit  ");
        var dependency = new EffectDependencyDefinition(
            effectId,
            EffectDependencyRequirement.PositiveDamage,
            EffectDependencyScope.SameTarget);
        var effect = new ApplyAilmentEffectDefinition(ContentId.Parse("poison"), 50)
        {
            EffectId = EffectLocalId.Parse("poison_rider"),
            Dependency = dependency
        };

        Assert.Equal("primary_hit", effectId.Value);
        Assert.Equal(effectId, dependency.SourceEffectId);
        Assert.Equal(EffectDependencyRequirement.PositiveDamage, effect.Dependency!.Requirement);
        Assert.False(default(EffectLocalId).IsValid);
        Assert.False(EffectLocalId.TryParse("pack:effect", out _));
        Assert.Throws<ArgumentException>(() => EffectLocalId.Parse("pack:effect"));
        Assert.Throws<ArgumentException>(() =>
            new EffectDependencyDefinition(
                default,
                EffectDependencyRequirement.Succeeded,
                EffectDependencyScope.SameTarget));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EffectDependencyDefinition(effectId, (EffectDependencyRequirement)99, EffectDependencyScope.SameTarget));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EffectDependencyDefinition(effectId, EffectDependencyRequirement.Succeeded, (EffectDependencyScope)99));
    }

    [Fact]
    public void DamageContactMode_DefaultsToIndependentAndRejectsUndefinedValues()
    {
        var damage = new DamageEffectDefinition(
            DamageElement.Physical,
            10,
            100,
            new NeverCriticalDefinition(),
            new HitCountDefinition(1, 1));

        Assert.Equal(DamageContactMode.Independent, damage.ContactMode);
        Assert.Equal(
            DamageContactMode.SharedContact,
            (damage with { ContactMode = DamageContactMode.SharedContact }).ContactMode);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            damage with { ContactMode = (DamageContactMode)int.MaxValue });
    }

    [Fact]
    public void EquipmentBasicAttackSnapshotsAndComposesOrderedSecondaryEffects()
    {
        EffectLocalId primaryId = EffectLocalId.Parse("weapon_contact");
        var source = new List<EffectDefinition>
        {
            new ApplyAilmentEffectDefinition(ContentId.Parse("burn"), 25)
            {
                Dependency = new EffectDependencyDefinition(
                    primaryId,
                    EffectDependencyRequirement.PositiveDamage,
                    EffectDependencyScope.SameTarget)
            }
        };
        var basicAttack = new EquipmentBasicAttackDefinition(
            DamageElement.Physical,
            10,
            90,
            new NeverCriticalDefinition(),
            false)
        {
            PrimaryEffectId = primaryId,
            SecondaryEffects = source
        };

        source.Clear();
        IReadOnlyList<EffectDefinition> composed = basicAttack.ComposeEffects();

        Assert.Single(basicAttack.SecondaryEffects);
        Assert.Equal(2, composed.Count);
        Assert.Equal(primaryId, Assert.IsType<DamageEffectDefinition>(composed[0]).EffectId);
        Assert.IsType<ApplyAilmentEffectDefinition>(composed[1]);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EffectDefinition>)basicAttack.SecondaryEffects).Add(
                new AnalyzeEffectDefinition([AnalysisLayer.Stats])));
        Assert.Throws<ArgumentException>(() => basicAttack with { PrimaryEffectId = default(EffectLocalId) });
    }

    [Fact]
    public void EquipmentQualificationQualifiesSecondaryContentReferencesButKeepsLocalEffectIds()
    {
        EffectLocalId primaryId = EffectLocalId.Parse("weapon_contact");
        var equipment = new EquipmentDefinition(
            ContentId.Parse("ember_blade"),
            "Ember Blade",
            "Test weapon.",
            EquipmentSlot.Weapon,
            10,
            weapon: new EquipmentWeaponProfileDefinition(new EquipmentBasicAttackDefinition(
                DamageElement.Physical,
                10,
                100,
                new NeverCriticalDefinition(),
                false)
            {
                PrimaryEffectId = primaryId,
                SecondaryEffects =
                [
                    new ApplyAilmentEffectDefinition(ContentId.Parse("burn"), 25)
                    {
                        Dependency = new EffectDependencyDefinition(
                            primaryId,
                            EffectDependencyRequirement.PositiveDamage,
                            EffectDependencyScope.SameTarget)
                    }
                ]
            }));

        EquipmentDefinition qualified = DefinitionQualifier.Equipment("sample.pack", equipment);
        EquipmentBasicAttackDefinition attack = qualified.Weapon!.BasicAttack;

        Assert.Equal(ContentId.Parse("sample.pack:ember_blade"), qualified.Id);
        Assert.Equal(primaryId, attack.PrimaryEffectId);
        Assert.Equal(primaryId, Assert.Single(attack.SecondaryEffects).Dependency!.SourceEffectId);
        Assert.Equal(
            ContentId.Parse("sample.pack:burn"),
            Assert.IsType<ApplyAilmentEffectDefinition>(Assert.Single(attack.SecondaryEffects)).AilmentId);
    }

    [Fact]
    public void CatalogQualificationPreservesSequenceLocalEffectReferences()
    {
        EffectLocalId sourceId = EffectLocalId.Parse("primary_hit");
        SkillDefinition local = new(
            ContentId.Parse("qualified_dependency"),
            "Qualified Dependency",
            "Exercises local effect IDs through qualification.",
            SkillActivation.Active,
            SkillMenuGroup.Ailment,
            InheritanceGroup.Ailment,
            new SkillInheritanceDefinition(true),
            targeting: EnemyTargeting(),
            effects:
            [
                new DamageEffectDefinition(
                    DamageElement.Physical,
                    10,
                    100,
                    new NeverCriticalDefinition(),
                    new HitCountDefinition(1, 1))
                {
                    EffectId = sourceId
                },
                new ApplyAilmentEffectDefinition(ContentId.Parse("poison"), 50)
                {
                    Dependency = new EffectDependencyDefinition(
                        sourceId,
                        EffectDependencyRequirement.PositiveDamage,
                        EffectDependencyScope.SameTarget)
                }
            ],
            availability: new SkillAvailabilityDefinition([ContentId.Parse("battle")]));

        SkillDefinition qualified = DefinitionQualifier.Skill("sample.pack", local);

        Assert.Equal(ContentId.Parse("sample.pack:qualified_dependency"), qualified.Id);
        Assert.Equal(sourceId, qualified.Effects[0].EffectId);
        Assert.Equal(sourceId, qualified.Effects[1].Dependency!.SourceEffectId);
        Assert.Equal(ContentId.Parse("sample.pack:poison"),
            Assert.IsType<ApplyAilmentEffectDefinition>(qualified.Effects[1]).AilmentId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ice boost")]
    [InlineData("ice-boost")]
    [InlineData("_ice_boost")]
    [InlineData("ice__boost")]
    [InlineData("pack::ice_boost")]
    [InlineData("pack.:ice_boost")]
    [InlineData(":ice_boost")]
    [InlineData("pack:")]
    public void ContentId_RejectsMalformedIds(string value)
    {
        Assert.False(ContentId.TryParse(value, out _));
        Assert.Throws<ArgumentException>(() => ContentId.Parse(value));
    }

    [Fact]
    public void Salvation_ComposesRestorationAndAilmentRemoval()
    {
        var salvation = new SkillDefinition(
            ContentId.Parse("salvation"),
            "Salvation",
            "Fully restores HP and removes removable ailments.",
            SkillActivation.Active,
            SkillMenuGroup.Recovery,
            InheritanceGroup.Recovery,
            new SkillInheritanceDefinition(true),
            targeting: AllyTargeting(),
            effects:
            [
                new RestoreResourceEffectDefinition(ContentId.Parse("hp"), new FullAmountDefinition()),
                new RemoveAilmentEffectDefinition(AilmentRemovalScope.AllRemovable)
            ]);

        Assert.Collection(
            salvation.Effects,
            effect => Assert.IsType<RestoreResourceEffectDefinition>(effect),
            effect => Assert.IsType<RemoveAilmentEffectDefinition>(effect));
    }

    [Fact]
    public void Regenerate_UsesOrdinaryRestoreEffectFromTurnEndTrigger()
    {
        var trigger = new PassiveTriggerDefinition(
            ContentId.Parse("owner_turn_end"),
            [new RestoreResourceEffectDefinition(ContentId.Parse("hp"), new PercentMaximumAmountDefinition(2))]);
        var regenerate = PassiveSkill("regenerate_1", triggers: [trigger]);

        PassiveTriggerDefinition actualTrigger = Assert.Single(regenerate.Triggers);
        Assert.Equal(ContentId.Parse("owner_turn_end"), actualTrigger.EventId);
        var restore = Assert.IsType<RestoreResourceEffectDefinition>(Assert.Single(actualTrigger.Effects));
        Assert.Equal(new PercentMaximumAmountDefinition(2), restore.Amount);
    }

    [Fact]
    public void IceBoost_IsPassiveModifierFilteredToIce()
    {
        var modifier = new NumericRuleModifierDefinition(
            NumericRuleModifierType.DamageDealt,
            ModifierOperation.Multiply,
            1.25m,
            new EffectElementConditionDefinition(DamageElement.Ice));
        var iceBoost = PassiveSkill("ice_boost", modifiers: [modifier]);

        Assert.Equal(SkillActivation.Passive, iceBoost.Activation);
        Assert.Null(iceBoost.MenuGroup);
        Assert.Equal(InheritanceGroup.Passive, iceBoost.InheritanceGroup);
        var actual = Assert.IsType<NumericRuleModifierDefinition>(Assert.Single(iceBoost.Modifiers));
        Assert.Equal(1.25m, actual.Value);
        var condition = Assert.IsType<EffectElementConditionDefinition>(actual.When);
        Assert.Equal(DamageElement.Ice, condition.Element);
    }

    [Fact]
    public void InstantKill_DefinesChannelAndExplicitBypassAsDifferentTypes()
    {
        var hama = new InstantKillEffectDefinition(
            30,
            new ChannelInstantDeathResistanceCheckDefinition(InstantDeathChannel.Light));
        var eternalRest = new InstantKillEffectDefinition(
            100,
            new NoInstantDeathResistanceCheckDefinition(),
            new HasAilmentConditionDefinition(ConditionSubject.Target, [ContentId.Parse("sleep")]));

        var channel = Assert.IsType<ChannelInstantDeathResistanceCheckDefinition>(hama.ResistanceCheck);
        Assert.Equal(InstantDeathChannel.Light, channel.Channel);
        Assert.IsType<NoInstantDeathResistanceCheckDefinition>(eternalRest.ResistanceCheck);
        Assert.IsType<HasAilmentConditionDefinition>(eternalRest.When);
    }

    [Fact]
    public void EntityDefinition_SeparatesDefenseAndInheritanceSystems()
    {
        var entity = new EntityDefinition(
            ContentId.Parse("ash_wisp"),
            "Ash Wisp",
            "A minor spirit.",
            ContentId.Parse("companion"),
            ContentId.Parse("spirit"),
            1,
            5,
            new EntityCapabilitiesDefinition(true, true, true),
            new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(
                    InheritanceGroupPolicyMode.DenyList,
                    [InheritanceGroup.Ice])),
            new Dictionary<ContentId, int> { [ContentId.Parse("magic")] = 7 },
            new Dictionary<DamageElement, ElementalAffinity> { [DamageElement.Ice] = ElementalAffinity.Weak },
            new Dictionary<ContentId, ResistanceLevel> { [ContentId.Parse("poison")] = ResistanceLevel.Resistant },
            new Dictionary<InstantDeathChannel, ResistanceLevel> { [InstantDeathChannel.Dark] = ResistanceLevel.Immune });

        Assert.Equal(ElementalAffinity.Weak, entity.ElementalAffinities[DamageElement.Ice]);
        Assert.Equal(ResistanceLevel.Resistant, entity.AilmentResistances[ContentId.Parse("poison")]);
        Assert.Equal(ResistanceLevel.Immune, entity.InstantDeathResistances[InstantDeathChannel.Dark]);
        Assert.Equal(InheritanceGroup.Ice, Assert.Single(entity.InheritanceRules.GroupPolicy.GroupIds));
    }

    [Fact]
    public void RaceAndAilmentDefinitions_RepresentApprovedSupportingContent()
    {
        var race = new RaceDefinition(
            ContentId.Parse("fairy"),
            "Fairy",
            [ContentId.Parse("neutral")],
            ContentId.Parse("playful"));
        var poison = new AilmentDefinition(
            ContentId.Parse("poison"),
            "Poison",
            "Deals damage at the end of the afflicted combatant's turn.",
            new TurnDurationDefinition(3, ContentId.Parse("owner_turn_end"), true),
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1, 0, 1, 1, false),
            new AilmentRecoveryDefinition(
                new NaturalAilmentRecoveryDefinition(20, ContentId.Parse("luck"), 0.5m)),
            exclusivityGroupId: ContentId.Parse("major_ailment"),
            triggers:
            [
                new PassiveTriggerDefinition(
                    ContentId.Parse("owner_turn_end"),
                    [
                        new ReduceResourceEffectDefinition(
                            ContentId.Parse("hp"),
                            new PercentMaximumAmountDefinition(13),
                            true)
                    ])
            ]);

        Assert.Equal(ContentId.Parse("playful"), race.NegotiationPersonalityId);
        Assert.Equal(ContentId.Parse("major_ailment"), poison.ExclusivityGroupId);
        Assert.IsType<NormalAilmentTurnBehaviorDefinition>(poison.TurnBehavior);
        Assert.IsType<ReduceResourceEffectDefinition>(Assert.Single(Assert.Single(poison.Triggers).Effects));
    }

    [Fact]
    public void Definitions_DefensivelyCopyInputCollections()
    {
        var effects = new List<EffectDefinition>
        {
            new AnalyzeEffectDefinition([AnalysisLayer.Affinities])
        };
        var costs = new List<SkillCostDefinition>
        {
            new(ContentId.Parse("sp"), new FlatAmountDefinition(10))
        };
        var owners = new List<ContentId> { ContentId.Parse("owner_one") };
        var parameters = new Dictionary<string, object?> { ["ratio"] = 1m };
        var skill = new SkillDefinition(
            ContentId.Parse("inspect"),
            "Inspect",
            "Reference utility skill.",
            SkillActivation.Active,
            SkillMenuGroup.Utility,
            InheritanceGroup.Utility,
            new SkillInheritanceDefinition(true, owners),
            costs: costs,
            targeting: EnemyTargeting(),
            effects: effects);
        var custom = new CustomEffectDefinition(ContentId.Parse("sample_handler"), parameters);

        effects.Clear();
        costs.Clear();
        owners.Clear();
        parameters["ratio"] = 9m;

        Assert.Single(skill.Effects);
        Assert.Single(skill.Costs);
        Assert.Single(skill.Inheritance.ExclusiveOwnerEntityIds);
        Assert.Equal(1m, custom.Parameters["ratio"]);
    }

    [Fact]
    public void CustomParameters_RecursivelyNormalizeAndFreezeDirectClrValues()
    {
        var nested = new Dictionary<string, object?> { ["enabled"] = true };
        var items = new List<object?> { 1, nested };
        var parameters = new Dictionary<string, object?>
        {
            ["items"] = items,
            ["ratio"] = 1.25m
        };

        var custom = new CustomEffectDefinition(ContentId.Parse("sample_handler"), parameters);

        items[0] = 99;
        nested["enabled"] = false;
        parameters["ratio"] = 9m;

        IReadOnlyList<object?> frozenItems = Assert.IsAssignableFrom<IReadOnlyList<object?>>(
            custom.Parameters["items"]);
        IReadOnlyDictionary<string, object?> frozenNested =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(frozenItems[1]);
        Assert.Equal(1L, frozenItems[0]);
        Assert.Equal(true, frozenNested["enabled"]);
        Assert.Equal(1.25m, custom.Parameters["ratio"]);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<object?>)frozenItems).Add("late mutation"));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, object?>)frozenNested).Add("late", true));
    }

    [Fact]
    public void CustomParameters_RejectForeignValuesAndReferenceCycles()
    {
        using JsonDocument document = JsonDocument.Parse("{\"value\":1}");
        object[] unsupported =
        [
            document.RootElement,
            new object(),
            0.5d,
            ulong.MaxValue,
            new HashSet<object?> { 1L, 2L }
        ];

        foreach (object value in unsupported)
        {
            Assert.Throws<ArgumentException>(() => new CustomEffectDefinition(
                ContentId.Parse("sample_handler"),
                [new KeyValuePair<string, object?>("invalid", value)]));
        }

        var cycle = new List<object?>();
        cycle.Add(cycle);
        Assert.Throws<ArgumentException>(() => new CustomEffectDefinition(
            ContentId.Parse("sample_handler"),
            [new KeyValuePair<string, object?>("cycle", cycle)]));

        object? deeplyNested = "leaf";
        for (int depth = 0; depth < 66; depth++)
        {
            deeplyNested = new List<object?> { deeplyNested };
        }

        Assert.Throws<ArgumentException>(() => new CustomEffectDefinition(
            ContentId.Parse("sample_handler"),
            [new KeyValuePair<string, object?>("deep", deeplyNested)]));
    }

    [Fact]
    public void AffinityBreak_DefensivelyCopiesAffectedElements()
    {
        var elements = new List<DamageElement> { DamageElement.Fire };
        var definition = new BreakAffinityEffectDefinition(
            elements,
            new BattleDurationDefinition());

        elements.Clear();

        Assert.Equal([DamageElement.Fire], definition.Elements);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<DamageElement>)definition.Elements).Add(DamageElement.Ice));
    }

    [Fact]
    public void DefinitionNamespace_DoesNotReferenceLegacyRuntimeTypes()
    {
        Type[] definitionTypes = typeof(SkillDefinition).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(SkillDefinition).Namespace)
            .ToArray();

        foreach (Type definitionType in definitionTypes)
        {
            IEnumerable<Type> exposedTypes = definitionType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(property => Flatten(property.PropertyType))
                .Concat(definitionType.GetConstructors().SelectMany(constructor =>
                    constructor.GetParameters().SelectMany(parameter => Flatten(parameter.ParameterType))));

            Assert.DoesNotContain(exposedTypes, type =>
                type.Assembly != typeof(SkillDefinition).Assembly &&
                type.Namespace?.StartsWith("System", StringComparison.Ordinal) != true);
        }
    }

    private static SkillDefinition PassiveSkill(
        string id,
        IEnumerable<PassiveTriggerDefinition>? triggers = null,
        IEnumerable<RuleModifierDefinition>? modifiers = null)
    {
        return new SkillDefinition(
            ContentId.Parse(id),
            id,
            "Reference passive.",
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            triggers: triggers,
            modifiers: modifiers);
    }

    private static TargetingDefinition AllyTargeting()
    {
        return new TargetingDefinition(
            TargetRelation.Ally,
            TargetSelection.All,
            TargetLifeState.Alive,
            true);
    }

    private static TargetingDefinition EnemyTargeting()
    {
        return new TargetingDefinition(
            TargetRelation.Enemy,
            TargetSelection.Single,
            TargetLifeState.Alive,
            false,
            new TargetCountDefinition(1, 1));
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;

        if (type.IsArray)
        {
            foreach (Type nested in Flatten(type.GetElementType()!))
            {
                yield return nested;
            }
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Flatten(argument))
            {
                yield return nested;
            }
        }
    }
}
