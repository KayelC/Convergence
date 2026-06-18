using System.Reflection;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class RuntimePersistenceSnapshotTests
{
    [Fact]
    public void RuntimeSaveSnapshot_ValidatesRepresentativeCleanSessionAndRestoresActors()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot();

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        RuntimeSaveGameSnapshot valid = result.RequireValidSnapshot();
        RuntimeActorSnapshot restored = RuntimeActorStateSet.FromSnapshot(valid.Actors[0]).ToSnapshot();
        Assert.Equal(Id("convergence.clean_battle_demo:frost_duelist_demo"), restored.Identity.EntityDefinitionId);
        Assert.Equal(Id("convergence.shared_effects_demo:medicine_demo"), valid.Inventory.ItemQuantities.Keys.Single());
        Assert.Equal(Id("convergence.catalog_surface_sample:tartarus_sample"), valid.Field.DungeonProgress.DungeonId);
        Assert.Equal(2, valid.Checkpoints.Entries.Count);
    }

    [Fact]
    public void RuntimeSaveSnapshot_DefensivelyCopiesCollectionsAndCheckpointOrder()
    {
        GameDataCatalog catalog = LoadCatalog();
        List<RuntimeActorSnapshot> actors = [CreateActor(RuntimeInstanceId.Parse("frost"), Id("convergence.clean_battle_demo:frost_duelist_demo"))];
        List<KeyValuePair<ContentId, string>> hostContext = [new(Id("scene"), "/root/Frost")];
        List<RuntimeCheckpointEntrySnapshot> checkpoints =
        [
            new(0, RuntimeCheckpointKind.SaveCreated, "Save created.", RuntimeInstanceId.Parse("frost"))
        ];
        RuntimeActorReferenceSnapshot frostRef = Reference(actors[0]);

        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            actors,
            hostContext,
            checkpoints,
            new RuntimePartyStockSnapshot(frostRef, 5, activeParty: [frostRef], activeForm: frostRef));
        actors.Add(CreateActor(RuntimeInstanceId.Parse("ember"), Id("convergence.clean_battle_demo:ember_duelist_demo")));
        hostContext.Add(new KeyValuePair<ContentId, string>(Id("late"), "mutation"));
        checkpoints.Add(new RuntimeCheckpointEntrySnapshot(1, RuntimeCheckpointKind.HostAction, "Late mutation."));

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.True(result.IsValid);
        Assert.Single(snapshot.Actors);
        Assert.Single(snapshot.HostContext);
        Assert.Single(snapshot.Checkpoints.Entries);
        Assert.Equal(0, snapshot.Checkpoints.Entries[0].Sequence);
    }

    [Fact]
    public void RuntimeSaveValidator_AggregatesGraphAndCatalogDiagnostics()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeActorSnapshot frost = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            Id("missing.pack:missing_entity"),
            learnedSkills: [Id("missing.pack:missing_skill")],
            ailments: [new RuntimeTimedStateSnapshot(Id("missing.pack:missing_ailment"), 1)]);
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            actors: [frost, frost],
            partyStock: new RuntimePartyStockSnapshot(
                new RuntimeActorReferenceSnapshot(RuntimeInstanceId.Parse("ghost_owner"), Id("convergence.clean_battle_demo:frost_duelist_demo"), "Ghost Owner"),
                5,
                activeParty: [new RuntimeActorReferenceSnapshot(RuntimeInstanceId.Parse("ghost"), Id("convergence.clean_battle_demo:frost_duelist_demo"), "Ghost")],
                activeForm: new RuntimeActorReferenceSnapshot(RuntimeInstanceId.Parse("missing_form"), Id("convergence.clean_battle_demo:frost_duelist_demo"), "Missing"),
                personaStock: [],
                demonStock: []),
            inventory: new RuntimeInventorySnapshot(
                [new KeyValuePair<ContentId, int>(Id("missing.pack:missing_item"), 1)],
                [new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(EquipmentSlot.Weapon, [Id("missing.pack:missing_equipment")])]),
            equipment: new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Armor, Id("missing.pack:missing_armor"))
            ]),
            field: new RuntimeFieldSnapshot(
                RuntimeFieldLocation.Dungeon,
                new RuntimeDungeonProgressSnapshot(Id("missing.pack:missing_dungeon"))),
            compendium: new CompendiumStateSnapshot(
            [
                new CompendiumEntrySnapshot(Id("missing.pack:missing_species"), "Missing", 1, skillIds: [Id("missing.pack:missing_skill")])
            ]),
            knowledge: new RuntimeKnowledgeSnapshot(
                elementalAffinities: [new RuntimeElementalAffinityKnowledgeSnapshot(Id("missing.pack:missing_target"), DamageElement.Fire, ElementalAffinity.Weak)],
                ailmentResistances: [new RuntimeAilmentResistanceKnowledgeSnapshot(Id("missing.pack:missing_target"), Id("missing.pack:missing_ailment"), ResistanceLevel.Resistant)]),
            checkpoints:
            [
                new(2, RuntimeCheckpointKind.SaveCreated, "Second."),
                new(1, RuntimeCheckpointKind.ActorRestored, "Out of order.", RuntimeInstanceId.Parse("ghost"))
            ]);

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.DuplicateActorInstanceId);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingActorReference);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingActiveFormReference);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogEntity);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogSkill);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogItem);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogEquipment);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogDungeon);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogAilment);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCompendiumEntity);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.KnowledgeTargetMissing);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.InvalidCheckpoint);
        Assert.Throws<RuntimeSaveValidationException>(() => result.RequireValidSnapshot());
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsUnsupportedContractVersion()
    {
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(contractVersion: 2);
        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, LoadCatalog());

        RuntimeSaveValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(RuntimeSaveValidationCode.ContractVersionUnsupported, diagnostic.Code);
        Assert.Equal("$.contractVersion", diagnostic.Path);
    }

    [Fact]
    public void RuntimePersistenceContracts_ExposeNoHostSerializerOrLegacyTypes()
    {
        Type[] runtimeTypes = typeof(RuntimeSaveGameSnapshot).Assembly.GetExportedTypes()
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
                        foreach (ParameterInfo parameter in method.GetParameters()) AssertAllowed(parameter.ParameterType, forbidden);
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

    internal static RuntimeSaveGameSnapshot CreateSaveSnapshot(
        IEnumerable<RuntimeActorSnapshot>? actors = null,
        IEnumerable<KeyValuePair<ContentId, string>>? hostContext = null,
        IEnumerable<RuntimeCheckpointEntrySnapshot>? checkpoints = null,
        RuntimePartyStockSnapshot? partyStock = null,
        RuntimeInventorySnapshot? inventory = null,
        RuntimeEquipmentSnapshot? equipment = null,
        RuntimeFieldSnapshot? field = null,
        CompendiumStateSnapshot? compendium = null,
        RuntimeKnowledgeSnapshot? knowledge = null,
        int contractVersion = RuntimeSaveGameSnapshot.CurrentContractVersion)
    {
        RuntimeActorSnapshot frost = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"),
            learnedSkills:
            [
                Id("convergence.clean_battle_demo:frost_lance_demo"),
                Id("convergence.skill_system_redesign_sample:ice_boost_sample")
            ]);
        RuntimeActorSnapshot ember = CreateActor(
            RuntimeInstanceId.Parse("ember"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorReferenceSnapshot frostRef = Reference(frost);
        RuntimeActorReferenceSnapshot emberRef = Reference(ember);

        return new RuntimeSaveGameSnapshot(
            SemanticVersion.Parse("1.0.0"),
            actors ?? [frost, ember],
            partyStock ?? new RuntimePartyStockSnapshot(
                frostRef,
                5,
                activeParty: [frostRef],
                reserveMembers: [emberRef],
                activeForm: frostRef,
                personaStock: [],
                demonStock: [frostRef, emberRef]),
            inventory ?? new RuntimeInventorySnapshot(
                [new KeyValuePair<ContentId, int>(Id("convergence.shared_effects_demo:medicine_demo"), 2)],
                [
                    new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(
                        EquipmentSlot.Weapon,
                        [Id("convergence.catalog_surface_sample:shortsword_sample")])
                ]),
            equipment ?? new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<EquipmentSlot, ContentId>(
                    EquipmentSlot.Weapon,
                    Id("convergence.catalog_surface_sample:shortsword_sample"))
            ]),
            new RuntimeWalletSnapshot(1234),
            field ?? new RuntimeFieldSnapshot(
                RuntimeFieldLocation.Dungeon,
                new RuntimeDungeonProgressSnapshot(
                    Id("convergence.catalog_surface_sample:tartarus_sample"),
                    currentFloor: 5,
                    maxFloorReached: 10,
                    unlockedTerminals: [1, 5],
                    defeatedBossIds: [Id("convergence.catalog_surface_sample:thebel_training_sample")])),
            compendium ?? new CompendiumStateSnapshot(
            [
                new CompendiumEntrySnapshot(
                    Id("convergence.clean_battle_demo:frost_duelist_demo"),
                    "Frost Duelist",
                    5,
                    [new KeyValuePair<ContentId, int>(Id("magic"), 8)],
                    [Id("convergence.clean_battle_demo:frost_lance_demo")])
            ]),
            knowledge ?? new RuntimeKnowledgeSnapshot(
                elementalAffinities:
                [
                    new RuntimeElementalAffinityKnowledgeSnapshot(
                        Id("convergence.clean_battle_demo:ember_duelist_demo"),
                        DamageElement.Ice,
                        ElementalAffinity.Weak)
                ],
                ailmentResistances:
                [
                    new RuntimeAilmentResistanceKnowledgeSnapshot(
                        Id("convergence.clean_battle_demo:ember_duelist_demo"),
                        Id("convergence.shared_effects_demo:poison_demo"),
                        ResistanceLevel.Normal)
                ]),
            new RuntimeSessionProgressSnapshot(
                Id("new_moon"),
                elapsedTicks: 42,
                counters: [new KeyValuePair<ContentId, long>(Id("battles_won"), 1)],
                flags: [Id("tutorial_complete")]),
            new RuntimeCheckpointLogSnapshot(checkpoints ??
            [
                new(0, RuntimeCheckpointKind.SaveCreated, "Save created."),
                new(1, RuntimeCheckpointKind.ActorRestored, "Frost restored.", RuntimeInstanceId.Parse("frost"))
            ]),
            hostContext ?? [new KeyValuePair<ContentId, string>(Id("scene"), "clean_save_demo")],
            contractVersion);
    }

    internal static RuntimeActorSnapshot CreateActor(
        RuntimeInstanceId instanceId,
        ContentId entityId,
        IEnumerable<ContentId>? learnedSkills = null,
        IEnumerable<RuntimeTimedStateSnapshot>? ailments = null) =>
        new(
            new RuntimeActorIdentitySnapshot(instanceId, entityId, Id("demon"), entityId.ToString()),
            new RuntimeActorOwnershipSnapshot(Id("host"), Id("player_team")),
            new RuntimeActorDeploymentSnapshot(RuntimeActorDeployment.Deployed, IsActive: true),
            new RuntimeProgressionSnapshot(5, 0, 0, 0),
            [
                new RuntimeResourceSnapshot(Id("hp"), 50, 75),
                new RuntimeResourceSnapshot(Id("sp"), 20, 30)
            ],
            new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 8)],
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 8)]),
            new RuntimeSkillStateSnapshot(learnedSkills ?? [Id("convergence.clean_battle_demo:frost_lance_demo")], learnedSkills ?? [Id("convergence.clean_battle_demo:frost_lance_demo")]),
            new RuntimeFormStockSnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(ailments: ailments),
            new RuntimeBattleActivationSnapshot(),
            [new KeyValuePair<ContentId, decimal>(Id("hp"), 40)]);

    internal static RuntimeActorReferenceSnapshot Reference(RuntimeActorSnapshot actor) =>
        new(actor.Identity.InstanceId, actor.Identity.EntityDefinitionId, actor.Identity.DisplayName);

    internal static GameDataCatalog LoadCatalog()
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

        CatalogLoadResult result = new SkillSystemCatalogLoader().Load(new SkillSystemCatalogLoadRequest(
            Registrations(),
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

    private static SkillSystemRegistrationSnapshot Registrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEntityKind("demon")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterAilmentGroup("poison")
            .RegisterBattleKind("normal_battle")
            .RegisterMoonPhase("new_moon")
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
            foreach (Type nested in Expand(element)) yield return nested;
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Expand(argument)) yield return nested;
        }
    }

    private sealed class EmptyParameterValidator : IContentParameterValidator
    {
        public static EmptyParameterValidator Instance { get; } = new();

        public IReadOnlyList<ContentParameterValidationIssue> Validate(IReadOnlyDictionary<string, object?> parameters) => [];
    }
}
