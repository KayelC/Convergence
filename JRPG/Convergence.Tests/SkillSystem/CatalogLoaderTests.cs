using System.Reflection;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using Xunit;

namespace Convergence.Tests.SkillSystem;

public sealed class CatalogLoaderTests
{
    private readonly SkillSystemCatalogLoader _loader = new();

    [Theory]
    [InlineData("0.0.0")]
    [InlineData("1.2.3-alpha.1+build.5")]
    [InlineData("999999999999999999999999.0.1")]
    public void SemanticVersion_ParsesStrictSemVerWithoutNumericCeilings(string value)
    {
        Assert.True(SemanticVersion.TryParse(value, out SemanticVersion parsed));
        Assert.Equal(value, parsed.ToString());
        Assert.Equal(parsed, SemanticVersion.Parse(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-01")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0+build+second")]
    [InlineData(" 1.0.0")]
    public void SemanticVersion_RejectsNonSemVerValues(string value)
    {
        Assert.False(SemanticVersion.TryParse(value, out _));
        Assert.Throws<ArgumentException>(() => SemanticVersion.Parse(value));
    }

    [Fact]
    public void SemanticVersion_UsesSemVerPrecedenceButExactEqualityIncludesBuildMetadata()
    {
        SemanticVersion alpha = SemanticVersion.Parse("1.0.0-alpha");
        SemanticVersion release = SemanticVersion.Parse("1.0.0");
        SemanticVersion buildOne = SemanticVersion.Parse("1.0.0+one");
        SemanticVersion buildTwo = SemanticVersion.Parse("1.0.0+two");

        Assert.True(alpha < release);
        Assert.Equal(0, buildOne.CompareTo(buildTwo));
        Assert.NotEqual(buildOne, buildTwo);
    }

    [Fact]
    public void Manifest_MapsTypedDependencyObjectsAndRejectsLegacyStrings()
    {
        var deserializer = new SkillSystemJsonDeserializer();
        ContentPackManifest manifest = deserializer.DeserializeManifest(
            Manifest("addon.pack", dependencies: "[{\"id\":\"core.pack\",\"version\":\"1.2.3-beta+7\"}]"),
            "addon.manifest.json");

        ContentPackDependency dependency = Assert.Single(manifest.Dependencies);
        Assert.Equal("core.pack", dependency.Id);
        Assert.Equal(SemanticVersion.Parse("1.2.3-beta+7"), dependency.Version);
        Assert.Throws<ContentDeserializationException>(() => deserializer.DeserializeManifest(
            Manifest("addon.pack", dependencies: "[\"core.pack\"]"),
            "legacy.manifest.json"));
    }

    [Fact]
    public void ReferenceFixturePack_BuildsQualifiedImmutableCatalog()
    {
        string root = Path.Combine(FindRepositoryRoot(), "Data", "Jsons");
        string manifestName = "skill_system_redesign.manifest.sample.json";
        string[] documentNames =
        [
            "skill_system_redesign.entities.sample.json",
            "skill_system_redesign.skills.sample.json",
            "skill_system_redesign.races.sample.json"
        ];
        var bundle = new ContentPackTextBundle(
            manifestName,
            File.ReadAllText(Path.Combine(root, manifestName)),
            documentNames.Select(name => new ContentDocumentText(
                name, name, File.ReadAllText(Path.Combine(root, name)))));

        GameDataCatalog catalog = _loader.Load(new SkillSystemCatalogLoadRequest(
            ReferenceRegistrations(), [bundle])).RequireCatalog();

        ContentId skillId = Id("convergence.skill_system_redesign_sample:ice_boost_sample");
        ContentId entityId = Id("convergence.skill_system_redesign_sample:cinder_fodder_sample");
        SkillDefinition skill = catalog.GetRequiredSkill(skillId);
        EntityDefinition entity = catalog.GetRequiredEntity(entityId);

        Assert.Equal(skillId, skill.Id);
        Assert.Equal(Id("convergence.skill_system_redesign_sample:sample_spirit"), entity.RaceId);
        Assert.Equal([skillId], entity.BaseSkillIds);
        Assert.Equal(Id("demon"), entity.EntityKindId);
        Assert.Contains(Id("strength"), entity.Stats.Keys);
        Assert.Throws<ArgumentException>(() => catalog.TryGetSkill(Id("ice_boost_sample"), out _));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ContentId, SkillDefinition>)catalog.Skills).Add(Id("other.pack:test"), skill));
    }

    [Fact]
    public void Loader_UsesManifestDocumentOrderInsteadOfSuppliedBundleOrder()
    {
        string manifest = Manifest(
            "ordered.pack",
            documents: "[{\"type\":\"races\",\"path\":\"first.json\"},{\"type\":\"races\",\"path\":\"second.json\"}]");
        var bundle = Bundle(manifest,
            Document("second.json", RaceDocument("second")),
            Document("first.json", RaceDocument("first")));

        GameDataCatalog catalog = Load(bundle).RequireCatalog();

        Assert.Equal(
            [Id("ordered.pack:first"), Id("ordered.pack:second")],
            catalog.Races.Keys.ToArray());
    }

    [Fact]
    public void DirectDependency_ResolvesExactVersionAndQualifiesCrossPackReferences()
    {
        ContentPackTextBundle core = Bundle(
            Manifest("core.pack", version: "1.2.0+release", documents:
                "[{\"type\":\"races\",\"path\":\"races.json\"},{\"type\":\"skills\",\"path\":\"skills.json\"}]"),
            Document("races.json", RaceDocument("spirit")),
            Document("skills.json", SkillDocument("inherit_me")));
        ContentPackTextBundle addon = Bundle(
            Manifest("addon.pack", dependencies:
                "[{\"id\":\"core.pack\",\"version\":\"1.2.0+release\"}]", documents:
                "[{\"type\":\"entities\",\"path\":\"entities.json\"}]"),
            Document("entities.json", EntityDocument(
                "guest", "core.pack:spirit", "core.pack:inherit_me", "core.pack:inherit_me")));

        GameDataCatalog catalog = Load(core, addon).RequireCatalog();
        EntityDefinition entity = catalog.GetRequiredEntity(Id("addon.pack:guest"));

        Assert.Equal(Id("core.pack:spirit"), entity.RaceId);
        Assert.Equal([Id("core.pack:inherit_me")], entity.BaseSkillIds);
        Assert.Equal([Id("core.pack:inherit_me")], entity.InheritanceRules.AllowedSkillIds);
        Assert.Equal(Id("demon"), entity.EntityKindId);
    }

    [Fact]
    public void DependencyDiagnostics_AggregateDuplicateSelfMissingAndExactVersionFailures()
    {
        ContentPackTextBundle core = EmptyBundle("core.pack", "1.0.0+one");
        ContentPackTextBundle addon = Bundle(Manifest(
            "addon.pack",
            dependencies:
                "[{\"id\":\"core.pack\",\"version\":\"1.0.0+two\"}," +
                "{\"id\":\"core.pack\",\"version\":\"1.0.0+two\"}," +
                "{\"id\":\"addon.pack\",\"version\":\"1.0.0\"}," +
                "{\"id\":\"missing.pack\",\"version\":\"1.0.0\"}]"));

        CatalogLoadResult result = Load(core, addon, EmptyBundle("core.pack", "1.0.0+one"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Catalog);
        Assert.Throws<CatalogLoadException>(() => result.RequireCatalog());
        Assert.Contains(result.Diagnostics, error => error.Code == CatalogLoadDiagnosticCode.PackDuplicate);
        Assert.Contains(result.Diagnostics, error => error.Code == CatalogLoadDiagnosticCode.DependencyDuplicate);
        Assert.Contains(result.Diagnostics, error => error.Code == CatalogLoadDiagnosticCode.DependencySelfReference);
        Assert.Contains(result.Diagnostics, error => error.Code == CatalogLoadDiagnosticCode.DependencyMissing);
        Assert.Equal(2, result.Diagnostics.Count(error =>
            error.Code == CatalogLoadDiagnosticCode.DependencyVersionMismatch));
        Assert.DoesNotContain(result.Diagnostics, error => error.Code == CatalogLoadDiagnosticCode.DependencyCycle);
    }

    [Fact]
    public void DependencyCycle_ReportsOnlyCycleMembersNotBlockedDependents()
    {
        ContentPackTextBundle a = EmptyBundle("a.pack", dependencies: "[{\"id\":\"b.pack\",\"version\":\"1.0.0\"}]");
        ContentPackTextBundle b = EmptyBundle("b.pack", dependencies: "[{\"id\":\"a.pack\",\"version\":\"1.0.0\"}]");
        ContentPackTextBundle c = EmptyBundle("c.pack", dependencies: "[{\"id\":\"a.pack\",\"version\":\"1.0.0\"}]");

        CatalogLoadResult result = Load(a, b, c);

        Assert.Equal(
            ["a.pack", "b.pack"],
            result.Diagnostics
                .Where(error => error.Code == CatalogLoadDiagnosticCode.DependencyCycle)
                .Select(error => error.PackId!)
                .ToArray());
    }

    [Fact]
    public void TransitiveOnlyExternalReference_IsRejected()
    {
        ContentPackTextBundle core = Bundle(
            Manifest("core.pack", documents: "[{\"type\":\"races\",\"path\":\"races.json\"}]"),
            Document("races.json", RaceDocument("spirit")));
        ContentPackTextBundle middle = EmptyBundle(
            "middle.pack", dependencies: "[{\"id\":\"core.pack\",\"version\":\"1.0.0\"}]");
        ContentPackTextBundle addon = Bundle(
            Manifest("addon.pack", dependencies:
                "[{\"id\":\"middle.pack\",\"version\":\"1.0.0\"}]", documents:
                "[{\"type\":\"entities\",\"path\":\"entities.json\"}]"),
            Document("entities.json", EntityDocument("guest", "core.pack:spirit")));

        CatalogLoadResult result = Load(core, middle, addon);

        CatalogLoadDiagnostic error = Assert.Single(result.Diagnostics,
            diagnostic => diagnostic.Code == CatalogLoadDiagnosticCode.ExternalDependencyNotDeclared);
        Assert.Equal("$.entities[0].raceId", error.JsonPath);
        Assert.DoesNotContain(result.Diagnostics,
            diagnostic => diagnostic.Code == CatalogLoadDiagnosticCode.ExternalReferenceMissing);
    }

    [Fact]
    public void ExternalReferences_DistinguishMissingAndWrongTargetTypes()
    {
        ContentPackTextBundle core = Bundle(
            Manifest("core.pack", documents: "[{\"type\":\"skills\",\"path\":\"skills.json\"}]"),
            Document("skills.json", SkillDocument("not_a_race")));
        ContentPackTextBundle addon = Bundle(
            Manifest("addon.pack", dependencies:
                "[{\"id\":\"core.pack\",\"version\":\"1.0.0\"}]", documents:
                "[{\"type\":\"entities\",\"path\":\"entities.json\"}]"),
            Document("entities.json", $$"""
            { "schemaVersion": 1, "entities": [
              {{EntityRecord("wrong", "core.pack:not_a_race")}},
              {{EntityRecord("missing", "core.pack:missing_race")}}
            ] }
            """));

        CatalogLoadResult result = Load(core, addon);

        Assert.Contains(result.Diagnostics, error =>
            error.Code == CatalogLoadDiagnosticCode.ExternalReferenceWrongType &&
            error.JsonPath == "$.entities[0].raceId");
        Assert.Contains(result.Diagnostics, error =>
            error.Code == CatalogLoadDiagnosticCode.ExternalReferenceMissing &&
            error.JsonPath == "$.entities[1].raceId");
    }

    [Fact]
    public void CrossPackExplicitAllow_RejectsNonInheritableSkill()
    {
        ContentPackTextBundle core = Bundle(
            Manifest("core.pack", documents:
                "[{\"type\":\"skills\",\"path\":\"skills.json\"},{\"type\":\"races\",\"path\":\"races.json\"}]"),
            Document("skills.json", SkillDocument("sealed_skill", isInheritable: false)),
            Document("races.json", RaceDocument("spirit")));
        ContentPackTextBundle addon = Bundle(
            Manifest("addon.pack", dependencies:
                "[{\"id\":\"core.pack\",\"version\":\"1.0.0\"}]", documents:
                "[{\"type\":\"entities\",\"path\":\"entities.json\"}]"),
            Document("entities.json", EntityDocument(
                "guest", "core.pack:spirit", allowedSkillId: "core.pack:sealed_skill")));

        CatalogLoadResult result = Load(core, addon);

        CatalogLoadDiagnostic error = Assert.Single(result.Diagnostics,
            diagnostic => diagnostic.Code == CatalogLoadDiagnosticCode.CrossPackInheritanceInvalid);
        Assert.Equal("$.entities[0].inheritanceRules.allowedSkillIds[0]", error.JsonPath);
    }

    [Fact]
    public void CatalogQualification_CoversContentReferencesButLeavesHostVocabularyLocal()
    {
        string manifest = Manifest("qualification.pack", documents:
            "[{\"type\":\"ailments\",\"path\":\"ailments.json\"}," +
            "{\"type\":\"races\",\"path\":\"races.json\"}," +
            "{\"type\":\"skills\",\"path\":\"skills.json\"}," +
            "{\"type\":\"entities\",\"path\":\"entities.json\"}]");
        ContentPackTextBundle bundle = Bundle(
            manifest,
            Document("ailments.json", AilmentDocument("poison")),
            Document("races.json", RaceDocument("spirit")),
            Document("skills.json", TriggerSkillDocument()),
            Document("entities.json", EntityDocument("owner", "spirit", "skill", "skill", "poison")));

        CatalogLoadResult result = Load(bundle);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine,
            result.Diagnostics.Select(error => $"{error.Code} {error.JsonPath}: {error.Message}")));
        GameDataCatalog catalog = result.RequireCatalog();
        ContentId packSkill = Id("qualification.pack:skill");
        ContentId packAilment = Id("qualification.pack:poison");
        SkillDefinition skill = catalog.GetRequiredSkill(packSkill);
        EntityDefinition entity = catalog.GetRequiredEntity(Id("qualification.pack:owner"));
        PassiveTriggerDefinition trigger = Assert.Single(skill.Triggers);
        var condition = Assert.IsType<HasAilmentConditionDefinition>(trigger.When);
        var remove = Assert.IsType<RemoveAilmentEffectDefinition>(Assert.Single(trigger.Effects));

        Assert.Equal(Id("qualification.pack:mutation_family"), skill.Mutation!.FamilyId);
        Assert.Equal([Id("qualification.pack:owner")], skill.Inheritance.ExclusiveOwnerEntityIds);
        Assert.Equal([packAilment], condition.AilmentIds);
        Assert.Equal([packAilment], remove.AilmentIds);
        Assert.Equal(Id("qualification.pack:spirit"), entity.RaceId);
        Assert.Contains(packAilment, entity.AilmentResistances.Keys);
        Assert.Equal([packSkill], entity.BaseSkillIds);
        Assert.Equal([packSkill], entity.InheritanceRules.AllowedSkillIds);
        Assert.Equal(Id("owner_turn_end"), trigger.EventId);
        Assert.Equal(Id("demon"), entity.EntityKindId);
        Assert.Contains(Id("strength"), entity.Stats.Keys);
    }

    [Fact]
    public void PathAndDocumentDiagnostics_AggregateInDeterministicOrder()
    {
        ContentPackTextBundle bundle = Bundle(
            Manifest("paths.pack", documents:
                "[{\"type\":\"races\",\"path\":\"../bad.json\"}," +
                "{\"type\":\"unknown\",\"path\":\"missing.json\"}]"),
            Document("../bad.json", RaceDocument("race")),
            Document("extra.json", RaceDocument("extra")),
            Document("extra.json", RaceDocument("duplicate")));
        SkillSystemCatalogLoadRequest request = Request(bundle);

        CatalogLoadResult first = _loader.Load(request);
        CatalogLoadResult second = _loader.Load(request);

        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(
            [
                CatalogLoadDiagnosticCode.DocumentPathInvalid,
                CatalogLoadDiagnosticCode.DocumentPathDuplicate,
                CatalogLoadDiagnosticCode.DocumentPathInvalid,
                CatalogLoadDiagnosticCode.DocumentTypeUnsupported,
                CatalogLoadDiagnosticCode.DocumentMissing,
                CatalogLoadDiagnosticCode.DocumentUnexpected,
                CatalogLoadDiagnosticCode.DocumentUnexpected
            ],
            first.Diagnostics.Select(error => error.Code).ToArray());
    }

    [Fact]
    public void ParsingAndValidationFailures_AreSerializerNeutralAndPreventCatalogCreation()
    {
        CatalogLoadResult manifestFailure = Load(Bundle("{", []));
        ContentPackTextBundle documentFailure = Bundle(
            Manifest("parse.pack", documents: "[{\"type\":\"races\",\"path\":\"races.json\"}]"),
            Document("races.json", "{"));
        ContentPackTextBundle validationFailure = Bundle(
            Manifest("validation.pack", documents: "[{\"type\":\"skills\",\"path\":\"skills.json\"}]"),
            Document("skills.json",
                "{\"schemaVersion\":1,\"skills\":[{\"id\":\"empty_passive\",\"displayName\":\"Empty\",\"description\":\"Empty.\",\"activation\":\"passive\",\"inheritanceGroupId\":\"passive\",\"inheritance\":{\"isInheritable\":true}}]}"));

        Assert.Equal(CatalogLoadDiagnosticCode.ManifestDeserializationFailed,
            Assert.Single(manifestFailure.Diagnostics).Code);
        Assert.Equal(CatalogLoadDiagnosticCode.DocumentDeserializationFailed,
            Assert.Single(Load(documentFailure).Diagnostics).Code);
        CatalogLoadDiagnostic validation = Assert.Single(Load(validationFailure).Diagnostics);
        Assert.Equal(CatalogLoadDiagnosticCode.ContentValidationFailed, validation.Code);
        Assert.NotNull(validation.ValidationCode);
    }

    [Fact]
    public void LoadInputsSnapshotCallerCollectionsAndPublicBoundaryRemainsPortable()
    {
        var documents = new List<ContentDocumentText>();
        ContentPackTextBundle bundle = new("empty.manifest.json", Manifest("empty.pack"), documents);
        documents.Add(Document("unexpected.json", RaceDocument("late")));
        var bundles = new List<ContentPackTextBundle> { bundle };
        SkillSystemCatalogLoadRequest request = new(ReferenceRegistrations(), bundles);
        bundles.Clear();

        Assert.Empty(bundle.Documents);
        Assert.Single(request.Bundles);
        Assert.True(_loader.Load(request).IsSuccess);

        Type catalogNamespaceType = typeof(ISkillSystemCatalogLoader);
        Type[] publicTypes = catalogNamespaceType.Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == catalogNamespaceType.Namespace)
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
                candidate == typeof(JRPGPrototype.Data.SkillData) ||
                candidate == typeof(JRPGPrototype.Data.PersonaData));
        }
    }

    private CatalogLoadResult Load(params ContentPackTextBundle[] bundles) =>
        _loader.Load(Request(bundles));

    private static SkillSystemCatalogLoadRequest Request(params ContentPackTextBundle[] bundles) =>
        new(ReferenceRegistrations(), bundles);

    private static ContentPackTextBundle EmptyBundle(
        string id,
        string version = "1.0.0",
        string dependencies = "[]") =>
        Bundle(Manifest(id, version, dependencies));

    private static ContentPackTextBundle Bundle(
        string manifest,
        params ContentDocumentText[] documents) =>
        new("manifest.json", manifest, documents);

    private static ContentDocumentText Document(string path, string json) =>
        new(path, path, json);

    private static string Manifest(
        string id,
        string version = "1.0.0",
        string dependencies = "[]",
        string documents = "[]") => $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "version": "{{version}}",
          "displayName": "{{id}}",
          "dependencies": {{dependencies}},
          "documents": {{documents}}
        }
        """;

    private static string RaceDocument(string id) => $$"""
        { "schemaVersion": 1, "races": [{ "id": "{{id}}", "displayName": "{{id}}" }] }
        """;

    private static string SkillDocument(string id, bool isInheritable = true) => $$"""
        {
          "schemaVersion": 1,
          "skills": [{
            "id": "{{id}}",
            "displayName": "{{id}}",
            "description": "Reference passive.",
            "activation": "passive",
            "inheritanceGroupId": "passive",
            "inheritance": { "isInheritable": {{isInheritable.ToString().ToLowerInvariant()}} },
            "modifiers": [{ "type": "accuracy", "operation": "add", "value": 1 }]
          }]
        }
        """;

    private static string EntityDocument(
        string id,
        string raceId,
        string? baseSkillId = null,
        string? allowedSkillId = null,
        string? ailmentId = null) => $$"""
        { "schemaVersion": 1, "entities": [
          {{EntityRecord(id, raceId, baseSkillId, allowedSkillId, ailmentId)}}
        ] }
        """;

    private static string EntityRecord(
        string id,
        string raceId,
        string? baseSkillId = null,
        string? allowedSkillId = null,
        string? ailmentId = null)
    {
        string baseSkills = baseSkillId is null ? "[]" : $"[\"{baseSkillId}\"]";
        string allowedSkills = allowedSkillId is null ? "[]" : $"[\"{allowedSkillId}\"]";
        string ailments = ailmentId is null ? "{}" : $"{{\"{ailmentId}\":\"resistant\"}}";
        return $$"""
        {
          "id": "{{id}}", "displayName": "{{id}}", "description": "Reference entity.",
          "entityKind": "demon", "raceId": "{{raceId}}", "rank": 1, "baseLevel": 1,
          "capabilities": { "recruitable": true, "fusionEligible": true, "compendiumEligible": true },
          "inheritanceRules": {
            "groupPolicy": { "mode": "deny_list", "groupIds": [] },
            "blockedSkillIds": [], "allowedSkillIds": {{allowedSkills}}
          },
          "stats": { "strength": 1 }, "elementalAffinities": {},
          "ailmentResistances": {{ailments}}, "baseSkillIds": {{baseSkills}}, "skillUnlocks": []
        }
        """;
    }

    private static string AilmentDocument(string id) => $$"""
        {
          "schemaVersion": 1,
          "ailments": [{
            "id": "{{id}}", "displayName": "{{id}}", "description": "Reference ailment.",
            "defaultDuration": { "type": "turns", "value": 3, "tick": "owner_turn_end", "suspendWhileReserve": false },
            "turnBehavior": { "type": "normal" },
            "modifiers": {
              "evasionMultiplier": 1, "criticalChanceTakenBonus": 0,
              "damageTakenMultiplier": 1, "damageDealtMultiplier": 1, "isRigidBody": false
            },
            "recovery": { "removeOnEvents": [] }
          }]
        }
        """;

    private static string TriggerSkillDocument() =>
        """
        {
          "schemaVersion": 1,
          "skills": [{
            "id": "skill", "displayName": "Skill", "description": "Reference trigger.",
            "activation": "passive", "inheritanceGroupId": "passive",
            "inheritance": { "isInheritable": true, "exclusiveOwnerEntityIds": ["owner"] },
            "mutation": { "familyId": "mutation_family", "tier": 1 },
            "triggers": [{
              "event": "owner_turn_end",
              "when": { "type": "actor_has_ailment", "ailmentIds": ["poison"] },
              "effects": [{ "type": "remove_ailment", "scope": "selected", "ailmentIds": ["poison"] }]
            }]
          }]
        }
        """;

    private static SkillSystemRegistrationSnapshot ReferenceRegistrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterEntityKind("demon")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEvent("owner_turn_end")
            .SupportModifier<NumericRuleModifierDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportCondition<HasAilmentConditionDefinition>()
            .SupportEffect<RemoveAilmentEffectDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .Build();

    private static ContentId Id(string value) => ContentId.Parse(value);

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
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JRPG.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find JRPG.sln.");
    }
}
