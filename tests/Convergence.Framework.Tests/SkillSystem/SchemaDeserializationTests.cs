using System.Collections;
using System.Reflection;
using System.Text.Json;
using Convergence.Content;
using Convergence.Serialization;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class SchemaDeserializationTests
{
    private readonly SkillSystemJsonDeserializer _deserializer = new();

    [Fact]
    public void ReferencePack_DeserializesThroughPortableBoundary()
    {
        string jsonRoot = Path.Combine(AppContext.BaseDirectory, "Content");

        ContentPackManifest manifest = _deserializer.DeserializeManifest(
            File.ReadAllText(TestContentPath.Resolve(jsonRoot, "skill_system_redesign.manifest.sample.json")),
            "manifest.sample.json");
        DeserializedContentDocument<SkillDefinition> skills = _deserializer.DeserializeSkills(
            File.ReadAllText(TestContentPath.Resolve(jsonRoot, "skill_system_redesign.skills.sample.json")),
            "skills.sample.json");
        DeserializedContentDocument<EntityDefinition> entities = _deserializer.DeserializeEntities(
            File.ReadAllText(TestContentPath.Resolve(jsonRoot, "skill_system_redesign.entities.sample.json")),
            "entities.sample.json");
        DeserializedContentDocument<RaceDefinition> races = _deserializer.DeserializeRaces(
            File.ReadAllText(TestContentPath.Resolve(jsonRoot, "skill_system_redesign.races.sample.json")),
            "races.sample.json");

        Assert.Equal(8, manifest.SchemaVersion);
        Assert.Equal(3, manifest.Documents.Count);
        SkillDefinition iceBoost = Assert.Single(skills.Records);
        EntityDefinition cinder = Assert.Single(entities.Records);
        Assert.Single(races.Records);
        Assert.Equal(InheritanceGroup.Passive, iceBoost.InheritanceGroup);
        Assert.Equal(InheritanceGroup.Ice, Assert.Single(cinder.InheritanceRules.GroupPolicy.GroupIds));
        Assert.IsType<EffectElementConditionDefinition>(
            Assert.IsType<NumericRuleModifierDefinition>(Assert.Single(iceBoost.Modifiers)).When);
    }

    [Fact]
    public void ActiveSkill_MapsEveryApprovedEffectAndAvailability()
    {
        string json = WrapSkill(
            """
            {
              "id": "complete_active",
              "displayName": "Complete Active",
              "description": "Exercises every active effect.",
              "activation": "active",
              "menuGroup": "utility",
              "inheritanceGroupId": "utility",
              "inheritance": { "isInheritable": true },
              "costs": [
                { "resourceId": "sp", "amount": { "type": "flat", "value": 5 } },
                { "resourceId": "hp", "amount": { "type": "percent_current", "value": 10 } }
              ],
              "targeting": {
                "relation": "enemy", "selection": "single", "lifeState": "alive", "allowSelf": false,
                "count": { "minimum": 1, "maximum": 1 }
              },
              "availability": { "contexts": ["battle", "field"] },
              "effects": [
                {
                  "type": "damage", "effectId": "primary_hit",
                  "elementId": "physical", "power": 60, "accuracy": 90,
                  "critical": { "mode": "chance", "chance": 20 },
                  "hits": { "minimum": 1, "maximum": 2, "distribution": "uniform" },
                  "drain": "hp", "onFailure": "stop_target"
                },
                {
                  "type": "instant_kill", "chance": 30,
                  "resistanceCheck": { "mode": "channel", "channelId": "light" },
                  "dependency": {
                    "sourceEffectId": "primary_hit",
                    "requirement": "succeeded",
                    "scope": "same_target"
                  }
                },
                {
                  "type": "apply_ailment", "ailmentId": "poison", "chance": 40,
                  "lifetime": {
                    "expiration": { "type": "battle" },
                    "allowedRemovalCauses": ["duration_expired", "battle_end"]
                  }
                },
                { "type": "restore_resource", "resourceId": "hp", "amount": { "type": "full" } },
                { "type": "remove_ailment", "scope": "selected", "ailmentIds": ["poison"], "ailmentGroupIds": ["mental"] },
                { "type": "revive", "resourceId": "hp", "amount": { "type": "percent_max", "value": 50 } },
                { "type": "modify_stat_stage", "modifierTrackIds": ["defense"], "stageDelta": 1, "duration": { "type": "turns", "value": 3, "tick": "owner_turn_end", "suspendWhileReserve": true } },
                {
                  "type": "grant_charge", "charge": "physical", "multiplier": 2.5,
                  "lifetime": {
                    "expiration": { "type": "phase", "phaseId": "next_attack" },
                    "allowedRemovalCauses": ["duration_expired", "consumed"]
                  }
                },
                {
                  "type": "grant_shield", "shield": "magical",
                  "lifetime": {
                    "expiration": { "type": "permanent" },
                    "allowedRemovalCauses": ["scripted_removal"]
                  }
                },
                {
                  "type": "break_affinity", "elementIds": ["fire", "ice"],
                  "lifetime": {
                    "expiration": { "type": "battle" },
                    "allowedRemovalCauses": ["duration_expired", "battle_end"]
                  }
                },
                {
                  "type": "override_affinity", "elementIds": ["fire", "ice"], "affinityId": "normal",
                  "lifetime": {
                    "expiration": { "type": "instant" },
                    "allowedRemovalCauses": ["duration_expired"]
                  }
                },
                { "type": "remove_status_effect", "statusKinds": ["buff", "charge"], "statusIds": ["focus"] },
                { "type": "reduce_resource", "resourceId": "hp", "amount": { "type": "power", "power": 80 }, "canReduceToZero": true },
                { "type": "set_resource", "resourceId": "sp", "amount": { "type": "formula", "formulaId": "sample_formula", "parameters": { "ratio": 1.5 } } },
                { "type": "analyze", "layers": ["stats", "affinities", "skills"] },
                { "type": "escape", "eligibilityRuleId": "battle_escape", "chance": 100 },
                { "type": "custom", "handlerId": "sample_handler", "parameters": { "enabled": true } }
              ]
            }
            """);

        SkillDefinition skill = Assert.Single(_deserializer.DeserializeSkills(json, "full-effects.json").Records);

        Assert.Equal(
            new[]
            {
                typeof(DamageEffectDefinition), typeof(InstantKillEffectDefinition), typeof(ApplyAilmentEffectDefinition),
                typeof(RestoreResourceEffectDefinition), typeof(RemoveAilmentEffectDefinition), typeof(ReviveEffectDefinition),
                typeof(ModifyStatStageEffectDefinition), typeof(GrantChargeEffectDefinition), typeof(GrantShieldEffectDefinition),
                typeof(BreakAffinityEffectDefinition), typeof(OverrideAffinityEffectDefinition), typeof(RemoveStatusEffectDefinition), typeof(ReduceResourceEffectDefinition),
                typeof(SetResourceEffectDefinition), typeof(AnalyzeEffectDefinition), typeof(EscapeEffectDefinition),
                typeof(CustomEffectDefinition)
            },
            skill.Effects.Select(effect => effect.GetType()));
        Assert.Equal([ContentId.Parse("battle"), ContentId.Parse("field")], skill.Availability!.ContextIds);
        Assert.IsType<ChanceCriticalDefinition>(Assert.IsType<DamageEffectDefinition>(skill.Effects[0]).Critical);
        Assert.Equal(EffectLocalId.Parse("primary_hit"), skill.Effects[0].EffectId);
        EffectDependencyDefinition dependency = skill.Effects[1].Dependency!;
        Assert.Equal(EffectLocalId.Parse("primary_hit"), dependency.SourceEffectId);
        Assert.Equal(EffectDependencyRequirement.Succeeded, dependency.Requirement);
        Assert.Equal(EffectDependencyScope.SameTarget, dependency.Scope);
        Assert.IsType<ChannelInstantDeathResistanceCheckDefinition>(
            Assert.IsType<InstantKillEffectDefinition>(skill.Effects[1]).ResistanceCheck);
        Assert.Equal(
            [StatusRemovalCause.DurationExpired, StatusRemovalCause.BattleEnd],
            Assert.IsType<ApplyAilmentEffectDefinition>(skill.Effects[2]).Lifetime!
                .RemovalProfile.AllowedCauses);
        BreakAffinityEffectDefinition affinityBreak = Assert.IsType<BreakAffinityEffectDefinition>(skill.Effects[9]);
        Assert.Equal([DamageElement.Fire, DamageElement.Ice], affinityBreak.Elements);
        Assert.IsType<BattleDurationDefinition>(affinityBreak.Lifetime.Expiration);
        Assert.Equal(
            [StatusRemovalCause.DurationExpired, StatusRemovalCause.BattleEnd],
            affinityBreak.Lifetime.RemovalProfile.AllowedCauses);
        Assert.Equal(
            [StatusRemovalCause.DurationExpired, StatusRemovalCause.Consumed],
            Assert.IsType<GrantChargeEffectDefinition>(skill.Effects[7]).Lifetime!.RemovalProfile.AllowedCauses);
        Assert.Equal(
            [StatusRemovalCause.ScriptedRemoval],
            Assert.IsType<GrantShieldEffectDefinition>(skill.Effects[8]).Lifetime!.RemovalProfile.AllowedCauses);
        Assert.Equal(
            [StatusRemovalCause.DurationExpired],
            Assert.IsType<OverrideAffinityEffectDefinition>(skill.Effects[10]).Lifetime.RemovalProfile.AllowedCauses);
        Assert.IsType<FormulaAmountDefinition>(Assert.IsType<SetResourceEffectDefinition>(skill.Effects[13]).Amount);
    }

    [Fact]
    public void InstantDeathChecks_MapChannelAndNoChannelModes()
    {
        string json = WrapSkill(MinimalActiveRecord(
            """
            [
              { "type": "instant_kill", "chance": 30, "resistanceCheck": { "mode": "channel", "channelId": "dark" } },
              { "type": "instant_kill", "chance": 100, "resistanceCheck": { "mode": "none" } }
            ]
            """,
            ", \"availability\": { \"contexts\": [\"battle\"] }"));

        SkillDefinition skill = Assert.Single(_deserializer.DeserializeSkills(json, "instant-death.json").Records);

        Assert.Equal([ContentId.Parse("battle")], skill.Availability!.ContextIds);
        Assert.IsType<ChannelInstantDeathResistanceCheckDefinition>(
            Assert.IsType<InstantKillEffectDefinition>(skill.Effects[0]).ResistanceCheck);
        Assert.IsType<NoInstantDeathResistanceCheckDefinition>(
            Assert.IsType<InstantKillEffectDefinition>(skill.Effects[1]).ResistanceCheck);
    }

    [Fact]
    public void Conditions_AmountsAndCriticalModes_MapCompleteVocabulary()
    {
        string json = WrapSkill(
            """
            {
              "id": "condition_matrix",
              "displayName": "Condition Matrix",
              "description": "Exercises condition composition.",
              "activation": "active",
              "menuGroup": "offense",
              "inheritanceGroupId": "almighty",
              "inheritance": { "isInheritable": true },
              "targeting": { "relation": "enemy", "selection": "all", "lifeState": "alive", "allowSelf": false },
              "effects": [
                {
                  "type": "damage", "elementId": "almighty", "power": 1, "accuracy": 100,
                  "critical": { "mode": "never" }, "hits": { "minimum": 1, "maximum": 1 },
                  "when": {
                    "all": [
                      { "type": "actor_resource_percentage", "resourceId": "hp", "comparison": "greater_than", "value": 10 },
                      { "type": "target_resource_percentage", "resourceId": "sp", "comparison": "less_than_or_equal", "value": 50 },
                      { "type": "actor_has_ailment", "ailmentIds": ["poison"] },
                      { "type": "target_has_ailment", "ailmentIds": ["sleep"] },
                      { "type": "actor_has_skill", "skillId": "focus" },
                      { "type": "target_has_skill", "skillId": "guard" },
                      { "type": "actor_has_buff", "modifierTrackId": "defense" },
                      { "type": "target_has_buff", "modifierTrackId": "agility" },
                      { "type": "actor_has_affinity", "elementId": "fire", "affinityId": "resist" },
                      { "type": "target_has_affinity", "elementId": "ice", "affinityId": "weak" },
                      { "type": "actor_has_capability", "capabilityId": "summoner" },
                      { "type": "target_has_capability", "capabilityId": "recruitable" },
                      { "type": "actor_life_state", "lifeState": "alive" },
                      { "type": "target_life_state", "lifeState": "alive" },
                      { "type": "battle_kind", "allowed": ["random"] },
                      { "type": "moon_phase", "allowed": ["full_moon"] },
                      { "type": "party_size", "comparison": "greater_than_or_equal", "value": 2 },
                      { "type": "chance", "chance": 50 },
                      { "type": "effect_element_is", "elementId": "almighty" },
                      { "type": "custom", "handlerId": "condition_handler", "parameters": { "flag": true } },
                      { "any": [ { "type": "chance", "chance": 10 } ] },
                      { "not": { "type": "target_has_skill", "skillId": "immunity" } }
                    ]
                  }
                }
              ]
            }
            """);

        SkillDefinition skill = Assert.Single(_deserializer.DeserializeSkills(json, "conditions.json").Records);
        var damage = Assert.IsType<DamageEffectDefinition>(Assert.Single(skill.Effects));
        var all = Assert.IsType<AllConditionDefinition>(damage.When);

        Assert.Equal(22, all.Conditions.Count);
        Assert.Contains(all.Conditions, condition => condition is AnyConditionDefinition);
        Assert.Contains(all.Conditions, condition => condition is NotConditionDefinition);
        Assert.IsType<NeverCriticalDefinition>(damage.Critical);
        Assert.Equal(ConditionSubject.Actor,
            Assert.IsType<ResourcePercentageConditionDefinition>(all.Conditions[0]).Subject);
        Assert.Equal(ConditionSubject.Target,
            Assert.IsType<ResourcePercentageConditionDefinition>(all.Conditions[1]).Subject);
    }

    [Fact]
    public void EquipmentBasicAttack_RequiresAndMapsExplicitCriticalDefinition()
    {
        const string valid = """
        {
          "schemaVersion": 8,
          "equipment": [{
            "id": "blade", "displayName": "Blade", "description": "",
            "slot": "weapon", "baseValue": 1,
            "weapon": { "basicAttack": {
              "element": "physical", "power": 10, "accuracy": 95,
              "critical": { "mode": "chance", "chance": 12 },
              "isLongRange": false,
              "primaryEffectId": "weapon_contact",
              "secondaryEffects": [{
                "type": "damage", "elementId": "fire", "power": 4, "accuracy": 25,
                "critical": { "mode": "never" }, "hits": { "minimum": 1, "maximum": 1 },
                "contactMode": "shared_contact",
                "dependency": {
                  "sourceEffectId": "weapon_contact", "requirement": "positive_damage",
                  "scope": "same_target"
                }
              }]
            }}
          }]
        }
        """;
        const string oldShape = """
        {
          "schemaVersion": 8,
          "equipment": [{
            "id": "blade", "displayName": "Blade", "description": "",
            "slot": "weapon", "baseValue": 1,
            "weapon": { "basicAttack": {
              "element": "physical", "power": 10, "accuracy": 95,
              "isLongRange": false
            }}
          }]
        }
        """;

        EquipmentDefinition equipment = Assert.Single(
            _deserializer.DeserializeEquipment(valid, "equipment.json").Records);

        Assert.Equal(12, Assert.IsType<ChanceCriticalDefinition>(
            equipment.Weapon!.BasicAttack.Critical).Chance);
        Assert.Equal(EffectLocalId.Parse("weapon_contact"), equipment.Weapon.BasicAttack.PrimaryEffectId);
        Assert.Equal(
            DamageContactMode.SharedContact,
            Assert.IsType<DamageEffectDefinition>(
                Assert.Single(equipment.Weapon.BasicAttack.SecondaryEffects)).ContactMode);
        Assert.Throws<ContentDeserializationException>(() =>
            _deserializer.DeserializeEquipment(oldShape, "old-equipment.json"));
    }

    [Fact]
    public void PassiveSkill_MapsTriggersAndAllModifierFamilies()
    {
        string json = WrapSkill(
            """
            {
              "id": "complete_passive",
              "displayName": "Complete Passive",
              "description": "Exercises passive shapes.",
              "activation": "passive",
              "inheritanceGroupId": "passive",
              "inheritance": { "isInheritable": true },
              "triggers": [
                {
                  "event": "owner_turn_end",
                  "targeting": {
                    "scope": "owner_team", "lifeState": "alive", "includeReserveActors": false
                  },
                  "when": { "type": "chance", "chance": 100 },
                  "effects": [
                    { "type": "restore_resource", "resourceId": "hp", "amount": { "type": "percent_max", "value": 2 } }
                  ]
                }
              ],
              "modifiers": [
                { "type": "damage_dealt", "operation": "multiply", "value": 1.25, "when": { "type": "effect_element_is", "elementId": "ice" } },
                { "type": "elemental_affinity", "elementId": "fire", "affinityId": "null" },
                { "type": "ailment_resistance", "ailmentId": "poison", "resistance": "resistant" },
                {
                  "type": "basic_attack", "elementId": "physical", "drain": "hp",
                  "targeting": { "relation": "enemy", "selection": "all", "lifeState": "alive", "allowSelf": false }
                }
              ]
            }
            """);

        SkillDefinition skill = Assert.Single(_deserializer.DeserializeSkills(json, "passive.json").Records);

        PassiveTriggerDefinition trigger = Assert.Single(skill.Triggers);
        Assert.IsType<RestoreResourceEffectDefinition>(Assert.Single(trigger.Effects));
        Assert.Equal(PassiveTriggerTargetScope.OwnerTeam, trigger.Targeting.Scope);
        Assert.Equal(TargetLifeState.Alive, trigger.Targeting.LifeState);
        Assert.False(trigger.Targeting.IncludeReserveActors);
        Assert.Collection(
            skill.Modifiers,
            modifier => Assert.IsType<NumericRuleModifierDefinition>(modifier),
            modifier => Assert.IsType<ElementalAffinityRuleModifierDefinition>(modifier),
            modifier => Assert.IsType<AilmentResistanceRuleModifierDefinition>(modifier),
            modifier => Assert.IsType<BasicAttackRuleModifierDefinition>(modifier));
    }

    [Fact]
    public void PassiveTrigger_RequiresExplicitTargeting()
    {
        string json = WrapSkill(
            """
            {
              "id": "missing_targeting",
              "displayName": "Missing Targeting",
              "description": "Invalid passive trigger.",
              "activation": "passive",
              "inheritanceGroupId": "passive",
              "inheritance": { "isInheritable": true },
              "triggers": [
                {
                  "event": "owner_turn_end",
                  "effects": [
                    { "type": "restore_resource", "resourceId": "hp", "amount": { "type": "flat", "value": 1 } }
                  ]
                }
              ]
            }
            """);

        ContentDeserializationException exception = Assert.Throws<ContentDeserializationException>(
            () => _deserializer.DeserializeSkills(json, "missing-trigger-targeting.json"));

        Assert.Contains("targeting", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("triggers", exception.JsonPath ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ \"type\": \"normal\" }", typeof(NormalAilmentTurnBehaviorDefinition))]
    [InlineData("{ \"type\": \"skip\" }", typeof(SkipAilmentTurnBehaviorDefinition))]
    [InlineData("{ \"type\": \"limited_actions\", \"allowedActionIds\": [\"attack\"] }", typeof(LimitedActionsAilmentTurnBehaviorDefinition))]
    [InlineData("{ \"type\": \"chance_skip\", \"skipChance\": 25 }", typeof(ChanceSkipAilmentTurnBehaviorDefinition))]
    [InlineData("{ \"type\": \"chance_skip_or_flee\", \"skipChance\": 25, \"fleeChance\": 10, \"companionFleeOutcome\": \"recall_to_roster\" }", typeof(ChanceSkipOrFleeAilmentTurnBehaviorDefinition))]
    [InlineData("{ \"type\": \"forced_basic_attack\" }", typeof(ForcedBasicAttackAilmentTurnBehaviorDefinition))]
    [InlineData("{ \"type\": \"confused_action\" }", typeof(ConfusedActionAilmentTurnBehaviorDefinition))]
    [InlineData("{ \"type\": \"custom\", \"handlerId\": \"custom_turn\", \"parameters\": { \"weight\": 2 } }", typeof(CustomAilmentTurnBehaviorDefinition))]
    public void Ailments_MapEveryTurnBehaviour(string turnBehavior, Type expectedType)
    {
        string json = $$"""
        {
          "schemaVersion": 8,
          "ailments": [
            {
              "id": "test_ailment", "displayName": "Test", "description": "Test ailment.",
              "defaultLifetime": {
                "expiration": { "type": "turns", "value": 3, "tick": "owner_turn_end", "suspendWhileReserve": true },
                "allowedRemovalCauses": ["cure_effect", "duration_expired", "battle_end"]
              },
              "turnBehavior": {{turnBehavior}},
              "modifiers": {
                "evasionMultiplier": 1.0, "criticalChanceTakenBonus": 0,
                "damageTakenMultiplier": 1.0, "damageDealtMultiplier": 1.0, "isRigidBody": false
              },
              "recovery": { "natural": { "baseChance": 20, "statId": "luck", "statMultiplier": 0.5 }, "removeOnEvents": ["battle_end"] }
            }
          ]
        }
        """;

        AilmentDefinition ailment = Assert.Single(_deserializer.DeserializeAilments(json, "ailment.json").Records);
        Assert.IsType(expectedType, ailment.TurnBehavior);
        Assert.Equal(
            [StatusRemovalCause.CureEffect, StatusRemovalCause.DurationExpired, StatusRemovalCause.BattleEnd],
            ailment.DefaultLifetime.RemovalProfile.AllowedCauses);
    }

    [Fact]
    public void StrictReader_RejectsUnknownPropertiesAndDiscriminatorsWithPaths()
    {
        string unknownProperty = WrapSkill(
            MinimalPassiveRecord().Replace(
                "\"inheritance\": { \"isInheritable\": true }",
                "\"inheritance\": { \"isInheritable\": true }, \"mystery\": 1",
                StringComparison.Ordinal));
        string unknownEffect = WrapSkill(
            MinimalActiveRecord("[{ \"type\": \"mystery_effect\" }]"));

        ContentDeserializationException propertyError = Assert.Throws<ContentDeserializationException>(
            () => _deserializer.DeserializeSkills(unknownProperty, "unknown-property.json"));
        ContentDeserializationException discriminatorError = Assert.Throws<ContentDeserializationException>(
            () => _deserializer.DeserializeSkills(unknownEffect, "unknown-effect.json"));

        Assert.Equal("unknown-property.json", propertyError.SourceName);
        Assert.Contains("mystery", propertyError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("skills", propertyError.JsonPath ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal("mystery_effect", discriminatorError.Discriminator);
        Assert.Contains("effects", discriminatorError.JsonPath ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void StrictReader_RejectsAmbiguousConditionsAndActivationShapeConflicts()
    {
        string ambiguous = WrapSkill(MinimalActiveRecord(
            """
            [{
              "type": "analyze", "layers": ["stats"],
              "when": { "all": [], "type": "chance", "chance": 50 }
            }]
            """));
        string activeWithTrigger = WrapSkill(MinimalActiveRecord(
            "[{ \"type\": \"analyze\", \"layers\": [\"stats\"] }]",
            ", \"triggers\": [{ \"event\": \"battle_start\", \"targeting\": { \"scope\": \"owner\", \"lifeState\": \"any\", \"includeReserveActors\": true }, \"effects\": [] }]"));
        string passiveWithTargeting = WrapSkill(MinimalPassiveRecord().Replace(
            "\"inheritance\": { \"isInheritable\": true }",
            "\"inheritance\": { \"isInheritable\": true }, \"targeting\": { \"relation\": \"self\", \"selection\": \"single\", \"lifeState\": \"alive\", \"allowSelf\": true }",
            StringComparison.Ordinal));

        Assert.Throws<ContentDeserializationException>(() => _deserializer.DeserializeSkills(ambiguous, "ambiguous.json"));
        Assert.Throws<ContentDeserializationException>(() => _deserializer.DeserializeSkills(activeWithTrigger, "active-trigger.json"));
        Assert.Throws<ContentDeserializationException>(() => _deserializer.DeserializeSkills(passiveWithTargeting, "passive-targeting.json"));
    }

    [Theory]
    [InlineData("{ /* comment */ \"schemaVersion\": 5, \"skills\": [] }", "comment.json")]
    [InlineData("{ \"schemaVersion\": 5, \"skills\": [], }", "trailing-comma.json")]
    [InlineData("{ \"SchemaVersion\": 3, \"skills\": [] }", "case-mismatch.json")]
    [InlineData("{ \"schemaVersion\": 5, \"skills\": \"wrong\" }", "wrong-token.json")]
    [InlineData("{ \"schemaVersion\": 5, \"skills\": null }", "null-collection.json")]
    public void StrictReader_RejectsNonCanonicalJson(string json, string sourceName)
    {
        ContentDeserializationException error = Assert.Throws<ContentDeserializationException>(
            () => _deserializer.DeserializeSkills(json, sourceName));

        Assert.Equal(sourceName, error.SourceName);
    }

    [Fact]
    public void StrictReader_RejectsNullRequiredReferencesAndCollectionElementsOnDotNet8()
    {
        string nullText = WrapSkill(MinimalPassiveRecord().Replace(
            "\"displayName\": \"Passive Sample\"",
            "\"displayName\": null",
            StringComparison.Ordinal));
        string nullObject = WrapSkill(MinimalPassiveRecord().Replace(
            "\"inheritance\": { \"isInheritable\": true }",
            "\"inheritance\": null",
            StringComparison.Ordinal));
        const string nullRecord = """
            { "schemaVersion": 8, "skills": [null] }
            """;
        string nullEffect = WrapSkill(MinimalActiveRecord("[null]"));

        ContentDeserializationException textError = Assert.Throws<ContentDeserializationException>(
            () => _deserializer.DeserializeSkills(nullText, "null-text.json"));
        ContentDeserializationException objectError = Assert.Throws<ContentDeserializationException>(
            () => _deserializer.DeserializeSkills(nullObject, "null-object.json"));
        ContentDeserializationException recordError = Assert.Throws<ContentDeserializationException>(
            () => _deserializer.DeserializeSkills(nullRecord, "null-record.json"));
        ContentDeserializationException effectError = Assert.Throws<ContentDeserializationException>(
            () => _deserializer.DeserializeSkills(nullEffect, "null-effect.json"));

        Assert.Contains("displayName", textError.Message, StringComparison.Ordinal);
        Assert.Contains("inheritance", objectError.Message, StringComparison.Ordinal);
        Assert.Contains("skills", textError.JsonPath ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("skills", objectError.JsonPath ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal("$.skills[0]", recordError.JsonPath);
        Assert.Equal("$.skills[0].effects[0]", effectError.JsonPath);
    }

    [Fact]
    public void StrictReader_RejectsUnknownEnumValues()
    {
        string json = WrapSkill(MinimalPassiveRecord().Replace(
            "\"activation\": \"passive\"",
            "\"activation\": \"PASSIVE\"",
            StringComparison.Ordinal));

        Assert.Throws<ContentDeserializationException>(
            () => _deserializer.DeserializeSkills(json, "unknown-enum.json"));
    }

    [Fact]
    public void StrictReader_RejectsUnknownDependencyVocabularyAndMembers()
    {
        string unknownRequirement = WrapSkill(MinimalActiveRecord(
            """
            [
              {
                "type": "damage", "effectId": "primary_hit", "elementId": "physical",
                "power": 10, "accuracy": 100, "critical": { "mode": "never" },
                "hits": { "minimum": 1, "maximum": 1 }
              },
              {
                "type": "apply_ailment", "ailmentId": "poison", "chance": 50,
                "dependency": {
                  "sourceEffectId": "primary_hit", "requirement": "landed",
                  "scope": "same_target"
                }
              }
            ]
            """));
        string unknownMember = unknownRequirement.Replace(
            "\"requirement\": \"landed\"",
            "\"requirement\": \"positive_damage\", \"mystery\": true",
            StringComparison.Ordinal);

        Assert.Throws<ContentDeserializationException>(() =>
            _deserializer.DeserializeSkills(unknownRequirement, "unknown-dependency-requirement.json"));
        Assert.Throws<ContentDeserializationException>(() =>
            _deserializer.DeserializeSkills(unknownMember, "unknown-dependency-member.json"));
    }

    [Fact]
    public void DamageContactMode_MapsSharedContactAndRejectsUnknownVocabulary()
    {
        string json = WrapSkill(MinimalActiveRecord(
            """
            [
              {
                "type": "damage", "effectId": "primary_hit", "elementId": "physical",
                "power": 10, "accuracy": 100, "critical": { "mode": "never" },
                "hits": { "minimum": 1, "maximum": 1 }
              },
              {
                "type": "damage", "elementId": "fire", "power": 5, "accuracy": 25,
                "critical": { "mode": "never" }, "hits": { "minimum": 1, "maximum": 1 },
                "contactMode": "shared_contact",
                "dependency": {
                  "sourceEffectId": "primary_hit", "requirement": "positive_damage",
                  "scope": "same_target"
                }
              }
            ]
            """));

        SkillDefinition skill = Assert.Single(
            _deserializer.DeserializeSkills(json, "shared-contact.json").Records);
        DamageEffectDefinition primary = Assert.IsType<DamageEffectDefinition>(skill.Effects[0]);
        DamageEffectDefinition secondary = Assert.IsType<DamageEffectDefinition>(skill.Effects[1]);

        Assert.Equal(DamageContactMode.Independent, primary.ContactMode);
        Assert.Equal(DamageContactMode.SharedContact, secondary.ContactMode);
        Assert.Throws<ContentDeserializationException>(() =>
            _deserializer.DeserializeSkills(
                json.Replace("shared_contact", "reuse_accuracy", StringComparison.Ordinal),
                "unknown-contact-mode.json"));
    }

    [Fact]
    public void CustomParameters_AreImmutableClrValuesWithoutJsonElements()
    {
        string json = WrapSkill(MinimalActiveRecord(
            """
            [{
              "type": "custom", "handlerId": "parameter_test",
              "parameters": {
                "nothing": null, "enabled": true, "name": "sample", "count": 4,
                "ratio": 1.25, "items": [1, "two"], "nested": { "flag": false }
              }
            }]
            """));

        var custom = Assert.IsType<CustomEffectDefinition>(
            Assert.Single(Assert.Single(_deserializer.DeserializeSkills(json, "parameters.json").Records).Effects));

        AssertNoJsonValues(custom.Parameters);
        Assert.IsAssignableFrom<IReadOnlyList<object?>>(custom.Parameters["items"]);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(custom.Parameters["nested"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, object?>)custom.Parameters).Add("new", 1));
    }

    [Fact]
    public void DisplayText_DoesNotChangeBehavioralDefinition()
    {
        string first = WrapSkill(MinimalActiveRecord("[{ \"type\": \"analyze\", \"layers\": [\"stats\"] }]"));
        string second = first
            .Replace("Active Sample", "Completely Different", StringComparison.Ordinal)
            .Replace("Reference active.", "Unrelated prose.", StringComparison.Ordinal);

        SkillDefinition a = Assert.Single(_deserializer.DeserializeSkills(first, "first.json").Records);
        SkillDefinition b = Assert.Single(_deserializer.DeserializeSkills(second, "second.json").Records);

        Assert.Equal(a.Activation, b.Activation);
        Assert.Equal(a.MenuGroup, b.MenuGroup);
        Assert.Equal(a.InheritanceGroup, b.InheritanceGroup);
        Assert.Equal(a.Effects.Select(effect => effect.GetType()), b.Effects.Select(effect => effect.GetType()));
        Assert.NotEqual(a.DisplayName, b.DisplayName);
    }

    [Fact]
    public void SourceGeneratedContext_CoversEveryConcreteSchemaDto()
    {
        Type[] dtoTypes = typeof(SkillSystemJsonContext).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(SkillSystemJsonContext).Namespace)
            .Where(type => type.Name.EndsWith("Dto", StringComparison.Ordinal) && !type.IsAbstract)
            .ToArray();

        Assert.NotEmpty(dtoTypes);
        foreach (Type dtoType in dtoTypes)
        {
            Assert.NotNull(SkillSystemJsonContext.Default.GetTypeInfo(dtoType));
        }
    }

    [Fact]
    public void PublicBoundary_DoesNotExposeSerializerGodotOrLegacyTypes()
    {
        Type[] publicTypes = typeof(ISkillSystemDocumentDeserializer).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == typeof(ISkillSystemDocumentDeserializer).Namespace)
            .ToArray();

        foreach (Type type in publicTypes)
        {
            IEnumerable<Type> exposed = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))
                .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.PropertyType));

            Assert.DoesNotContain(exposed.SelectMany(Flatten), candidate =>
                candidate.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true ||
                candidate.Namespace?.StartsWith("Newtonsoft.Json", StringComparison.Ordinal) == true ||
                candidate.Namespace?.StartsWith("Godot", StringComparison.Ordinal) == true ||
                candidate.Name == "SkillData" ||
                candidate.Name == string.Concat("Per", "sona", "Data"));
        }
    }

    private static string WrapSkill(string record) => $$"""
    { "schemaVersion": 8, "skills": [ {{record}} ] }
    """;

    private static string MinimalPassiveRecord() =>
        """
        {
          "id": "passive_sample", "displayName": "Passive Sample", "description": "Reference passive.",
          "activation": "passive", "inheritanceGroupId": "passive",
          "inheritance": { "isInheritable": true },
          "modifiers": [{ "type": "accuracy", "operation": "add", "value": 1 }]
        }
        """;

    private static string MinimalActiveRecord(string effects, string extra = "") => $$"""
    {
      "id": "active_sample", "displayName": "Active Sample", "description": "Reference active.",
      "activation": "active", "menuGroup": "utility", "inheritanceGroupId": "utility",
      "inheritance": { "isInheritable": true },
      "targeting": { "relation": "enemy", "selection": "single", "lifeState": "alive", "allowSelf": false },
      "effects": {{effects}}{{extra}}
    }
    """;

    private static void AssertNoJsonValues(object? value)
    {
        Assert.False(value is JsonElement or JsonDocument);
        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            foreach (object? nested in dictionary.Values)
            {
                AssertNoJsonValues(nested);
            }
        }
        else if (value is IEnumerable enumerable and not string)
        {
            foreach (object? nested in enumerable)
            {
                AssertNoJsonValues(nested);
            }
        }
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Flatten(argument))
            {
                yield return nested;
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Convergence.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find Convergence.sln.");
    }
}
