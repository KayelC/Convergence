using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;

namespace Convergence.Framework.Tests.SkillSystem;

public sealed class ContentSchemaContractTests
{
    private const string SchemaPrefix = "urn:convergence:schema:content:v9:";

    [Fact]
    public void ActiveContentDocuments_ValidateAgainstTheirDeclaredDraft202012Schemas()
    {
        SchemaSet schemas = SchemaSet.Load();
        string[] contentFiles = Directory.GetFiles(ContentRoot(), "*.json", SearchOption.AllDirectories);

        Assert.Equal(36, contentFiles.Length);
        foreach (string contentFile in contentFiles)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(contentFile));
            string schemaId = document.RootElement.GetProperty("$schema").GetString()!;
            EvaluationResults result = schemas.Evaluate(schemaId, document.RootElement);

            Assert.True(result.IsValid, $"{Path.GetRelativePath(ContentRoot(), contentFile)} failed {schemaId}: {Describe(result)}");
        }
    }

    [Fact]
    public void ManifestDocumentTypes_MapToTheDeclaredFamilySchema_AndCoverEveryActiveDocument()
    {
        string root = ContentRoot();
        string[] allFiles = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
        HashSet<string> covered = new(StringComparer.OrdinalIgnoreCase);

        foreach (string manifestPath in allFiles.Where(path => Path.GetFileName(path).Contains(".manifest", StringComparison.Ordinal)))
        {
            covered.Add(Path.GetFullPath(manifestPath));
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            Assert.Equal(SchemaPrefix + "manifest", manifest.RootElement.GetProperty("$schema").GetString());

            foreach (JsonElement entry in manifest.RootElement.GetProperty("documents").EnumerateArray())
            {
                string documentType = entry.GetProperty("type").GetString()!;
                string documentPath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(manifestPath)!,
                    entry.GetProperty("path").GetString()!));
                Assert.True(File.Exists(documentPath), $"Manifest document is missing: {documentPath}");

                using JsonDocument content = JsonDocument.Parse(File.ReadAllText(documentPath));
                Assert.Equal(SchemaPrefix + documentType, content.RootElement.GetProperty("$schema").GetString());
                covered.Add(documentPath);
            }
        }

        Assert.Empty(allFiles.Select(Path.GetFullPath).Except(covered, StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(StructurallyInvalidDocuments))]
    public void StructuralInvalidCases_AreRejectedIndependently(string schemaFamily, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        EvaluationResults result = SchemaSet.Load().Evaluate(SchemaPrefix + schemaFamily, document.RootElement);

        Assert.False(result.IsValid);
    }

    [Theory]
    [MemberData(nameof(SharedUnionVariants))]
    public void SharedUnionVariants_AreIndependentlyValid(string definition, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        EvaluationResults result = SchemaSet.Load().EvaluateReference(
            SchemaPrefix + $"shared#/$defs/{definition}",
            document.RootElement);

        Assert.True(result.IsValid, $"{definition} rejected {json}: {Describe(result)}");
    }

    [Theory]
    [MemberData(nameof(InvalidSharedNumericVariants))]
    public void SharedNumericContracts_RejectEachInvalidLocalValueIndependently(string definition, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        EvaluationResults result = SchemaSet.Load().EvaluateReference(
            SchemaPrefix + $"shared#/$defs/{definition}",
            document.RootElement);

        Assert.False(result.IsValid, $"{definition} accepted invalid numeric contract {json}.");
    }

    [Theory]
    [MemberData(nameof(StatusLifetimeVariants))]
    public void StatusLifetimeVariants_AreIndependentlyValid(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        EvaluationResults result = SchemaSet.Load().EvaluateReference(
            SchemaPrefix + "shared#/$defs/statusLifetime",
            document.RootElement);

        Assert.True(result.IsValid, $"Status lifetime rejected {json}: {Describe(result)}");
    }

    [Theory]
    [MemberData(nameof(InvalidStatusLifetimeVariants))]
    public void StatusLifetimeContracts_RejectMalformedRemovalPolicies(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        EvaluationResults result = SchemaSet.Load().EvaluateReference(
            SchemaPrefix + "shared#/$defs/statusLifetime",
            document.RootElement);

        Assert.False(result.IsValid, $"Status lifetime accepted malformed contract {json}.");
    }

    [Theory]
    [InlineData("{\"type\":\"apply_ailment\",\"ailmentId\":\"poison\",\"chance\":50,\"duration\":{\"type\":\"battle\"}}")]
    [InlineData("{\"type\":\"grant_charge\",\"charge\":\"physical\",\"multiplier\":2,\"duration\":{\"type\":\"battle\"}}")]
    [InlineData("{\"type\":\"grant_shield\",\"shield\":\"magical\",\"duration\":{\"type\":\"battle\"}}")]
    [InlineData("{\"type\":\"break_affinity\",\"elementIds\":[\"fire\"],\"duration\":{\"type\":\"battle\"}}")]
    [InlineData("{\"type\":\"override_affinity\",\"elementIds\":[\"ice\"],\"affinityId\":\"repel\",\"duration\":{\"type\":\"battle\"}}")]
    public void StatusProducingEffects_RejectRetiredDurationShape(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        EvaluationResults result = SchemaSet.Load().EvaluateReference(
            SchemaPrefix + "shared#/$defs/effect",
            document.RootElement);

        Assert.False(result.IsValid, $"Effect accepted retired duration shape {json}.");
    }

    [Theory]
    [MemberData(nameof(FamilyUnionVariants))]
    public void FamilyUnionVariants_AreIndependentlyValid(string reference, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        EvaluationResults result = SchemaSet.Load().EvaluateReference(reference, document.RootElement);

        Assert.True(result.IsValid, $"{reference} rejected {json}: {Describe(result)}");
    }

    public static TheoryData<string, string> StructurallyInvalidDocuments()
    {
        JsonObject skill = LoadObject("demos", "clean-battle", "clean_battle_demo.skills.json");
        JsonObject item = LoadObject("original", "training-annex", "training_annex_slice.items.json");
        JsonObject equipment = LoadObject(
            "original",
            "training-annex",
            "training_annex_slice.equipment.json");

        JsonObject unknownField = skill.DeepClone().AsObject();
        unknownField["unexpected"] = true;

        JsonObject invalidEnum = skill.DeepClone().AsObject();
        invalidEnum["skills"]![0]!["activation"] = "automatic";

        JsonObject malformedId = skill.DeepClone().AsObject();
        malformedId["skills"]![0]!["id"] = "Bad ID";

        JsonObject wrongVersion = skill.DeepClone().AsObject();
        wrongVersion["schemaVersion"] = 2;

        JsonObject wrongSchema = skill.DeepClone().AsObject();
        wrongSchema["$schema"] = SchemaPrefix + "items";

        JsonObject malformedActive = skill.DeepClone().AsObject();
        malformedActive["skills"]![0]!.AsObject().Remove("targeting");

        JsonObject missingPassiveTriggerTargeting = skill.DeepClone().AsObject();
        JsonObject passiveSkill = missingPassiveTriggerTargeting["skills"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(record => record["activation"]!.GetValue<string>() == "passive");
        passiveSkill["triggers"]![0]!.AsObject().Remove("targeting");

        JsonObject invalidItemUsage = item.DeepClone().AsObject();
        invalidItemUsage["items"]![0]!["itemKind"] = "valuable";

        JsonObject missingBasicAttackCritical = equipment.DeepClone().AsObject();
        missingBasicAttackCritical["equipment"]![0]!["weapon"]!["basicAttack"]!
            .AsObject()
            .Remove("critical");

        JsonObject retiredEquipmentSlot = equipment.DeepClone().AsObject();
        JsonObject retiredEquipmentRecord = retiredEquipmentSlot["equipment"]![0]!.AsObject();
        retiredEquipmentRecord["slot"] = retiredEquipmentRecord["slotId"]!.DeepClone();
        retiredEquipmentRecord.Remove("slotId");

        JsonObject ambiguousCondition = skill.DeepClone().AsObject();
        JsonObject firstSkill = ambiguousCondition["skills"]![0]!.AsObject();
        firstSkill["effects"]![0]!["when"] = new JsonObject
        {
            ["all"] = new JsonArray(),
            ["any"] = new JsonArray()
        };

        JsonObject qualifiedEffectId = skill.DeepClone().AsObject();
        qualifiedEffectId["skills"]![0]!["effects"]![0]!["effectId"] = "pack:primary_hit";

        JsonObject incompleteDependency = skill.DeepClone().AsObject();
        incompleteDependency["skills"]![0]!["effects"]![0]!["dependency"] = new JsonObject
        {
            ["sourceEffectId"] = "primary_hit",
            ["requirement"] = "positive_damage"
        };

        return new TheoryData<string, string>
        {
            { "skills", unknownField.ToJsonString() },
            { "skills", invalidEnum.ToJsonString() },
            { "skills", malformedId.ToJsonString() },
            { "skills", wrongVersion.ToJsonString() },
            { "skills", wrongSchema.ToJsonString() },
            { "skills", malformedActive.ToJsonString() },
            { "skills", missingPassiveTriggerTargeting.ToJsonString() },
            { "items", invalidItemUsage.ToJsonString() },
            { "equipment", missingBasicAttackCritical.ToJsonString() },
            { "equipment", retiredEquipmentSlot.ToJsonString() },
            { "skills", ambiguousCondition.ToJsonString() },
            { "skills", qualifiedEffectId.ToJsonString() },
            { "skills", incompleteDependency.ToJsonString() }
        };
    }

    public static TheoryData<string, string> SharedUnionVariants()
    {
        TheoryData<string, string> variants = new();

        Add(variants, "amount",
            """{"type":"flat","value":5}""",
            """{"type":"percent_max","value":25}""",
            """{"type":"percent_current","value":10}""",
            """{"type":"full"}""",
            """{"type":"power","power":40}""",
            """{"type":"formula","formulaId":"sample_formula","parameters":{"scale":2}}""");
        Add(variants, "duration",
            """{"type":"instant"}""",
            """{"type":"battle"}""",
            """{"type":"permanent"}""",
            """{"type":"turns","value":2,"tick":"owner_turn_end","suspendWhileReserve":true}""",
            """{"type":"phase","phaseId":"round_end"}""");
        Add(variants, "critical",
            """{"mode":"never"}""",
            """{"mode":"chance","chance":20}""");
        Add(variants, "passiveTriggerTargeting",
            """{"scope":"owner","lifeState":"any","includeReserveActors":true}""",
            """{"scope":"event_targets","lifeState":"alive","includeReserveActors":false}""",
            """{"scope":"owner_team","lifeState":"alive","includeReserveActors":false}""",
            """{"scope":"opposing_teams","lifeState":"dead","includeReserveActors":true}""",
            """{"scope":"all_participants","lifeState":"any","includeReserveActors":true}""");
        Add(variants, "instantDeathResistanceCheck",
            """{"mode":"none"}""",
            """{"mode":"channel","channelId":"light"}""");
        Add(variants, "condition",
            """{"all":[]}""",
            """{"any":[]}""",
            """{"not":{"type":"chance","chance":50}}""",
            """{"type":"actor_resource_percentage","resourceId":"hp","comparison":"less_than","value":50}""",
            """{"type":"target_resource_percentage","resourceId":"sp","comparison":"greater_than","value":10}""",
            """{"type":"actor_has_ailment","ailmentIds":["poison"]}""",
            """{"type":"target_has_ailment","ailmentIds":["poison"]}""",
            """{"type":"actor_has_skill","skillId":"sample_skill"}""",
            """{"type":"target_has_skill","skillId":"sample_skill"}""",
            """{"type":"actor_has_buff","modifierTrackId":"attack"}""",
            """{"type":"target_has_buff","modifierTrackId":"attack"}""",
            """{"type":"actor_has_affinity","elementId":"ice","affinityId":"weak"}""",
            """{"type":"target_has_affinity","elementId":"fire","affinityId":"resist"}""",
            """{"type":"actor_has_capability","capabilityId":"flight"}""",
            """{"type":"target_has_capability","capabilityId":"flight"}""",
            """{"type":"actor_life_state","lifeState":"alive"}""",
            """{"type":"target_life_state","lifeState":"dead"}""",
            """{"type":"battle_kind","allowed":["standard"]}""",
            """{"type":"moon_phase","allowed":["full"]}""",
            """{"type":"party_size","comparison":"equal","value":0}""",
            """{"type":"chance","chance":50}""",
            """{"type":"effect_element_is","elementId":"wind"}""",
            """{"type":"custom","handlerId":"sample_condition","parameters":{"enabled":true}}""");
        Add(variants, "effect",
            """{"type":"damage","effectId":"primary_hit","elementId":"physical","power":20,"accuracy":95,"critical":{"mode":"chance","chance":10},"hits":{"minimum":1,"maximum":1}}""",
            """{"type":"damage","elementId":"physical","power":1,"accuracy":100,"critical":{"mode":"never"},"hits":{"minimum":1024,"maximum":1024}}""",
            """{"type":"damage","elementId":"fire","power":10,"accuracy":50,"critical":{"mode":"never"},"hits":{"minimum":1,"maximum":1},"contactMode":"shared_contact","dependency":{"sourceEffectId":"primary_hit","requirement":"positive_damage","scope":"same_target"}}""",
            """{"type":"instant_kill","chance":25,"resistanceCheck":{"mode":"channel","channelId":"dark"}}""",
            """{"type":"apply_ailment","ailmentId":"poison","chance":50,"lifetime":{"expiration":{"type":"battle"},"allowedRemovalCauses":["duration_expired","battle_end"]}}""",
            """{"type":"restore_resource","resourceId":"hp","amount":{"type":"flat","value":20}}""",
            """{"type":"revive","resourceId":"hp","amount":{"type":"percent_max","value":25}}""",
            """{"type":"reduce_resource","resourceId":"sp","amount":{"type":"flat","value":5},"canReduceToZero":true}""",
            """{"type":"set_resource","resourceId":"hp","amount":{"type":"full"}}""",
            """{"type":"remove_ailment","scope":"selected","ailmentIds":["poison"]}""",
            """{"type":"modify_stat_stage","modifierTrackIds":["attack"],"stageDelta":1,"duration":{"type":"turns","value":3,"tick":"owner_turn_end","suspendWhileReserve":true}}""",
            """{"type":"grant_charge","charge":"physical","multiplier":2.0,"lifetime":{"expiration":{"type":"battle"},"allowedRemovalCauses":["duration_expired","consumed"]}}""",
            """{"type":"grant_charge","charge":"general","multiplier":2.0,"lifetime":{"expiration":{"type":"battle"},"allowedRemovalCauses":["duration_expired","consumed"]}}""",
            """{"type":"grant_shield","shield":"magical","lifetime":{"expiration":{"type":"turns","value":1,"tick":"owner_turn_end","suspendWhileReserve":false},"allowedRemovalCauses":["duration_expired","deployment_swap"]}}""",
            """{"type":"break_affinity","elementIds":["fire"],"lifetime":{"expiration":{"type":"battle"},"allowedRemovalCauses":["duration_expired","battle_end"]}}""",
            """{"type":"override_affinity","elementIds":["ice"],"affinityId":"repel","lifetime":{"expiration":{"type":"battle"},"allowedRemovalCauses":["duration_expired","battle_end"]}}""",
            """{"type":"remove_status_effect","statusKinds":["buff"],"statusIds":["attack"]}""",
            """{"type":"analyze","layers":["full"]}""",
            """{"type":"escape","eligibilityRuleId":"standard_escape","chance":100}""",
            """{"type":"custom","handlerId":"request_host_action","parameters":{"requestId":"sample"}}""");
        variants.Add(
            "effect",
            """{"type":"apply_ailment","ailmentId":"poison","chance":50,"dependency":{"sourceEffectId":"primary_hit","requirement":"positive_damage","scope":"same_target"}}""");

        foreach (string numericModifier in new[]
        {
            "damage_dealt", "damage_taken", "accuracy", "evasion", "critical_chance",
            "ailment_infliction", "healing_received", "healing_given", "resource_cost",
            "maximum_resource", "experience_gain"
        })
        {
            variants.Add("ruleModifier", $"{{\"type\":\"{numericModifier}\",\"operation\":\"multiply\",\"value\":1.1}}");
        }

        Add(variants, "ruleModifier",
            """{"type":"elemental_affinity","elementId":"ice","affinityId":"null"}""",
            """{"type":"ailment_resistance","ailmentId":"poison","resistance":"immune"}""",
            """{"type":"basic_attack","elementId":"physical","drain":"none"}""");

        return variants;
    }

    public static TheoryData<string, string> InvalidSharedNumericVariants() => new()
    {
        { "amount", """{"type":"flat","value":-0.1}""" },
        { "amount", """{"type":"percent_max","value":-1}""" },
        { "amount", """{"type":"percent_current","value":-1}""" },
        { "amount", """{"type":"power","power":-1}""" },
        { "duration", """{"type":"turns","value":0,"tick":"owner_turn_end","suspendWhileReserve":false}""" },
        { "critical", """{"mode":"chance","chance":101}""" },
        { "condition", """{"type":"chance","chance":-1}""" },
        { "condition", """{"type":"actor_resource_percentage","resourceId":"hp","comparison":"less_than","value":-1}""" },
        { "condition", """{"type":"target_resource_percentage","resourceId":"hp","comparison":"greater_than","value":101}""" },
        { "condition", """{"type":"party_size","comparison":"equal","value":-1}""" },
        { "effect", """{"type":"damage","elementId":"physical","power":-1,"accuracy":100,"critical":{"mode":"never"},"hits":{"minimum":1,"maximum":1}}""" },
        { "effect", """{"type":"damage","elementId":"physical","power":1,"accuracy":101,"critical":{"mode":"never"},"hits":{"minimum":1,"maximum":1}}""" },
        { "effect", """{"type":"damage","elementId":"physical","power":1,"accuracy":100,"critical":{"mode":"never"},"hits":{"minimum":1,"maximum":1025,"distribution":"uniform"}}""" },
        { "effect", """{"type":"instant_kill","chance":-1,"resistanceCheck":{"mode":"none"}}""" },
        { "effect", """{"type":"apply_ailment","ailmentId":"poison","chance":101}""" },
        { "effect", """{"type":"modify_stat_stage","modifierTrackIds":["attack"],"stageDelta":0}""" },
        { "effect", """{"type":"grant_charge","charge":"physical","multiplier":0}""" },
        { "effect", """{"type":"escape","eligibilityRuleId":"standard_escape","chance":101}""" }
    };

    public static TheoryData<string> StatusLifetimeVariants() => new()
    {
        """{"expiration":{"type":"instant"},"allowedRemovalCauses":["duration_expired"]}""",
        """{"expiration":{"type":"battle"},"allowedRemovalCauses":["duration_expired","battle_end"]}""",
        """{"expiration":{"type":"permanent"},"allowedRemovalCauses":[]}""",
        """{"expiration":{"type":"permanent"},"allowedRemovalCauses":["scripted_removal"]}""",
        """{"expiration":{"type":"turns","value":2,"tick":"owner_turn_end","suspendWhileReserve":true},"allowedRemovalCauses":["cure_effect","duration_expired"]}""",
        """{"expiration":{"type":"phase","phaseId":"round_end"},"allowedRemovalCauses":["duration_expired","dispel_effect"]}"""
    };

    public static TheoryData<string> InvalidStatusLifetimeVariants() => new()
    {
        """{"expiration":{"type":"battle"},"allowedRemovalCauses":["battle_end"]}""",
        """{"expiration":{"type":"turns","value":2,"tick":"owner_turn_end","suspendWhileReserve":true},"allowedRemovalCauses":["cure_effect"]}""",
        """{"expiration":{"type":"phase","phaseId":"round_end"},"allowedRemovalCauses":[]}""",
        """{"expiration":{"type":"instant"},"allowedRemovalCauses":["duration_expired","duration_expired"]}""",
        """{"expiration":{"type":"permanent"},"allowedRemovalCauses":["unknown_cause"]}""",
        """{"allowedRemovalCauses":["scripted_removal"]}""",
        """{"expiration":{"type":"permanent"}}""",
        """{"expiration":{"type":"permanent"},"allowedRemovalCauses":[],"unexpected":true}"""
    };

    public static TheoryData<string, string> FamilyUnionVariants()
    {
        TheoryData<string, string> variants = new();
        string sharedTargeting = """{"relation":"enemy","selection":"single","lifeState":"alive","allowSelf":false}""";
        string damage = """{"type":"damage","elementId":"physical","power":10,"accuracy":100,"critical":{"mode":"never"},"hits":{"minimum":1,"maximum":1}}""";

        Add(variants, SchemaPrefix + "ailments#/$defs/turnBehavior",
            """{"type":"normal"}""",
            """{"type":"skip"}""",
            """{"type":"forced_basic_attack"}""",
            """{"type":"confused_action"}""",
            """{"type":"limited_actions","allowedActionIds":["attack"]}""",
            """{"type":"chance_skip","skipChance":50}""",
            """{"type":"chance_skip_or_flee","skipChance":40,"fleeChance":15,"companionFleeOutcome":"recall_to_roster"}""",
            """{"type":"custom","handlerId":"sample_turn_behavior"}""");
        Add(variants, SchemaPrefix + "shops#/$defs/price",
            """{"kind":"fixed","basePrice":100}""",
            """{"kind":"policy","pricingPolicyId":"standard_price","parameters":{"factor":1}}""");
        Add(variants, SchemaPrefix + "shops#/$defs/stock",
            """{"kind":"unlimited"}""",
            """{"kind":"limited","quantity":3}""",
            """{"kind":"policy","stockPolicyId":"progress_stock"}""");
        Add(variants, SchemaPrefix + "fusion#/$defs/result",
            """{"operation":"create_entity","resultEntityId":"result_entity"}""",
            """{"operation":"catalyst_rank_shift","rankShift":1}""",
            """{"operation":"stat_boost","policyId":"sample_boost"}""",
            """{"operation":"special","policyId":"sample_special"}""");
        Add(variants, SchemaPrefix + "items#/$defs/item",
            $"{{\"id\":\"consumable\",\"displayName\":\"Consumable\",\"description\":\"\",\"itemKind\":\"consumable\",\"stackLimit\":9,\"baseValue\":1,\"usage\":{{\"contexts\":[\"battle\"],\"consumeOn\":\"successful_execution\",\"targeting\":{sharedTargeting},\"effects\":[{damage}]}}}}",
            """{"id":"key_item","displayName":"Key","description":"","itemKind":"key","stackLimit":1,"baseValue":0}""",
            """{"id":"material","displayName":"Material","description":"","itemKind":"material","stackLimit":99,"baseValue":5}""",
            """{"id":"valuable","displayName":"Valuable","description":"","itemKind":"valuable","stackLimit":1,"baseValue":50}""");
        Add(variants, SchemaPrefix + "skills#/$defs/skill",
            $"{{\"id\":\"active_skill\",\"displayName\":\"Active\",\"description\":\"\",\"activation\":\"active\",\"menuGroup\":\"offense\",\"inheritanceGroupId\":\"physical\",\"inheritance\":{{\"isInheritable\":true}},\"targeting\":{sharedTargeting},\"effects\":[{damage}],\"availability\":{{\"contexts\":[\"battle\"]}}}}",
            """{"id":"passive_skill","displayName":"Passive","description":"","activation":"passive","inheritanceGroupId":"passive","inheritance":{"isInheritable":true},"triggers":[{"event":"owner_turn_end","targeting":{"scope":"owner","lifeState":"any","includeReserveActors":true},"effects":[{"type":"restore_resource","resourceId":"hp","amount":{"type":"flat","value":1}}]}]}""");
        Add(variants, SchemaPrefix + "equipment#/$defs/equipmentRecord",
            """{"id":"weapon","displayName":"Weapon","description":"","slotId":"weapon","baseValue":1,"weapon":{"basicAttack":{"element":"physical","power":10,"accuracy":100,"critical":{"mode":"chance","chance":10},"isLongRange":false}}}""",
            """{"id":"composite_weapon","displayName":"Composite Weapon","description":"","slotId":"weapon","baseValue":1,"weapon":{"basicAttack":{"element":"physical","power":10,"accuracy":100,"critical":{"mode":"never"},"isLongRange":false,"primaryEffectId":"weapon_contact","secondaryEffects":[{"type":"damage","elementId":"fire","power":5,"accuracy":25,"critical":{"mode":"never"},"hits":{"minimum":1,"maximum":1},"contactMode":"shared_contact","dependency":{"sourceEffectId":"weapon_contact","requirement":"positive_damage","scope":"same_target"}}]}}}""",
            """{"id":"armor","displayName":"Armor","description":"","slotId":"armor","baseValue":1,"armor":{"defense":2,"evasion":0}}""",
            """{"id":"boots","displayName":"Boots","description":"","slotId":"boots","baseValue":1,"boots":{"evasion":2}}""",
            """{"id":"accessory","displayName":"Accessory","description":"","slotId":"accessory","baseValue":1,"accessory":{"statModifiers":[{"statId":"luck","value":1}]}}""");

        return variants;
    }

    private static void Add(TheoryData<string, string> data, string discriminator, params string[] examples)
    {
        foreach (string example in examples)
        {
            data.Add(discriminator, example);
        }
    }

    private static JsonObject LoadObject(params string[] segments) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine([ContentRoot(), .. segments])))!.AsObject();

    private static string ContentRoot() => Path.Combine(AppContext.BaseDirectory, "Content");

    private static string SchemaRoot() => Path.Combine(AppContext.BaseDirectory, "Schemas", "content", "v9");

    private static string Describe(EvaluationResults result)
    {
        result.ToList();
        return string.Join(", ", (result.Details ?? []).Select(detail =>
            $"{detail.InstanceLocation}: {detail.EvaluationPath}"));
    }

    private sealed class SchemaSet
    {
        private readonly Dictionary<string, JsonSchema> _schemas;
        private readonly BuildOptions _buildOptions;

        private SchemaSet(Dictionary<string, JsonSchema> schemas, BuildOptions buildOptions)
        {
            _schemas = schemas;
            _buildOptions = buildOptions;
        }

        public static SchemaSet Load()
        {
            SchemaRegistry registry = new();
            BuildOptions buildOptions = new() { SchemaRegistry = registry };
            Dictionary<string, JsonSchema> schemas = new(StringComparer.Ordinal);

            foreach (string path in Directory.GetFiles(SchemaRoot(), "*.schema.json").Order(StringComparer.Ordinal))
            {
                string text = File.ReadAllText(path);
                using JsonDocument document = JsonDocument.Parse(text);
                string id = document.RootElement.GetProperty("$id").GetString()!;
                JsonSchema schema = JsonSchema.FromText(text, buildOptions, new Uri(path));
                registry.Register(schema);
                schemas.Add(id, schema);
            }

            return new SchemaSet(schemas, buildOptions);
        }

        public EvaluationResults Evaluate(string schemaId, JsonElement instance)
        {
            Assert.True(_schemas.TryGetValue(schemaId, out JsonSchema? schema), $"Unknown schema ID: {schemaId}");
            return schema.Evaluate(instance, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });
        }

        public EvaluationResults EvaluateReference(string reference, JsonElement instance)
        {
            string wrapper = $$"""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "$ref": "{{reference}}"
                }
                """;
            JsonSchema schema = JsonSchema.FromText(wrapper, _buildOptions);
            return schema.Evaluate(instance, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });
        }
    }
}
