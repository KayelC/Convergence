using System.Reflection;
using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class ContentValidationTests
{
    private readonly SkillSystemJsonDeserializer _deserializer = new();
    private readonly SkillSystemContentValidator _validator = new();

    [Fact]
    public void ReferencePack_ValidatesWithExplicitRegistrations()
    {
        string jsonRoot = Path.Combine(AppContext.BaseDirectory, "Content");
        ContentPackManifest manifest = ReadManifest(jsonRoot, "skill_system_redesign.manifest.sample.json");
        var registrations = new SkillSystemRegistrationBuilder()
            .RegisterEntityKind("companion")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .SupportModifier<NumericRuleModifierDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .Build();
        var request = new SkillSystemValidationRequest(
            manifest,
            "skill_system_redesign.manifest.sample.json",
            registrations,
            skillDocuments:
            [
                SkillDocument(jsonRoot, "skill_system_redesign.skills.sample.json")
            ],
            entityDocuments:
            [
                EntityDocument(jsonRoot, "skill_system_redesign.entities.sample.json")
            ],
            raceDocuments:
            [
                RaceDocument(jsonRoot, "skill_system_redesign.races.sample.json")
            ]);

        ContentValidationResult result = _validator.Validate(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Same(result.ValidatedContent, result.RequireValidContent());
        Assert.Single(result.RequireValidContent().SkillDocuments);
    }

    [Fact]
    public void InvalidReferencePack_AggregatesOrderedSourceAwareDiagnostics()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Validation");
        ContentPackManifest manifest = ReadManifest(root, "invalid.manifest.json");
        SkillSystemValidationRequest request = new(
            manifest,
            "invalid.manifest.json",
            InvalidFixtureRegistrations(),
            [SkillDocument(root, "invalid.skills.json")],
            [EntityDocument(root, "invalid.entities.json")],
            [RaceDocument(root, "invalid.races.json")],
            [AilmentDocument(root, "invalid.ailments.json")]);

        ContentValidationResult first = _validator.Validate(request);
        ContentValidationResult second = _validator.Validate(request);

        Assert.False(first.IsValid);
        Assert.Null(first.ValidatedContent);
        Assert.True(first.Errors.Count >= 35);
        Assert.Equal(first.Errors, second.Errors);
        Assert.Contains(first.Errors, error =>
            error.SourceName == "invalid.skills.json" &&
            error.RecordId == ContentId.Parse("unstable_blast") &&
            error.JsonPath == "$.skills[0].effects[0].accuracy" &&
            error.Code == ContentValidationErrorCode.ValueOutOfRange);
        Assert.Contains(first.Errors, error =>
            error.SourceName == "invalid.entities.json" &&
            error.RecordId == ContentId.Parse("invalid_entity") &&
            error.JsonPath == "$.entities[0].inheritanceRules.allowedSkillIds[0]" &&
            error.Code == ContentValidationErrorCode.InheritanceListConflict);
        Assert.Contains(first.Errors, error =>
            error.SourceName == "invalid.ailments.json" &&
            error.JsonPath == "$.ailments[0].turnBehavior" &&
            error.Code == ContentValidationErrorCode.ValueOutOfRange);
        Assert.Contains(first.Errors, error =>
            error.SourceName == "invalid.races.json" &&
            error.JsonPath == "$.races[0].id" &&
            error.Code == ContentValidationErrorCode.RecordIdMustBeLocal);

        ContentValidationException exception = Assert.Throws<ContentValidationException>(first.RequireValidContent);
        Assert.Equal(first.Errors, exception.Errors);
    }

    [Fact]
    public void LocalAndSamePackReferencesResolveWhileExternalPackReferencesAreDeferred()
    {
        SkillDefinition skill = PassiveSkill("local_skill");
        RaceDefinition race = new(Id("local_race"), "Local Race");
        EntityDefinition entity = Entity(
            "local_entity",
            Id("test.pack:local_race"),
            baseSkillIds: [Id("test.pack:local_skill"), Id("other.pack:external_skill")]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [skill],
            entities: [entity],
            races: [race]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ProgrammaticDefaultIdentifiersProduceTypedDiagnosticsWithoutCascadingAsMissing()
    {
        SkillDefinition invalidRecord = InvalidIdentifierSkill();
        SkillDefinition invalidMutation = new(
            Id("invalid_mutation"), "Invalid Mutation", "Invalid mutation family.",
            SkillActivation.Passive, null, InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            mutation: new SkillMutationDefinition(default, 1),
            modifiers:
            [
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.Accuracy,
                    ModifierOperation.Add,
                    1)
            ]);
        EntityDefinition invalidReference = Entity(
            "invalid_reference",
            default,
            new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList),
                blockedSkillIds: [default],
                allowedSkillIds: [default]),
            baseSkillIds: [default, default]);
        RaceDefinition invalidRegistration = new(
            Id("invalid_registration"),
            "Invalid Registration",
            alignmentIds: [default]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [invalidRecord, invalidMutation],
            entities: [invalidReference],
            races: [invalidRegistration]));

        Assert.Contains(result.Errors, error =>
            error.Code == ContentValidationErrorCode.RecordIdInvalid &&
            error.JsonPath == "$.skills[0].id" &&
            error.RecordId is ContentId recordId && recordId.IsEmpty);
        Assert.Contains(result.Errors, error =>
            error.Code == ContentValidationErrorCode.ReferenceIdInvalid &&
            error.JsonPath == "$.skills[1].mutation.familyId");
        Assert.Contains(result.Errors, error =>
            error.Code == ContentValidationErrorCode.ReferenceIdInvalid &&
            error.JsonPath == "$.entities[0].raceId");
        Assert.Contains(result.Errors, error =>
            error.Code == ContentValidationErrorCode.ReferenceIdInvalid &&
            error.JsonPath == "$.entities[0].baseSkillIds[0]");
        Assert.Contains(result.Errors, error =>
            error.Code == ContentValidationErrorCode.RegistrationIdInvalid &&
            error.JsonPath == "$.races[0].alignmentIds[0]");
        Assert.DoesNotContain(result.Errors, error =>
            error.JsonPath is "$.entities[0].raceId" or "$.entities[0].baseSkillIds[0]" &&
            error.Code == ContentValidationErrorCode.ReferenceMissing);
        Assert.DoesNotContain(result.Errors, error =>
            error.Code is ContentValidationErrorCode.InheritanceListConflict or
                ContentValidationErrorCode.EntitySkillAssignmentDuplicate or
                ContentValidationErrorCode.ListDuplicateValue);
    }

    [Fact]
    public void CatalogLoader_ContainsProgrammaticDefaultRecordIdInsideValidationDiagnostics()
    {
        const string manifestJson = """
            {
              "schemaVersion": 6,
              "id": "test.pack",
              "version": "1.0.0",
              "displayName": "Test Pack",
              "documents": [
                { "type": "skills", "path": "skills.json" }
              ]
            }
            """;
        var loader = new SkillSystemCatalogLoader(
            new ProgrammaticDefaultIdDeserializer(InvalidIdentifierSkill()),
            new SkillSystemContentValidator());
        var registrations = new SkillSystemRegistrationBuilder()
            .SupportModifier<NumericRuleModifierDefinition>()
            .Build();

        CatalogLoadResult result = loader.Load(new SkillSystemCatalogLoadRequest(
            registrations,
            [
                new ContentPackTextBundle(
                    "manifest.json",
                    manifestJson,
                    [new ContentDocumentText("skills.json", "skills.json", "{}")])
            ]));

        Assert.False(result.IsSuccess);
        CatalogLoadDiagnostic diagnostic = Assert.Single(result.Diagnostics, item =>
            item.Code == CatalogLoadDiagnosticCode.ContentValidationFailed);
        Assert.Equal(ContentValidationErrorCode.RecordIdInvalid, diagnostic.ValidationCode);
        Assert.Equal("$.skills[0].id", diagnostic.JsonPath);
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void SamePackAliasesShareConflictAndDuplicateIdentity()
    {
        SkillDefinition skill = PassiveSkill("local_skill");
        EntityDefinition entity = Entity(
            "local_entity",
            Id("race"),
            inheritanceRules: new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList),
                blockedSkillIds: [Id("local_skill")],
                allowedSkillIds: [Id("test.pack:local_skill")]),
            baseSkillIds: [Id("local_skill"), Id("test.pack:local_skill")]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [skill],
            entities: [entity],
            races: [new RaceDefinition(Id("race"), "Race")]));

        Assert.Contains(result.Errors, error => error.Code == ContentValidationErrorCode.InheritanceListConflict);
        Assert.Contains(result.Errors, error => error.Code == ContentValidationErrorCode.EntitySkillAssignmentDuplicate);
    }

    [Fact]
    public void DuplicateTargetsProduceDuplicateAndAmbiguousReferenceErrors()
    {
        SkillDefinition first = PassiveSkill("duplicate_skill");
        SkillDefinition second = PassiveSkill("duplicate_skill");
        EntityDefinition entity = Entity("entity", Id("race"), baseSkillIds: [Id("duplicate_skill")]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [first, second],
            entities: [entity],
            races: [new RaceDefinition(Id("race"), "Race")]));

        Assert.Equal(2, result.Errors.Count(error => error.Code == ContentValidationErrorCode.RecordDuplicateId));
        Assert.Contains(result.Errors, error =>
            error.Code == ContentValidationErrorCode.ReferenceAmbiguous &&
            error.JsonPath == "$.entities[0].baseSkillIds[0]");
    }

    [Fact]
    public void ActivationShapesAreValidatedAfterDeserialization()
    {
        SkillDefinition active = new(
            Id("invalid_active"), "Invalid Active", "Invalid active shape.",
            SkillActivation.Active, null, InheritanceGroup.Utility, new SkillInheritanceDefinition(true),
            modifiers: [new NumericRuleModifierDefinition(NumericRuleModifierType.Accuracy, ModifierOperation.Add, 1)]);
        SkillDefinition passive = new(
            Id("invalid_passive"), "Invalid Passive", "Invalid passive shape.",
            SkillActivation.Passive, SkillMenuGroup.Utility, InheritanceGroup.Ice,
            new SkillInheritanceDefinition(true),
            costs: [new SkillCostDefinition(Id("sp"), new FlatAmountDefinition(1))],
            targeting: SingleEnemyTarget(),
            effects: [new AnalyzeEffectDefinition([AnalysisLayer.Stats])],
            availability: new SkillAvailabilityDefinition([Id("battle")]));

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(), skills: [active, passive]));

        AssertCodes(result,
            ContentValidationErrorCode.SkillActiveMenuGroupRequired,
            ContentValidationErrorCode.SkillActiveEffectsRequired,
            ContentValidationErrorCode.SkillActiveAvailabilityRequired,
            ContentValidationErrorCode.SkillActivePassiveMembersForbidden,
            ContentValidationErrorCode.SkillPassiveMenuGroupForbidden,
            ContentValidationErrorCode.SkillPassiveAvailabilityForbidden,
            ContentValidationErrorCode.SkillPassiveActiveMembersForbidden,
            ContentValidationErrorCode.SkillPassiveInheritanceGroupRequired,
            ContentValidationErrorCode.SkillPassiveBehaviorRequired);
    }

    [Fact]
    public void NumericContractsAndTargetShapesUseContractOnlyRanges()
    {
        SkillDefinition rangeSkill = ActiveSkill(
            "range_skill",
            [
                new DamageEffectDefinition(
                    DamageElement.Fire, -1, 101, new ChanceCriticalDefinition(-1),
                    new HitCountDefinition(2, 1, HitDistribution.Fixed)),
                new ApplyAilmentEffectDefinition(
                    Id("poison"), 101, new TurnDurationDefinition(0, Id("owner_turn_end"), false)),
                new ModifyStatStageEffectDefinition([], 0),
                new GrantChargeEffectDefinition(ChargeKind.Magical, 0)
            ],
            costs: [new SkillCostDefinition(Id("sp"), new FlatAmountDefinition(-1))],
            targeting: new TargetingDefinition(
                TargetRelation.Enemy, TargetSelection.Random, TargetLifeState.Alive, false));
        AilmentDefinition poison = Ailment("poison");

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(), skills: [rangeSkill], ailments: [poison]));

        Assert.Contains(result.Errors, error => error.JsonPath == "$.skills[0].costs[0].amount.value" &&
            error.Code == ContentValidationErrorCode.ValueMustBeNonNegative);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.skills[0].targeting.count" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.skills[0].effects[0].power" &&
            error.Code == ContentValidationErrorCode.ValueMustBeNonNegative);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.skills[0].effects[0].hits" &&
            error.Code == ContentValidationErrorCode.MinimumExceedsMaximum);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.skills[0].effects[1].duration.value" &&
            error.Code == ContentValidationErrorCode.ValueMustBePositive);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.skills[0].effects[2].stageDelta" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.skills[0].effects[3].multiplier" &&
            error.Code == ContentValidationErrorCode.ValueMustBePositive);
    }

    [Fact]
    public void DamageHitCountsCannotExceedThePublishedContentCeiling()
    {
        SkillDefinition skill = ActiveSkill(
            "excessive_hit_count",
            [
                new DamageEffectDefinition(
                    DamageElement.Physical,
                    1,
                    100,
                    new NeverCriticalDefinition(),
                    new HitCountDefinition(1, 1025, HitDistribution.Uniform))
            ]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [skill]));

        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.skills[0].effects[0].hits.maximum" &&
            error.Code == ContentValidationErrorCode.ValueOutOfRange);
    }

    [Fact]
    public void EffectDependenciesRequireUniqueEarlierCompatibleSources()
    {
        EffectLocalId duplicateId = EffectLocalId.Parse("duplicate");
        EffectLocalId futureId = EffectLocalId.Parse("future_damage");
        EffectLocalId restoreId = EffectLocalId.Parse("restore_source");
        SkillDefinition skill = ActiveSkill(
            "invalid_dependencies",
            [
                new AnalyzeEffectDefinition([AnalysisLayer.Stats]) { EffectId = duplicateId },
                new AnalyzeEffectDefinition([AnalysisLayer.Affinities]) { EffectId = duplicateId },
                new ApplyAilmentEffectDefinition(Id("poison"), 50)
                {
                    Dependency = new EffectDependencyDefinition(
                        EffectLocalId.Parse("missing_source"),
                        EffectDependencyRequirement.Succeeded,
                        EffectDependencyScope.SameTarget)
                },
                new ApplyAilmentEffectDefinition(Id("poison"), 50)
                {
                    Dependency = new EffectDependencyDefinition(
                        futureId,
                        EffectDependencyRequirement.Succeeded,
                        EffectDependencyScope.SameTarget)
                },
                new DamageEffectDefinition(
                    DamageElement.Physical, 10, 100, new NeverCriticalDefinition(),
                    new HitCountDefinition(1, 1))
                {
                    EffectId = futureId
                },
                new RestoreResourceEffectDefinition(Id("hp"), new FlatAmountDefinition(10))
                {
                    EffectId = restoreId
                },
                new ApplyAilmentEffectDefinition(Id("poison"), 50)
                {
                    Dependency = new EffectDependencyDefinition(
                        restoreId,
                        EffectDependencyRequirement.PositiveDamage,
                        EffectDependencyScope.SameTarget)
                }
            ]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [skill],
            ailments: [Ailment("poison")]));

        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.skills[0].effects[1].effectId" &&
            error.Code == ContentValidationErrorCode.EffectIdDuplicate);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.skills[0].effects[2].dependency.sourceEffectId" &&
            error.Code == ContentValidationErrorCode.EffectDependencySourceMissing);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.skills[0].effects[3].dependency.sourceEffectId" &&
            error.Code == ContentValidationErrorCode.EffectDependencyOrderInvalid);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.skills[0].effects[6].dependency.requirement" &&
            error.Code == ContentValidationErrorCode.EffectDependencySourceIncompatible);
    }

    [Fact]
    public void PositiveDamageDependencyAcceptsAnEarlierDamageSource()
    {
        EffectLocalId sourceId = EffectLocalId.Parse("needle_hit");
        SkillDefinition skill = ActiveSkill(
            "valid_dependency",
            [
                new DamageEffectDefinition(
                    DamageElement.Physical, 50, 100, new NeverCriticalDefinition(),
                    new HitCountDefinition(1, 1))
                {
                    EffectId = sourceId
                },
                new ApplyAilmentEffectDefinition(Id("poison"), 50)
                {
                    Dependency = new EffectDependencyDefinition(
                        sourceId,
                        EffectDependencyRequirement.PositiveDamage,
                        EffectDependencyScope.SameTarget)
                }
            ]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [skill],
            ailments: [Ailment("poison")]));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
    }

    [Fact]
    public void SharedContactRequiresSameTargetPositiveDamageDependency()
    {
        EffectLocalId sourceId = EffectLocalId.Parse("primary_hit");
        DamageEffectDefinition Source() => new(
            DamageElement.Physical, 10, 100, new NeverCriticalDefinition(),
            new HitCountDefinition(1, 1))
        {
            EffectId = sourceId
        };
        DamageEffectDefinition Shared(EffectDependencyDefinition? dependency = null) => new(
            DamageElement.Fire, 10, 100, new NeverCriticalDefinition(),
            new HitCountDefinition(1, 1))
        {
            ContactMode = DamageContactMode.SharedContact,
            Dependency = dependency
        };

        SkillDefinition missing = ActiveSkill("shared_missing_dependency", [Source(), Shared()]);
        SkillDefinition wrongRequirement = ActiveSkill(
            "shared_wrong_requirement",
            [Source(), Shared(new EffectDependencyDefinition(
                sourceId,
                EffectDependencyRequirement.Succeeded,
                EffectDependencyScope.SameTarget))]);
        SkillDefinition wrongScope = ActiveSkill(
            "shared_wrong_scope",
            [Source(), Shared(new EffectDependencyDefinition(
                sourceId,
                EffectDependencyRequirement.PositiveDamage,
                EffectDependencyScope.AnyTarget))]);
        SkillDefinition valid = ActiveSkill(
            "shared_valid",
            [Source(), Shared(new EffectDependencyDefinition(
                sourceId,
                EffectDependencyRequirement.PositiveDamage,
                EffectDependencyScope.SameTarget))]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [missing, wrongRequirement, wrongScope, valid]));

        Assert.Equal(3, result.Errors.Count(error =>
            error.Code == ContentValidationErrorCode.SharedContactDependencyInvalid));
        Assert.DoesNotContain(result.Errors, error =>
            error.RecordId == valid.Id &&
            error.Code == ContentValidationErrorCode.SharedContactDependencyInvalid);
    }

    [Fact]
    public void GrantCharge_RejectsUndefinedProgrammaticChargeKind()
    {
        SkillDefinition skill = ActiveSkill(
            "invalid_charge_kind",
            [new GrantChargeEffectDefinition((ChargeKind)int.MaxValue, 2m)]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [skill]));

        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.skills[0].effects[0].charge" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid);
    }

    [Fact]
    public void ProgrammaticCombatVocabulary_RejectsUndefinedEnumValues()
    {
        const int undefined = 999;
        ContentId poisonId = Id("poison");
        SkillDefinition active = ActiveSkill(
            "invalid_damage_vocabulary",
            [
                new DamageEffectDefinition(
                    (DamageElement)undefined,
                    10,
                    100,
                    new NeverCriticalDefinition(),
                    new HitCountDefinition(1, 2, (HitDistribution)undefined),
                    (DamageDrainMode)undefined)
            ]);
        SkillDefinition passive = new(
            Id("invalid_modifier_vocabulary"),
            "Invalid Modifier Vocabulary",
            "Exercises programmatic enum validation.",
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            modifiers:
            [
                new ElementalAffinityRuleModifierDefinition(
                    (DamageElement)undefined,
                    (ElementalAffinity)undefined),
                new AilmentResistanceRuleModifierDefinition(
                    poisonId,
                    (ResistanceLevel)undefined),
                new BasicAttackRuleModifierDefinition(
                    (DamageElement)undefined,
                    Drain: (DamageDrainMode)undefined)
            ]);
        EntityDefinition entity = Entity(
            "invalid_defense_vocabulary",
            Id("sample_race"),
            elementalAffinities:
            [
                new KeyValuePair<DamageElement, ElementalAffinity>(
                    DamageElement.Fire,
                    (ElementalAffinity)undefined)
            ],
            ailmentResistances:
            [
                new KeyValuePair<ContentId, ResistanceLevel>(
                    poisonId,
                    (ResistanceLevel)undefined)
            ]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [active, passive],
            entities: [entity],
            ailments: [Ailment("poison")]));

        string[] expectedPaths =
        [
            "$.skills[0].effects[0].elementId",
            "$.skills[0].effects[0].drain",
            "$.skills[0].effects[0].hits.distribution",
            "$.skills[1].modifiers[0].elementId",
            "$.skills[1].modifiers[0].affinityId",
            "$.skills[1].modifiers[1].resistance",
            "$.skills[1].modifiers[2].elementId",
            "$.skills[1].modifiers[2].drain",
            "$.entities[0].elementalAffinities.Fire",
            "$.entities[0].ailmentResistances.poison"
        ];
        Assert.All(expectedPaths, path => Assert.Contains(result.Errors, error =>
            error.JsonPath == path && error.Code == ContentValidationErrorCode.ShapeInvalid));
    }

    [Fact]
    public void MeaningfulOperandsCompositeConditionsAndAlmightyAffinitiesAreRequired()
    {
        SkillDefinition active = ActiveSkill(
            "shape_skill",
            [
                new DamageEffectDefinition(
                    DamageElement.Almighty, 1, 100, new NeverCriticalDefinition(), new HitCountDefinition(1, 1),
                    When: new AllConditionDefinition([])),
                new RemoveAilmentEffectDefinition(AilmentRemovalScope.Selected),
                new BreakAffinityEffectDefinition(
                    [DamageElement.Almighty], new InstantDurationDefinition()),
                new OverrideAffinityEffectDefinition(
                    [DamageElement.Almighty], ElementalAffinity.Resist, new InstantDurationDefinition()),
                new RemoveStatusEffectDefinition([]),
                new AnalyzeEffectDefinition([])
            ]);
        SkillDefinition passive = new(
            Id("empty_attack_replacement"), "Empty Attack Replacement", "No replacement fields.",
            SkillActivation.Passive, null, InheritanceGroup.Passive, new SkillInheritanceDefinition(true),
            modifiers:
            [
                new ElementalAffinityRuleModifierDefinition(DamageElement.Almighty, ElementalAffinity.Null),
                new BasicAttackRuleModifierDefinition()
            ]);
        EntityDefinition entity = Entity(
            "almighty_entity",
            Id("race"),
            elementalAffinities: [new KeyValuePair<DamageElement, ElementalAffinity>(DamageElement.Almighty, ElementalAffinity.Resist)]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [active, passive],
            entities: [entity],
            races: [new RaceDefinition(Id("race"), "Race")]));

        Assert.True(result.Errors.Count(error => error.Code == ContentValidationErrorCode.AlmightyAffinityForbidden) >= 4);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.skills[0].effects[0].when.all" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.skills[0].effects[1]" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.skills[0].effects[4]" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.skills[1].modifiers[1]" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid);
    }

    [Fact]
    public void AffinityBreak_RequiresAffectedNonAlmightyElementsAndAValidDuration()
    {
        SkillDefinition skill = ActiveSkill(
            "invalid_breaks",
            [
                new BreakAffinityEffectDefinition([], new InstantDurationDefinition()),
                new BreakAffinityEffectDefinition(
                    [DamageElement.Fire, DamageElement.Fire, DamageElement.Almighty],
                    new TurnDurationDefinition(0, Id("owner_turn_end"), false))
            ]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(), skills: [skill]));

        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.skills[0].effects[0].elementIds" &&
            error.Code == ContentValidationErrorCode.ShapeInvalid);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.skills[0].effects[1].elementIds[1]" &&
            error.Code == ContentValidationErrorCode.ListDuplicateValue);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.skills[0].effects[1].elementIds[2]" &&
            error.Code == ContentValidationErrorCode.AlmightyAffinityForbidden);
        Assert.Contains(result.Errors, error =>
            error.JsonPath == "$.skills[0].effects[1].duration.value" &&
            error.Code == ContentValidationErrorCode.ValueMustBePositive);
    }

    [Fact]
    public void InheritanceAssignmentsAndMutationFamiliesAreCheckedTogether()
    {
        SkillDefinition locked = new(
            Id("locked"), "Locked", "Cannot be inherited.", SkillActivation.Passive, null,
            InheritanceGroup.Passive, new SkillInheritanceDefinition(false, [Id("other_owner")]),
            mutation: new SkillMutationDefinition(Id("family"), 2),
            modifiers: [new NumericRuleModifierDefinition(NumericRuleModifierType.Accuracy, ModifierOperation.Add, 1)]);
        SkillDefinition duplicateTier = new(
            Id("duplicate_tier"), "Duplicate Tier", "Duplicate mutation tier.", SkillActivation.Passive, null,
            InheritanceGroup.Passive, new SkillInheritanceDefinition(true),
            mutation: new SkillMutationDefinition(Id("family"), 2),
            modifiers: [new NumericRuleModifierDefinition(NumericRuleModifierType.Accuracy, ModifierOperation.Add, 1)]);
        EntityDefinition entity = Entity(
            "owner",
            Id("race"),
            inheritanceRules: new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList),
                blockedSkillIds: [Id("locked")],
                allowedSkillIds: [Id("locked")]),
            baseSkillIds: [Id("locked"), Id("locked")],
            unlocks: [new SkillUnlockDefinition(1, Id("locked"))]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(),
            skills: [locked, duplicateTier],
            entities: [entity],
            races: [new RaceDefinition(Id("race"), "Race")]));

        AssertCodes(result,
            ContentValidationErrorCode.InheritanceListConflict,
            ContentValidationErrorCode.InheritanceExplicitAllowInvalid,
            ContentValidationErrorCode.EntitySkillAssignmentDuplicate,
            ContentValidationErrorCode.EntityUnlockLevelInvalid,
            ContentValidationErrorCode.MutationTierDuplicate,
            ContentValidationErrorCode.MutationTierGap);
    }

    [Fact]
    public void RegistrationsAndCustomParameterValidatorsAreExplicit()
    {
        var rejecting = new RejectingParameterValidator();
        SkillSystemRegistrationSnapshot registrations = ComprehensiveRegistrationBuilder()
            .RegisterFormula("reject_formula", rejecting)
            .RegisterCustomCondition("reject_condition", rejecting)
            .RegisterCustomAilmentBehavior("reject_behavior", rejecting)
            .Build();
        SkillDefinition skill = ActiveSkill(
            "custom_skill",
            [
                new SetResourceEffectDefinition(
                    Id("sp"),
                    new FormulaAmountDefinition(Id("reject_formula"), [new("ratio", -1m)])),
                new DamageEffectDefinition(
                    DamageElement.Fire, 1, 100, new NeverCriticalDefinition(), new HitCountDefinition(1, 1),
                    When: new CustomConditionDefinition(Id("reject_condition"), [new("ratio", -1m)])),
                new CustomEffectDefinition(Id("missing_custom_effect"))
            ]);
        AilmentDefinition ailment = Ailment(
            "custom_ailment",
            new CustomAilmentTurnBehaviorDefinition(Id("reject_behavior"), [new("ratio", -1m)]));

        ContentValidationResult result = _validator.Validate(Request(
            registrations, skills: [skill], ailments: [ailment]));

        Assert.Contains(result.Errors, error =>
            error.Code == ContentValidationErrorCode.ParameterValidationFailed &&
            error.JsonPath == "$.skills[0].effects[0].amount.parameters.ratio" &&
            error.Suggestion == "Use a nonnegative ratio.");
        Assert.Contains(result.Errors, error =>
            error.Code == ContentValidationErrorCode.RegistrationMissing &&
            error.JsonPath == "$.skills[0].effects[2].parameters");
        Assert.Equal(3, result.Errors.Count(error => error.Code == ContentValidationErrorCode.ParameterValidationFailed));
    }

    [Fact]
    public void AilmentProbabilitiesListsAndMultipliersAreValidated()
    {
        AilmentDefinition ailment = new(
            Id("fear"), "Fear", "Invalid fear.",
            new TurnDurationDefinition(0, Id("owner_turn_end"), false),
            new ChanceSkipOrFleeAilmentTurnBehaviorDefinition(70, 50, CompanionFleeOutcome.RecallToRoster),
            new AilmentModifiersDefinition(0, 0, 0, 0, false),
            new AilmentRecoveryDefinition(
                new NaturalAilmentRecoveryDefinition(101, Id("luck"), 0),
                [Id("battle_end"), Id("battle_end")]),
            groupIds: [Id("mental"), Id("mental")]);

        ContentValidationResult result = _validator.Validate(Request(
            ComprehensiveRegistrations(), ailments: [ailment]));

        Assert.Contains(result.Errors, error => error.JsonPath == "$.ailments[0].turnBehavior" &&
            error.Code == ContentValidationErrorCode.ValueOutOfRange);
        Assert.Equal(4, result.Errors.Count(error => error.Code == ContentValidationErrorCode.ValueMustBePositive));
        Assert.Contains(result.Errors, error => error.JsonPath == "$.ailments[0].recovery.natural.baseChance" &&
            error.Code == ContentValidationErrorCode.ValueOutOfRange);
        Assert.True(result.Errors.Count(error => error.Code == ContentValidationErrorCode.ListDuplicateValue) >= 2);
    }

    [Fact]
    public void UnsupportedDefinitionTypesAndQualifiedRegistrationsRemainHostControlled()
    {
        SkillDefinition skill = ActiveSkill(
            "host_skill",
            [new AnalyzeEffectDefinition([AnalysisLayer.Stats])],
            availability: new SkillAvailabilityDefinition([Id("host.pack:battle")]));

        ContentValidationResult result = _validator.Validate(Request(
            new SkillSystemRegistrationBuilder().RegisterContext("battle").Build(),
            skills: [skill]));

        AssertCodes(result,
            ContentValidationErrorCode.RegistrationMissing,
            ContentValidationErrorCode.DefinitionTypeUnsupported);
    }

    [Fact]
    public void DocumentDiagnosticsRetainManifestAndSourceProvenance()
    {
        ContentPackManifest manifest = new(
            4, "test.pack", SemanticVersion.Parse("1.0.0"), "Test", null, null,
            [new ContentPackDocumentReference("skills", "declared.skills.json")]);
        var supplied = new SourceContentDocument<SkillDefinition>(
            "undeclared.skills.json",
            "disk/undeclared.skills.json",
            new DeserializedContentDocument<SkillDefinition>(4, []));

        ContentValidationResult result = _validator.Validate(new SkillSystemValidationRequest(
            manifest, "pack.manifest.json", new SkillSystemRegistrationBuilder().Build(), [supplied]));

        Assert.Contains(result.Errors, error =>
            error.SourceName == "pack.manifest.json" && error.JsonPath == "$.schemaVersion" &&
            error.Code == ContentValidationErrorCode.DocumentSchemaVersionUnsupported);
        Assert.Contains(result.Errors, error =>
            error.SourceName == "pack.manifest.json" && error.JsonPath == "$.documents[0].path" &&
            error.Code == ContentValidationErrorCode.DocumentMissing);
        Assert.Contains(result.Errors, error =>
            error.SourceName == "disk/undeclared.skills.json" && error.JsonPath == "$" &&
            error.Code == ContentValidationErrorCode.DocumentNotDeclared);
        Assert.Contains(result.Errors, error =>
            error.SourceName == "disk/undeclared.skills.json" && error.JsonPath == "$.schemaVersion" &&
            error.Code == ContentValidationErrorCode.DocumentSchemaVersionUnsupported);
    }

    [Fact]
    public void RecordDiagnosticsFollowManifestThenAuthoredRecordOrder()
    {
        ContentPackManifest manifest = new(
            6, "test.pack", SemanticVersion.Parse("1.0.0"), "Test", null, null,
            [
                new ContentPackDocumentReference("races", "races.json"),
                new ContentPackDocumentReference("skills", "skills.json")
            ]);
        var races = new SourceContentDocument<RaceDefinition>(
            "races.json", "races.json",
            new DeserializedContentDocument<RaceDefinition>(6,
            [
                new RaceDefinition(Id("test.pack:qualified_first"), "First"),
                new RaceDefinition(Id("test.pack:qualified_second"), "Second")
            ]));
        var skills = new SourceContentDocument<SkillDefinition>(
            "skills.json", "skills.json",
            new DeserializedContentDocument<SkillDefinition>(6,
            [
                new SkillDefinition(
                    Id("active_after_races"), "Active", "Invalid active.",
                    SkillActivation.Active, null, InheritanceGroup.Utility,
                    new SkillInheritanceDefinition(true))
            ]));

        ContentValidationResult result = _validator.Validate(new SkillSystemValidationRequest(
            manifest, "manifest.json", ComprehensiveRegistrations(), [skills], raceDocuments: [races]));

        Assert.Equal("races.json", result.Errors[0].SourceName);
        Assert.Equal("$.races[0].id", result.Errors[0].JsonPath);
        Assert.Equal("$.races[1].id", result.Errors[1].JsonPath);
        Assert.Equal("skills.json", result.Errors[2].SourceName);
    }

    [Fact]
    public void ValidationSnapshotsAreImmutableAndValidatedTokenHasNoPublicConstructor()
    {
        var builder = new SkillSystemRegistrationBuilder().RegisterContext("battle");
        SkillSystemRegistrationSnapshot snapshot = builder.Build();
        builder.RegisterContext("field");
        var source = new SourceContentDocument<SkillDefinition>(
            "skills.json", "skills.json", new DeserializedContentDocument<SkillDefinition>(6, []));
        var supplied = new List<SourceContentDocument<SkillDefinition>> { source };
        SkillSystemValidationRequest request = new(
            new ContentPackManifest(6, "test.pack", SemanticVersion.Parse("1.0.0"), "Test", null, null,
                [new ContentPackDocumentReference("skills", "skills.json")]),
            "manifest.json", snapshot, supplied);
        supplied.Clear();

        Assert.Contains(Id("battle"), snapshot.ContextIds);
        Assert.DoesNotContain(Id("field"), snapshot.ContextIds);
        Assert.Single(request.SkillDocuments);
        Assert.Empty(typeof(ValidatedSkillSystemContentPack).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void PublicValidationBoundaryExposesNoSerializerGodotFilesystemOrLegacyTypes()
    {
        Type[] publicTypes = typeof(ISkillSystemContentValidator).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == typeof(ISkillSystemContentValidator).Namespace)
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
                candidate == typeof(FileInfo) || candidate == typeof(DirectoryInfo) ||
                candidate.Name == "SkillData" ||
                candidate.Name == string.Concat("Per", "sona", "Data"));
        }
    }

    private ContentPackManifest ReadManifest(string root, string fileName) =>
        _deserializer.DeserializeManifest(File.ReadAllText(TestContentPath.Resolve(root, fileName)), fileName);

    private SourceContentDocument<SkillDefinition> SkillDocument(string root, string fileName) =>
        new(fileName, fileName, _deserializer.DeserializeSkills(File.ReadAllText(TestContentPath.Resolve(root, fileName)), fileName));

    private SourceContentDocument<EntityDefinition> EntityDocument(string root, string fileName) =>
        new(fileName, fileName, _deserializer.DeserializeEntities(File.ReadAllText(TestContentPath.Resolve(root, fileName)), fileName));

    private SourceContentDocument<RaceDefinition> RaceDocument(string root, string fileName) =>
        new(fileName, fileName, _deserializer.DeserializeRaces(File.ReadAllText(TestContentPath.Resolve(root, fileName)), fileName));

    private SourceContentDocument<AilmentDefinition> AilmentDocument(string root, string fileName) =>
        new(fileName, fileName, _deserializer.DeserializeAilments(File.ReadAllText(TestContentPath.Resolve(root, fileName)), fileName));

    private static SkillSystemValidationRequest Request(
        SkillSystemRegistrationSnapshot registrations,
        IReadOnlyList<SkillDefinition>? skills = null,
        IReadOnlyList<EntityDefinition>? entities = null,
        IReadOnlyList<RaceDefinition>? races = null,
        IReadOnlyList<AilmentDefinition>? ailments = null)
    {
        ContentPackDocumentReference[] references =
        [
            new("skills", "skills.json"),
            new("entities", "entities.json"),
            new("races", "races.json"),
            new("ailments", "ailments.json")
        ];
        ContentPackManifest manifest = new(
            6, "test.pack", SemanticVersion.Parse("1.0.0"), "Test Pack", null, null, references);
        return new SkillSystemValidationRequest(
            manifest,
            "manifest.json",
            registrations,
            [new SourceContentDocument<SkillDefinition>("skills.json", "skills.json", new(6, skills ?? []))],
            [new SourceContentDocument<EntityDefinition>("entities.json", "entities.json", new(6, entities ?? []))],
            [new SourceContentDocument<RaceDefinition>("races.json", "races.json", new(6, races ?? []))],
            [new SourceContentDocument<AilmentDefinition>("ailments.json", "ailments.json", new(6, ailments ?? []))]);
    }

    private static SkillDefinition ActiveSkill(
        string id,
        IReadOnlyList<EffectDefinition> effects,
        IReadOnlyList<SkillCostDefinition>? costs = null,
        TargetingDefinition? targeting = null,
        SkillAvailabilityDefinition? availability = null) =>
        new(
            Id(id), id, id, SkillActivation.Active, SkillMenuGroup.Utility, InheritanceGroup.Utility,
            new SkillInheritanceDefinition(true),
            costs: costs,
            targeting: targeting,
            effects: effects,
            availability: availability ?? new SkillAvailabilityDefinition([Id("battle")]));

    private static SkillDefinition PassiveSkill(string id) =>
        new(
            Id(id), id, id, SkillActivation.Passive, null, InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            modifiers: [new NumericRuleModifierDefinition(NumericRuleModifierType.Accuracy, ModifierOperation.Add, 1)]);

    private static SkillDefinition InvalidIdentifierSkill() =>
        new(
            default,
            "Invalid ID",
            "Programmatic invalid identifier fixture.",
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            modifiers:
            [
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.Accuracy,
                    ModifierOperation.Add,
                    1)
            ]);

    private static EntityDefinition Entity(
        string id,
        ContentId raceId,
        EntityInheritanceRulesDefinition? inheritanceRules = null,
        IEnumerable<KeyValuePair<DamageElement, ElementalAffinity>>? elementalAffinities = null,
        IEnumerable<KeyValuePair<ContentId, ResistanceLevel>>? ailmentResistances = null,
        IEnumerable<ContentId>? baseSkillIds = null,
        IEnumerable<SkillUnlockDefinition>? unlocks = null) =>
        new(
            Id(id), id, id, Id("companion"), raceId, 1, 1,
            new EntityCapabilitiesDefinition(true, true, true),
            inheritanceRules ?? new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList)),
            [new KeyValuePair<ContentId, int>(Id("strength"), 1)],
            elementalAffinities,
            ailmentResistances,
            baseSkillIds: baseSkillIds,
            skillUnlocks: unlocks);

    private static AilmentDefinition Ailment(
        string id,
        AilmentTurnBehaviorDefinition? behavior = null) =>
        new(
            Id(id), id, id, new BattleDurationDefinition(),
            behavior ?? new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1, 0, 1, 1, false),
            new AilmentRecoveryDefinition());

    private static TargetingDefinition SingleEnemyTarget() =>
        new(TargetRelation.Enemy, TargetSelection.Single, TargetLifeState.Alive, false);

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class ProgrammaticDefaultIdDeserializer(SkillDefinition skill)
        : ISkillSystemDocumentDeserializer
    {
        private readonly SkillSystemJsonDeserializer _inner = new();

        public ContentPackManifest DeserializeManifest(string json, string sourceName) =>
            _inner.DeserializeManifest(json, sourceName);

        public DeserializedContentDocument<SkillDefinition> DeserializeSkills(string json, string sourceName) =>
            new(6, [skill]);

        public DeserializedContentDocument<EntityDefinition> DeserializeEntities(string json, string sourceName) =>
            _inner.DeserializeEntities(json, sourceName);

        public DeserializedContentDocument<RaceDefinition> DeserializeRaces(string json, string sourceName) =>
            _inner.DeserializeRaces(json, sourceName);

        public DeserializedContentDocument<AilmentDefinition> DeserializeAilments(string json, string sourceName) =>
            _inner.DeserializeAilments(json, sourceName);

        public DeserializedContentDocument<ItemDefinition> DeserializeItems(string json, string sourceName) =>
            _inner.DeserializeItems(json, sourceName);

        public DeserializedContentDocument<EquipmentDefinition> DeserializeEquipment(string json, string sourceName) =>
            _inner.DeserializeEquipment(json, sourceName);

        public DeserializedContentDocument<ShopCatalogDefinition> DeserializeShops(string json, string sourceName) =>
            _inner.DeserializeShops(json, sourceName);

        public DeserializedContentDocument<NegotiationDefinition> DeserializeNegotiations(string json, string sourceName) =>
            _inner.DeserializeNegotiations(json, sourceName);

        public DeserializedContentDocument<EncounterDefinition> DeserializeEncounters(string json, string sourceName) =>
            _inner.DeserializeEncounters(json, sourceName);

        public DeserializedContentDocument<DungeonDefinition> DeserializeDungeons(string json, string sourceName) =>
            _inner.DeserializeDungeons(json, sourceName);

        public DeserializedContentDocument<FusionRecipeDefinition> DeserializeFusionRecipes(string json, string sourceName) =>
            _inner.DeserializeFusionRecipes(json, sourceName);

        public DeserializedContentDocument<RulesetDefinition> DeserializeRulesets(string json, string sourceName) =>
            _inner.DeserializeRulesets(json, sourceName);
    }

    private static SkillSystemRegistrationSnapshot InvalidFixtureRegistrations() =>
        ComprehensiveRegistrationBuilder().Build();

    private static SkillSystemRegistrationSnapshot ComprehensiveRegistrations() =>
        ComprehensiveRegistrationBuilder().Build();

    private static SkillSystemRegistrationBuilder ComprehensiveRegistrationBuilder()
    {
        var builder = new SkillSystemRegistrationBuilder()
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterModifierTrack("attack", "defense", "agility")
            .RegisterEvent("owner_turn_end", "battle_end")
            .RegisterPhase("next_attack")
            .RegisterEntityKind("companion")
            .RegisterAlignment("light", "neutral", "dark")
            .RegisterNegotiationPersonality("upbeat")
            .RegisterAilmentGroup("mental", "physical")
            .RegisterBattleKind("random", "boss")
            .RegisterMoonPhase("full_moon", "new_moon")
            .RegisterCapability("summoner", "recruitable")
            .RegisterAction("attack", "guard")
            .RegisterStatus("focus")
            .RegisterEscapeRule("battle_escape");

        foreach (Type type in typeof(EffectDefinition).Assembly.GetTypes()
                     .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(EffectDefinition))))
        {
            AddSupportedType(builder, nameof(SkillSystemRegistrationBuilder.SupportEffect), type);
        }
        foreach (Type type in typeof(ConditionDefinition).Assembly.GetTypes()
                     .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(ConditionDefinition))))
        {
            AddSupportedType(builder, nameof(SkillSystemRegistrationBuilder.SupportCondition), type);
        }
        foreach (Type type in typeof(RuleModifierDefinition).Assembly.GetTypes()
                     .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(RuleModifierDefinition))))
        {
            AddSupportedType(builder, nameof(SkillSystemRegistrationBuilder.SupportModifier), type);
        }
        foreach (Type type in typeof(AilmentTurnBehaviorDefinition).Assembly.GetTypes()
                     .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(AilmentTurnBehaviorDefinition))))
        {
            AddSupportedType(builder, nameof(SkillSystemRegistrationBuilder.SupportAilmentBehavior), type);
        }

        return builder;
    }

    private static void AddSupportedType(SkillSystemRegistrationBuilder builder, string methodName, Type type)
    {
        MethodInfo method = typeof(SkillSystemRegistrationBuilder).GetMethod(methodName)!
            .MakeGenericMethod(type);
        method.Invoke(builder, null);
    }

    private static void AssertCodes(ContentValidationResult result, params ContentValidationErrorCode[] codes)
    {
        foreach (ContentValidationErrorCode code in codes)
        {
            Assert.Contains(result.Errors, error => error.Code == code);
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

    private sealed class RejectingParameterValidator : IContentParameterValidator
    {
        public IReadOnlyList<ContentParameterValidationIssue> Validate(
            IReadOnlyDictionary<string, object?> parameters) =>
            [new ContentParameterValidationIssue("ratio", "Ratio is invalid.", "Use a nonnegative ratio.")];
    }
}
