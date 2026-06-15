using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using Xunit;

namespace Convergence.Tests.SkillSystem;

public sealed class CatalogSurfaceTests
{
    private readonly SkillSystemJsonDeserializer _deserializer = new();
    private readonly SkillSystemCatalogLoader _loader = new();

    [Fact]
    public void CatalogSurfaceSample_LoadsAllNewFamiliesWithQualifiedReferences()
    {
        GameDataCatalog catalog = LoadCatalogSurfaceSample();

        Assert.Equal(4, catalog.Equipment.Count);
        Assert.Single(catalog.Shops);
        Assert.Single(catalog.Negotiations);
        Assert.Single(catalog.Encounters);
        Assert.Single(catalog.Dungeons);
        Assert.Single(catalog.FusionRecipes);
        Assert.Equal(8, catalog.Rulesets.Count);

        EquipmentDefinition weapon = catalog.GetRequiredEquipment(
            Id("convergence.catalog_surface_sample:shortsword_sample"));
        ShopCatalogDefinition shop = catalog.GetRequiredShop(
            Id("convergence.catalog_surface_sample:paulownia_blacksmith_sample"));
        NegotiationDefinition negotiation = catalog.GetRequiredNegotiation(
            Id("convergence.catalog_surface_sample:childlike_sample"));
        EncounterDefinition encounter = catalog.GetRequiredEncounter(
            Id("convergence.catalog_surface_sample:thebel_training_sample"));
        DungeonDefinition dungeon = catalog.GetRequiredDungeon(
            Id("convergence.catalog_surface_sample:tartarus_sample"));
        FusionRecipeDefinition fusion = catalog.GetRequiredFusionRecipe(
            Id("convergence.catalog_surface_sample:demo_spirit_binary_sample"));
        RulesetDefinition ruleset = catalog.GetRequiredRuleset(
            Id("convergence.catalog_surface_sample:standard_damage_sample"));

        Assert.Equal(DamageElement.Physical, weapon.Weapon!.BasicAttack.Element);
        Assert.Equal(Id("weapon_shop"), shop.CategoryId);
        Assert.Equal(Id("field"), Assert.Single(shop.AvailabilityContextIds));
        Assert.Equal(Id("convergence.catalog_surface_sample:shortsword_sample"), shop.Offers[0].ContentId);
        Assert.Equal(Id("convergence.shared_effects_demo:medicine_demo"), shop.Offers[1].ContentId);
        Assert.Equal(Id("childlike"), negotiation.PersonalityId);
        Assert.Equal(Id("convergence.clean_battle_demo:demo_spirit"), Assert.Single(negotiation.DefaultRaceIds));
        Assert.Equal(Id("convergence.clean_battle_demo:ember_duelist_demo"),
            Assert.Single(Assert.Single(encounter.Formations).Members).EntityId);
        Assert.Equal(Id("convergence.catalog_surface_sample:thebel_training_sample"),
            Assert.Single(Assert.Single(dungeon.Blocks).EncounterPoolIds));
        Assert.Equal(Id("convergence.clean_battle_demo:frost_duelist_demo"),
            fusion.Result.ResultEntityId);
        Assert.Equal(Id("standard_damage"), ruleset.PolicyId);
        Assert.Throws<ArgumentException>(() => catalog.GetRequiredEquipment(Id("shortsword_sample")));
    }

