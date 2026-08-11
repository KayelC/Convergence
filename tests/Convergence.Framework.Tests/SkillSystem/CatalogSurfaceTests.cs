using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Convergence.Fusion;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Content;

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
        Assert.Equal(10, catalog.Rulesets.Count);

        EquipmentDefinition weapon = catalog.GetRequiredEquipment(
            Id("convergence.catalog_surface_sample:shortsword_sample"));
        ShopCatalogDefinition shop = catalog.GetRequiredShop(
            Id("convergence.catalog_surface_sample:sample_outfitter"));
        NegotiationDefinition negotiation = catalog.GetRequiredNegotiation(
            Id("convergence.catalog_surface_sample:playful_sample"));
        EncounterDefinition encounter = catalog.GetRequiredEncounter(
            Id("convergence.catalog_surface_sample:entry_block_training_sample"));
        DungeonDefinition dungeon = catalog.GetRequiredDungeon(
            Id("convergence.catalog_surface_sample:sample_depths"));
        FusionRecipeDefinition fusion = catalog.GetRequiredFusionRecipe(
            Id("convergence.catalog_surface_sample:demo_spirit_binary_sample"));
        RulesetDefinition ruleset = catalog.GetRequiredRuleset(
            Id("convergence.catalog_surface_sample:standard_damage_sample"));

        Assert.Equal(DamageElement.Physical, weapon.Weapon!.BasicAttack.Element);
        Assert.Equal(Id("weapon_shop"), shop.CategoryId);
        Assert.Equal(Id("field"), Assert.Single(shop.AvailabilityContextIds));
        Assert.Equal(Id("convergence.catalog_surface_sample:shortsword_sample"), shop.Offers[0].ContentId);
        Assert.Equal(Id("convergence.shared_effects_demo:medicine_demo"), shop.Offers[1].ContentId);
        Assert.Equal(Id("playful"), negotiation.PersonalityId);
        Assert.Equal(Id("convergence.clean_battle_demo:demo_spirit"), Assert.Single(negotiation.DefaultRaceIds));
        Assert.Equal(Id("convergence.clean_battle_demo:ember_duelist_demo"),
            Assert.Single(Assert.Single(encounter.Formations).Members).EntityId);
        Assert.Equal(Id("convergence.catalog_surface_sample:entry_block_training_sample"),
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
            9,
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
            9,
                    [
                        new EquipmentDefinition(
                            Id("bad_weapon"),
                            "Bad Weapon",
                            "Bad ranges.",
                            StandardEquipmentSlotIds.Weapon,
                            -1,
                            weapon: new EquipmentWeaponProfileDefinition(
                                new EquipmentBasicAttackDefinition(DamageElement.Physical, -1, 101, new NeverCriticalDefinition(), false)))
                    ]))
            ],
            shopDocuments:
            [
                Source("shops.json", "shops.json", new DeserializedContentDocument<ShopCatalogDefinition>(
            9,
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
            9,
                    [new EncounterDefinition(Id("empty_encounter"), "Empty", "No formations.")]))
            ],
            dungeonDocuments:
            [
                Source("dungeons.json", "dungeons.json", new DeserializedContentDocument<DungeonDefinition>(
            9,
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
            9,
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
            9,
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
            error.JsonPath == "$.fusionRecipes[0].parents" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid &&
            error.Message.Contains("exactly two", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.fusionRecipes[0].result.resultEntityId" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.rulesets[0].policyId" &&
            error.Code == ContentValidationErrorCode.RegistrationMissing);
        Assert.Null(result.ValidatedContent);
    }

    [Fact]
    public void EquipmentBasicAttackSecondaryEffectsValidateAsOneOrderedSequence()
    {
        EffectLocalId primaryId = EffectLocalId.Parse("weapon_contact");
        EquipmentDefinition Equipment(bool exposePrimary) => new(
            Id(exposePrimary ? "valid_weapon" : "invalid_weapon"),
            "Weapon",
            "Exercises composed basic attacks.",
            StandardEquipmentSlotIds.Weapon,
            1,
            weapon: new EquipmentWeaponProfileDefinition(new EquipmentBasicAttackDefinition(
                DamageElement.Physical,
                10,
                100,
                new NeverCriticalDefinition(),
                false)
            {
                PrimaryEffectId = exposePrimary ? primaryId : null,
                SecondaryEffects =
                [
                    new DamageEffectDefinition(
                        DamageElement.Fire,
                        5,
                        20,
                        new NeverCriticalDefinition(),
                        new HitCountDefinition(1, 1))
                    {
                        ContactMode = DamageContactMode.SharedContact,
                        Dependency = new EffectDependencyDefinition(
                            primaryId,
                            EffectDependencyRequirement.PositiveDamage,
                            EffectDependencyScope.SameTarget)
                    }
                ]
            }));
        var manifest = new ContentPackManifest(
            9,
            "test.pack",
            SemanticVersion.Parse("1.0.0"),
            "Test Pack",
            null,
            null,
            [new ContentPackDocumentReference("equipment", "equipment.json")]);
        SkillSystemRegistrationSnapshot registrations = new SkillSystemRegistrationBuilder()
            .SupportEffect<DamageEffectDefinition>()
            .Build();

        ContentValidationResult valid = new SkillSystemContentValidator().Validate(
            new SkillSystemValidationRequest(
                manifest,
                "manifest.json",
                registrations,
                equipmentDocuments:
                [
                    Source(
                        "equipment.json",
                        "equipment.json",
                        new DeserializedContentDocument<EquipmentDefinition>(9, [Equipment(true)]))
                ]));
        ContentValidationResult invalid = new SkillSystemContentValidator().Validate(
            new SkillSystemValidationRequest(
                manifest,
                "manifest.json",
                registrations,
                equipmentDocuments:
                [
                    Source(
                        "equipment.json",
                        "equipment.json",
                        new DeserializedContentDocument<EquipmentDefinition>(9, [Equipment(false)]))
                ]));

        Assert.True(valid.IsValid, string.Join(Environment.NewLine, valid.Errors.Select(error => error.Message)));
        Assert.Contains(invalid.Errors, error =>
            error.JsonPath == "$.equipment[0].weapon.basicAttack.secondaryEffects[0].dependency.sourceEffectId" &&
            error.Code == ContentValidationErrorCode.EffectDependencySourceMissing);
    }

    [Fact]
    public void CatalogSurfaceValidation_RejectsUnsafeNegotiationAggregates()
    {
        ContentPackManifest manifest = new(
            9,
            "test.pack",
            SemanticVersion.Parse("1.0.0"),
            "Test Pack",
            null,
            null,
            [new ContentPackDocumentReference("negotiations", "negotiations.json")]);
        NegotiationDefinition negotiation = new(
            Id("unsafe_negotiation"),
            "Unsafe Negotiation",
            "Exercises aggregate numeric validation.",
            Id("test_personality"),
            questions:
            [
                new NegotiationQuestionDefinition("Maximum?", [new NegotiationAnswerDefinition("Yes", int.MaxValue)]),
                new NegotiationQuestionDefinition("Positive overflow?", [new NegotiationAnswerDefinition("Yes", 1)]),
                new NegotiationQuestionDefinition("Minimum?", [new NegotiationAnswerDefinition("No", int.MinValue)]),
                new NegotiationQuestionDefinition("Negative overflow?", [new NegotiationAnswerDefinition("No", -1)])
            ],
            demands:
            [
                new NegotiationDemandDefinition(Id("first_demand"), int.MaxValue),
                new NegotiationDemandDefinition(Id("second_demand"), 1)
            ]);

        ContentValidationResult result = new SkillSystemContentValidator().Validate(
            new SkillSystemValidationRequest(
                manifest,
                "manifest.json",
                new SkillSystemRegistrationBuilder()
                    .RegisterNegotiationPersonality("test_personality")
                    .RegisterNegotiationDemand("first_demand", "second_demand")
                    .Build(),
                negotiationDocuments:
                [
                    Source(
                        "negotiations.json",
                        "negotiations.json",
                        new DeserializedContentDocument<NegotiationDefinition>(9, [negotiation]))
                ]));

        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.negotiations[0].questions" &&
            error.Code == ContentValidationErrorCode.ValueOutOfRange);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.negotiations[0].demands" &&
            error.Code == ContentValidationErrorCode.ValueOutOfRange);
        Assert.Null(result.ValidatedContent);
    }

    [Fact]
    public void CatalogSurfaceValidation_AcceptsExactNegotiationNumericBoundaries()
    {
        ContentPackManifest manifest = new(
            9,
            "test.pack",
            SemanticVersion.Parse("1.0.0"),
            "Test Pack",
            null,
            null,
            [new ContentPackDocumentReference("negotiations", "negotiations.json")]);
        NegotiationDefinition negotiation = new(
            Id("boundary_negotiation"),
            "Boundary Negotiation",
            "Uses the complete supported numeric domain.",
            Id("test_personality"),
            questions:
            [
                new NegotiationQuestionDefinition("Maximum?", [new NegotiationAnswerDefinition("Yes", int.MaxValue)]),
                new NegotiationQuestionDefinition("Minimum?", [new NegotiationAnswerDefinition("No", int.MinValue)])
            ],
            demands:
            [
                new NegotiationDemandDefinition(Id("maximum_demand"), int.MaxValue)
            ]);

        ContentValidationResult result = new SkillSystemContentValidator().Validate(
            new SkillSystemValidationRequest(
                manifest,
                "manifest.json",
                new SkillSystemRegistrationBuilder()
                    .RegisterNegotiationPersonality("test_personality")
                    .RegisterNegotiationDemand("maximum_demand")
                    .Build(),
                negotiationDocuments:
                [
                    Source(
                        "negotiations.json",
                        "negotiations.json",
                        new DeserializedContentDocument<NegotiationDefinition>(9, [negotiation]))
                ]));

        Assert.True(result.IsValid, string.Join(Environment.NewLine,
            result.Errors.Select(error => $"{error.Code} {error.JsonPath}: {error.Message}")));
        Assert.NotNull(result.ValidatedContent);
    }

    [Fact]
    public void CatalogSurfaceValidation_RequiresExactlyTwoFusionParents()
    {
        ContentPackManifest manifest = new(
            9,
            "test.pack",
            SemanticVersion.Parse("1.0.0"),
            "Test Pack",
            null,
            null,
            [new ContentPackDocumentReference("fusion", "fusion.json")]);
        FusionRecipeDefinition oneParent = RecipeWithParents(
            "one_parent",
            [new FusionParentSelectorDefinition(FusionParentSelectorKind.Race, Id("race_a"))]);
        FusionRecipeDefinition threeParents = RecipeWithParents(
            "three_parents",
            [
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Race, Id("race_a")),
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Race, Id("race_b")),
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Entity, Id("entity_c"))
            ]);

        ContentValidationResult result = new SkillSystemContentValidator().Validate(
            new SkillSystemValidationRequest(
                manifest,
                "manifest.json",
                new SkillSystemRegistrationBuilder().Build(),
                fusionDocuments:
                [
                    Source(
                        "fusion.json",
                        "fusion.json",
                        new DeserializedContentDocument<FusionRecipeDefinition>(9, [oneParent, threeParents]))
                ]));

        ContentValidationError[] cardinalityErrors = result.Errors
            .Where(error =>
                error.Code == ContentValidationErrorCode.ShapeInvalid &&
                error.JsonPath.EndsWith(".parents", StringComparison.Ordinal) &&
                error.Message.Contains("exactly two", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            ["$.fusionRecipes[0].parents", "$.fusionRecipes[1].parents"],
            cardinalityErrors.Select(error => error.JsonPath).ToArray());
    }

    [Fact]
    public void CatalogSurfaceValidation_RequiresOneCatalystAndOneRankShiftTarget()
    {
        ContentPackManifest manifest = new(
            9,
            "test.pack",
            SemanticVersion.Parse("1.0.0"),
            "Test Pack",
            null,
            null,
            [new ContentPackDocumentReference("fusion", "fusion.json")]);
        var recipe = new FusionRecipeDefinition(
            Id("bad_roles"),
            "Bad Roles",
            "Both selectors incorrectly use the normal participant role.",
            [
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Race, Id("race_a")),
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Entity, Id("catalyst"))
            ],
            new FusionResultDefinition(
                FusionResultOperationKind.CatalystRankShift,
                rankShift: 1));

        ContentValidationResult result = new SkillSystemContentValidator().Validate(
            new SkillSystemValidationRequest(
                manifest,
                "manifest.json",
                new SkillSystemRegistrationBuilder().Build(),
                fusionDocuments:
                [
                    Source(
                        "fusion.json",
                        "fusion.json",
                        new DeserializedContentDocument<FusionRecipeDefinition>(9, [recipe]))
                ]));

        Assert.Contains(result.Errors, error =>
            error.Code == ContentValidationErrorCode.ShapeInvalid &&
            error.JsonPath == "$.fusionRecipes[0].parents" &&
            error.Message.Contains("one catalyst", StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogSurfaceValidation_RejectsOverlappingEqualSpecificityFusionRecipes()
    {
        ContentPackManifest manifest = new(
            9,
            "test.pack",
            SemanticVersion.Parse("1.0.0"),
            "Test Pack",
            null,
            null,
            [
                new ContentPackDocumentReference("entities", "entities.json"),
                new ContentPackDocumentReference("races", "races.json"),
                new ContentPackDocumentReference("fusion", "fusion.json")
            ]);
        EntityDefinition parentA = FusionEntity("parent_a", "race_a");
        EntityDefinition parentB = FusionEntity("parent_b", "race_b");
        EntityDefinition resultEntity = FusionEntity("result", "race_a");
        FusionRecipeDefinition first = RecipeWithParents(
            "first_overlap",
            [
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Entity, parentA.Id),
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Race, parentB.RaceId)
            ]);
        FusionRecipeDefinition second = RecipeWithParents(
            "second_overlap",
            [
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Entity, parentB.Id),
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Race, parentA.RaceId)
            ]);

        ContentValidationResult result = new SkillSystemContentValidator().Validate(
            new SkillSystemValidationRequest(
                manifest,
                "manifest.json",
                new SkillSystemRegistrationBuilder().RegisterEntityKind("companion").Build(),
                entityDocuments:
                [
                    Source(
                        "entities.json",
                        "entities.json",
                        new DeserializedContentDocument<EntityDefinition>(9, [parentA, parentB, resultEntity]))
                ],
                raceDocuments:
                [
                    Source(
                        "races.json",
                        "races.json",
                        new DeserializedContentDocument<RaceDefinition>(
            9,
                            [new RaceDefinition(Id("race_a"), "Race A"), new RaceDefinition(Id("race_b"), "Race B")]))
                ],
                fusionDocuments:
                [
                    Source(
                        "fusion.json",
                        "fusion.json",
                        new DeserializedContentDocument<FusionRecipeDefinition>(9, [first, second]))
                ]));

        ContentValidationError ambiguity = Assert.Single(result.Errors);
        Assert.Equal(ContentValidationErrorCode.FusionRecipeAmbiguous, ambiguity.Code);
        Assert.Equal("$.fusionRecipes[1].parents", ambiguity.JsonPath);
        Assert.Equal(second.Id, ambiguity.RecordId);
        Assert.Contains(first.Id.ToString(), ambiguity.Message, StringComparison.Ordinal);
        Assert.Contains("no recipe-priority field", ambiguity.Suggestion, StringComparison.Ordinal);
        Assert.Null(result.ValidatedContent);
    }

    [Fact]
    public void CatalogFusionRepository_RejectsUnvalidatedNonBinaryRecipesInsteadOfOmittingThem()
    {
        FusionRecipeDefinition malformed = RecipeWithParents(
            "test.pack:three_parents",
            [
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Race, Id("test.pack:race_a")),
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Race, Id("test.pack:race_b")),
                new FusionParentSelectorDefinition(FusionParentSelectorKind.Entity, Id("test.pack:entity_c"))
            ]);
        var catalog = new GameDataCatalog(
            contentPacks: [],
            skills: [],
            entities: [],
            races: [],
            ailments: [],
            items: [],
            fusionRecipes:
            [
                new KeyValuePair<ContentId, FusionRecipeDefinition>(malformed.Id, malformed)
            ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new CatalogFusionContentRepository(catalog));

        Assert.Contains("exactly two", exception.Message, StringComparison.Ordinal);
        Assert.Contains(malformed.Id.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogSurfaceDeserialization_RejectsUnknownFieldsEnumsAndUnionShapes()
    {
        Assert.Throws<ContentDeserializationException>(() => _deserializer.DeserializeEquipment(
            """
            {
              "schemaVersion": 9,
              "equipment": [{
                "id": "bad", "displayName": "Bad", "description": "Bad.",
                "slotId": "weapon", "baseValue": 1,
                "weapon": { "basicAttack": { "element": "physical", "power": 1, "accuracy": 100, "isLongRange": false } },
                "unexpected": true
              }]
            }
            """,
            "bad.equipment.json"));

        EquipmentDefinition customSlot = Assert.Single(_deserializer.DeserializeEquipment(
            """
            {
              "schemaVersion": 9,
              "equipment": [{
                "id": "bad", "displayName": "Bad", "description": "Bad.",
                "slotId": "wand", "baseValue": 1,
                "weapon": { "basicAttack": { "element": "physical", "power": 1, "accuracy": 100, "critical": { "mode": "never" }, "isLongRange": false } }
              }]
            }
            """,
            "custom-slot.equipment.json").Records);

        Assert.Equal(Id("wand"), customSlot.SlotId);

        ContentDeserializationException priceShape = Assert.Throws<ContentDeserializationException>(() =>
            _deserializer.DeserializeShops(
                """
                {
                  "schemaVersion": 9,
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

        Assert.Throws<ContentDeserializationException>(() => _deserializer.DeserializeFusionRecipes(
            """
            {
              "schemaVersion": 9,
              "fusionRecipes": [{
                "id": "old_rank", "displayName": "Old", "description": "Old shape.",
                "parents": [
                  { "kind": "race", "id": "race_a", "role": "rank_shift_target" },
                  { "kind": "entity", "id": "catalyst", "role": "catalyst" }
                ],
                "result": { "operation": "rank_offset", "rankOffset": 1 }
              }]
            }
            """,
            "old-rank.fusion.json"));

        Assert.Throws<ContentDeserializationException>(() => _deserializer.DeserializeFusionRecipes(
            """
            {
              "schemaVersion": 9,
              "fusionRecipes": [{
                "id": "missing_role", "displayName": "Missing", "description": "Missing role.",
                "parents": [
                  { "kind": "race", "id": "race_a" },
                  { "kind": "entity", "id": "catalyst", "role": "catalyst" }
                ],
                "result": { "operation": "catalyst_rank_shift", "rankShift": 1 }
              }]
            }
            """,
            "missing-role.fusion.json"));
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
        string jsonRoot = Path.Combine(AppContext.BaseDirectory, "Content");
        return new ContentPackTextBundle(
            manifestName,
            File.ReadAllText(TestContentPath.Resolve(jsonRoot, manifestName)),
            documentNames.Select(name => new ContentDocumentText(
                name,
                name,
                File.ReadAllText(TestContentPath.Resolve(jsonRoot, name)))));
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
            .RegisterEntityKind("companion")
            .RegisterEvent("owner_turn_end")
            .RegisterAilmentGroup("poison")
            .RegisterEscapeRule("standard_escape")
            .RegisterCustomEffect("request_dungeon_exit", EmptyParameterValidator.Instance)
            .RegisterShopCategory("weapon_shop")
            .RegisterNegotiationPersonality("playful")
            .RegisterNegotiationDemand("credits")
            .RegisterEncounterEnvironment("entry_block")
            .RegisterPolicy(
                "standard_damage",
                "standard_reward",
                "standard_growth",
                "standard_stat",
                "persistent_staged",
                "timed_exclusive",
                "timed_contribution",
                "standard_action_token",
                "standard_roster_capacity",
                "standard_economy",
                "standard_shop_pricing",
                "luck_adjusted_shop_pricing",
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

    private static FusionRecipeDefinition RecipeWithParents(
        string id,
        IEnumerable<FusionParentSelectorDefinition> parents) =>
        new(
            Id(id),
            id,
            "Cardinality test recipe.",
            parents,
            new FusionResultDefinition(
                FusionResultOperationKind.CreateEntity,
                resultEntityId: Id("test.pack:result")));

    private static EntityDefinition FusionEntity(string id, string raceId) =>
        new(
            Id(id),
            id,
            "Fusion ambiguity test entity.",
            Id("companion"),
            Id(raceId),
            rank: 1,
            baseLevel: 1,
            new EntityCapabilitiesDefinition(true, true, true),
            new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList)),
            []);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Convergence.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find Convergence.sln.");
    }

    private sealed class EmptyParameterValidator : IContentParameterValidator
    {
        public static EmptyParameterValidator Instance { get; } = new();

        public IReadOnlyList<ContentParameterValidationIssue> Validate(
            IReadOnlyDictionary<string, object?> parameters) =>
            [];
    }
}