    [Fact]
    public void CatalogSurfaceValidation_RejectsBadRangesMissingRegistrationsAndShapes()
    {
        ContentPackManifest manifest = new(
            1,
            "test.pack",
            SemanticVersion.Parse("1.0.0"),
            "Test Pack",
            null,
            null,
            [
                new ContentPackDocumentReference("equipment", "equipment.json"),
                new ContentPackDocumentReference("shops", "shops.json"),
                new ContentPackDocumentReference("encounters", "encounters.json"),
                new ContentPackDocumentReference("dungeons", "dungeons.json"),
                new ContentPackDocumentReference("fusion", "fusion.json"),
                new ContentPackDocumentReference("rulesets", "rulesets.json")
            ]);

        var validator = new SkillSystemContentValidator();
        ContentValidationResult result = validator.Validate(new SkillSystemValidationRequest(
            manifest,
            "manifest.json",
            new SkillSystemRegistrationBuilder().Build(),
            equipmentDocuments:
            [
                Source("equipment.json", "equipment.json", new DeserializedContentDocument<EquipmentDefinition>(
                    1,
                    [
                        new EquipmentDefinition(
                            Id("bad_weapon"),
                            "Bad Weapon",
                            "Bad ranges.",
                            EquipmentSlot.Weapon,
                            -1,
                            weapon: new EquipmentWeaponProfileDefinition(
                                new EquipmentBasicAttackDefinition(DamageElement.Physical, -1, 101, false)))
                    ]))
            ],
            shopDocuments:
            [
                Source("shops.json", "shops.json", new DeserializedContentDocument<ShopCatalogDefinition>(
                    1,
                    [
                        new ShopCatalogDefinition(
                            Id("bad_shop"),
                            "Bad Shop",
                            "Missing registrations.",
                            Id("weapon_shop"),
                            [Id("field")],
                            [])
                    ]))
            ],
            encounterDocuments:
            [
                Source("encounters.json", "encounters.json", new DeserializedContentDocument<EncounterDefinition>(
                    1,
                    [new EncounterDefinition(Id("empty_encounter"), "Empty", "No formations.")]))
            ],
            dungeonDocuments:
            [
                Source("dungeons.json", "dungeons.json", new DeserializedContentDocument<DungeonDefinition>(
                    1,
                    [
                        new DungeonDefinition(
                            Id("bad_dungeon"),
                            "Bad Dungeon",
                            "Bad floor range.",
                            [new DungeonBlockDefinition(Id("bad_block"), "Bad Block", 10, 2)])
                    ]))
            ],
            fusionDocuments:
            [
                Source("fusion.json", "fusion.json", new DeserializedContentDocument<FusionRecipeDefinition>(
                    1,
                    [
                        new FusionRecipeDefinition(
                            Id("bad_fusion"),
                            "Bad Fusion",
                            "Missing result.",
                            [new FusionParentSelectorDefinition(FusionParentSelectorKind.Race, Id("race_a"))],
                            new FusionResultDefinition(FusionResultOperationKind.CreateEntity))
                    ]))
            ],
            rulesetDocuments:
            [
                Source("rulesets.json", "rulesets.json", new DeserializedContentDocument<RulesetDefinition>(
                    1,
                    [
                        new RulesetDefinition(
                            Id("bad_ruleset"),
                            "Bad Ruleset",
                            "Missing policy registration.",
                            RulesetCategory.Damage,
                            Id("standard_damage"))
                    ]))
            ]));

        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.equipment[0].baseValue" &&
            error.Code == ContentValidationErrorCode.ValueMustBeNonNegative);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.equipment[0].weapon.basicAttack.accuracy" &&
            error.Code == ContentValidationErrorCode.ValueOutOfRange);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.shops[0].categoryId" &&
            error.Code == ContentValidationErrorCode.RegistrationMissing);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.encounters[0].formations" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.dungeons[0].blocks[0]" &&
            error.Code == ContentValidationErrorCode.MinimumExceedsMaximum);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.fusionRecipes[0].result.resultEntityId" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.rulesets[0].policyId" &&
            error.Code == ContentValidationErrorCode.RegistrationMissing);
        Assert.Null(result.ValidatedContent);
    }

    [Fact]
    public void CatalogSurfaceDeserialization_RejectsUnknownFieldsEnumsAndUnionShapes()
    {
        Assert.Throws<ContentDeserializationException>(() => _deserializer.DeserializeEquipment(
            """
            {
              "schemaVersion": 1,
              "equipment": [{
                "id": "bad", "displayName": "Bad", "description": "Bad.",
                "slot": "weapon", "baseValue": 1,
                "weapon": { "basicAttack": { "element": "physical", "power": 1, "accuracy": 100, "isLongRange": false } },
                "unexpected": true
              }]
            }
            """,
            "bad.equipment.json"));

        Assert.Throws<ContentDeserializationException>(() => _deserializer.DeserializeEquipment(
            """
            {
              "schemaVersion": 1,
              "equipment": [{
                "id": "bad", "displayName": "Bad", "description": "Bad.",
                "slot": "wand", "baseValue": 1,
                "weapon": { "basicAttack": { "element": "physical", "power": 1, "accuracy": 100, "isLongRange": false } }
              }]
            }
            """,
            "bad.equipment.json"));

        ContentDeserializationException priceShape = Assert.Throws<ContentDeserializationException>(() =>
            _deserializer.DeserializeShops(
                """
                {
                  "schemaVersion": 1,
                  "shops": [{
                    "id": "bad_shop", "displayName": "Bad", "description": "Bad.",
                    "categoryId": "weapon_shop", "availabilityContexts": ["field"],
                    "offers": [{
                      "contentKind": "equipment", "contentId": "shortsword",
                      "price": { "kind": "fixed" },
                      "stock": { "kind": "unlimited" }
                    }]
                  }]
                }
                """,
                "bad.shops.json"));

        Assert.Equal("$.shops[0].offers[0].price", priceShape.JsonPath);
    }

    private GameDataCatalog LoadCatalogSurfaceSample()
    {
        ContentPackTextBundle reference = Bundle(
            "skill_system_redesign.manifest.sample.json",
            "skill_system_redesign.entities.sample.json",
            "skill_system_redesign.skills.sample.json",
            "skill_system_redesign.races.sample.json");
        ContentPackTextBundle battle = Bundle(
            "clean_battle_demo.manifest.json",
            "clean_battle_demo.races.json",
            "clean_battle_demo.skills.json",
            "clean_battle_demo.entities.json");
        ContentPackTextBundle shared = Bundle(
            "shared_effects_demo.manifest.json",
            "shared_effects_demo.ailments.json",
            "shared_effects_demo.skills.json",
            "shared_effects_demo.entities.json",
            "shared_effects_demo.items.json");
        ContentPackTextBundle surface = Bundle(
            "catalog_surface_sample.manifest.json",
            "catalog_surface_sample.equipment.json",
            "catalog_surface_sample.shops.json",
            "catalog_surface_sample.negotiations.json",
            "catalog_surface_sample.encounters.json",
            "catalog_surface_sample.dungeons.json",
            "catalog_surface_sample.fusion.json",
            "catalog_surface_sample.rulesets.json");

        CatalogLoadResult result = _loader.Load(new SkillSystemCatalogLoadRequest(
            CatalogSurfaceRegistrations(),
            [reference, battle, shared, surface]));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine,
            result.Diagnostics.Select(error => $"{error.Code} {error.JsonPath}: {error.Message}")));
        return result.RequireCatalog();
    }

    private static ContentPackTextBundle Bundle(string manifestName, params string[] documentNames)
    {
        string jsonRoot = Path.Combine(FindRepositoryRoot(), "Data", "Jsons");
        return new ContentPackTextBundle(
            manifestName,
            File.ReadAllText(Path.Combine(jsonRoot, manifestName)),
            documentNames.Select(name => new ContentDocumentText(
                name,
                name,
                File.ReadAllText(Path.Combine(jsonRoot, name)))));
    }

    private static SourceContentDocument<TDefinition> Source<TDefinition>(
        string manifestPath,
        string sourceName,
        DeserializedContentDocument<TDefinition> document) =>
        new(manifestPath, sourceName, document);

    private static SkillSystemRegistrationSnapshot CatalogSurfaceRegistrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEntityKind("demon")
            .RegisterEvent("owner_turn_end")
            .RegisterAilmentGroup("poison")
            .RegisterEscapeRule("standard_escape")
            .RegisterCustomEffect("request_dungeon_exit", EmptyParameterValidator.Instance)
            .RegisterShopCategory("weapon_shop")
            .RegisterNegotiationPersonality("childlike")
            .RegisterNegotiationDemand("macca")
            .RegisterEncounterEnvironment("thebel")
            .RegisterPolicy(
                "standard_damage",
                "standard_reward",
                "standard_growth",
                "standard_stat",
                "standard_press_turn",
                "standard_stock_capacity",
                "standard_economy",
                "standard_moon_phase",
                "return_to_lobby",
                "standard_accident",
                "standard_mutation")
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportEffect<RemoveAilmentEffectDefinition>()
            .SupportEffect<ReviveEffectDefinition>()
            .SupportEffect<EscapeEffectDefinition>()
            .SupportEffect<CustomEffectDefinition>()
            .SupportModifier<NumericRuleModifierDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .Build();

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JRPG.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find JRPG.sln.");
    }

    private sealed class EmptyParameterValidator : IContentParameterValidator
    {
        public static EmptyParameterValidator Instance { get; } = new();

        public IReadOnlyList<ContentParameterValidationIssue> Validate(
            IReadOnlyDictionary<string, object?> parameters) =>
            [];
    }
}
