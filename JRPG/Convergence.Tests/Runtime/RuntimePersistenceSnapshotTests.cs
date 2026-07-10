using System.Reflection;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Execution;
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
        RuntimeActorSnapshot restored = RuntimeActorState.Restore(
            valid.Actors[0],
            CombatDefenseProfile.Empty).ToSnapshot();
        Assert.Equal(Id("convergence.clean_battle_demo:frost_duelist_demo"), restored.Identity.EntityDefinitionId);
        Assert.Equal(Id("convergence.shared_effects_demo:medicine_demo"), valid.Inventory.ItemQuantities.Keys.Single());
        Assert.Equal(
            Id("convergence.catalog_surface_sample:tartarus_sample"),
            valid.Field!.DungeonTraversal!.DungeonId);
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
            ailments: [new RuntimeTimedStateSnapshot(
                Id("missing.pack:missing_ailment"),
                new TurnDurationDefinition(1, Id("owner_turn_end"), false))]);
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
                new RuntimeNavigationSnapshot(Id("missing_location")),
                new RuntimeDungeonTraversalSnapshot(
                    Id("missing.pack:missing_dungeon"),
                    Id("missing_node"))),
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
    public void RuntimeSaveValidator_RejectsPartyStockStructuralInvariantViolations()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeActorSnapshot frost = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot ember = CreateActor(
            RuntimeInstanceId.Parse("ember"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorSnapshot ward = CreateActor(
            RuntimeInstanceId.Parse("ward"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorReferenceSnapshot frostRef = Reference(frost);
        RuntimeActorReferenceSnapshot emberRef = Reference(ember);
        RuntimeActorReferenceSnapshot wardRef = Reference(ward);
        RuntimePartyStockSnapshot invalidParty = new(
            frostRef,
            ownerLevel: 1,
            activeParty: [frostRef, emberRef, frostRef],
            reserveMembers: [emberRef],
            activeForm: frostRef,
            personaStock: [frostRef, emberRef, emberRef],
            demonStock: [frostRef, emberRef, wardRef, wardRef],
            maxActivePartySize: 2);
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            actors: [frost, ember, ward],
            partyStock: invalidParty);

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ActivePartyCapacityExceeded &&
            diagnostic.Path == "$.partyStock.activeParty");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DemonStockCapacityExceeded &&
            diagnostic.Path == "$.partyStock.demonStock");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicatePartyStockReference &&
            diagnostic.Path == "$.partyStock.activeParty[2]" &&
            diagnostic.InstanceId == frostRef.InstanceId);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicatePartyStockReference &&
            diagnostic.Path == "$.partyStock.reserveMembers[0]" &&
            diagnostic.InstanceId == emberRef.InstanceId);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ActiveFormDuplicatedInPersonaStock &&
            diagnostic.Path == "$.partyStock.personaStock[0]" &&
            diagnostic.InstanceId == frostRef.InstanceId);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicatePartyStockReference &&
            diagnostic.Path == "$.partyStock.personaStock[2]" &&
            diagnostic.InstanceId == emberRef.InstanceId);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicatePartyStockReference &&
            diagnostic.Path == "$.partyStock.demonStock[3]" &&
            diagnostic.InstanceId == wardRef.InstanceId);
    }

    [Fact]
    public void RuntimeSaveValidator_AllowsIntentionalActiveDemonOwnedStockOverlap()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot();

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Contains(
            snapshot.PartyStock.DemonStock,
            actor => actor.InstanceId == snapshot.PartyStock.ActiveParty[0].InstanceId);
    }

    [Fact]
    public void RuntimeSaveValidator_UsesInjectedStockCapacityPolicy()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeActorSnapshot frost = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot ember = CreateActor(
            RuntimeInstanceId.Parse("ember"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorSnapshot ward = CreateActor(
            RuntimeInstanceId.Parse("ward"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot veil = CreateActor(
            RuntimeInstanceId.Parse("veil"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorReferenceSnapshot frostRef = Reference(frost);
        RuntimeActorReferenceSnapshot emberRef = Reference(ember);
        RuntimeActorReferenceSnapshot wardRef = Reference(ward);
        RuntimeActorReferenceSnapshot veilRef = Reference(veil);
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            actors: [frost, ember, ward, veil],
            partyStock: new RuntimePartyStockSnapshot(
                frostRef,
                ownerLevel: 1,
                activeParty: [frostRef],
                demonStock: [frostRef, emberRef, wardRef, veilRef]));

        RuntimeSaveValidationResult defaultResult = new RuntimeSaveValidator().Validate(snapshot, catalog);
        RuntimeSaveValidationResult customResult = new RuntimeSaveValidator(new FixedStockCapacityPolicy(4))
            .Validate(snapshot, catalog);

        Assert.Contains(defaultResult.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DemonStockCapacityExceeded);
        Assert.True(customResult.IsValid, string.Join(Environment.NewLine, customResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsMissingDuplicateAndVersionMismatchedContentPacks()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot snapshot = Copy(
            CreateSaveSnapshot(),
            contentPacks:
            [
                new ContentPackIdentity("convergence.skill_system_redesign_sample", SemanticVersion.Parse("0.1.0")),
                new ContentPackIdentity("convergence.clean_battle_demo", SemanticVersion.Parse("9.9.9")),
                new ContentPackIdentity("convergence.clean_battle_demo", SemanticVersion.Parse("0.1.0")),
                new ContentPackIdentity("missing.pack", SemanticVersion.Parse("0.1.0"))
            ]);

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ContentPackVersionMismatch &&
            diagnostic.Path == "$.contentPacks[1].version");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicateContentPack &&
            diagnostic.Path == "$.contentPacks[2].id");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.MissingContentPack &&
            diagnostic.Path == "$.contentPacks[3].id");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.MissingContentPack &&
            diagnostic.Path == "$.contentPacks");
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsUnsupportedContractVersion()
    {
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            contractVersion: RuntimeSaveGameSnapshot.CurrentContractVersion + 1);
        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, LoadCatalog());

        RuntimeSaveValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(RuntimeSaveValidationCode.ContractVersionUnsupported, diagnostic.Code);
        Assert.Equal("$.contractVersion", diagnostic.Path);
    }

    [Fact]
    public void RuntimeSaveSnapshot_AllowsNavigationAndDungeonModulesToBeOmittedIndependently()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot noField = CreateSaveSnapshot(includeDefaultField: false);
        RuntimeSaveGameSnapshot navigationOnly = CreateSaveSnapshot(
            field: new RuntimeFieldSnapshot(new RuntimeNavigationSnapshot(Id("host_owned_location"))));

        RuntimeSaveValidationResult noFieldResult = new RuntimeSaveValidator().Validate(noField, catalog);
        RuntimeSaveValidationResult navigationOnlyResult =
            new RuntimeSaveValidator().Validate(navigationOnly, catalog);

        Assert.True(noFieldResult.IsValid);
        Assert.Null(noField.Field);
        Assert.True(navigationOnlyResult.IsValid);
        Assert.Equal(Id("host_owned_location"), navigationOnly.Field!.Navigation.CurrentLocationId);
        Assert.Null(navigationOnly.Field.DungeonTraversal);
    }

    [Fact]
    public void RuntimeSavePolicy_AllowsManualAndSuspendOnlyInRegisteredStableContexts()
    {
        var service = new RuntimeSavePolicyService(new RuntimeSavePolicyOptions(
            manualAllowedContextIds: [Id("field_menu"), Id("dungeon_menu")],
            suspendAllowedContextIds: [Id("field_menu"), Id("dungeon_menu")]));

        RuntimeSavePolicyAssessment manual = service.AssessSave(
            RuntimeSaveKind.Manual,
            new RuntimeSaveContextSnapshot(Id("field_menu")));
        RuntimeSavePolicyAssessment suspend = service.AssessSave(
            RuntimeSaveKind.Suspend,
            new RuntimeSaveContextSnapshot(Id("dungeon_menu")));
        RuntimeSavePolicyAssessment battle = service.AssessSave(
            RuntimeSaveKind.Manual,
            new RuntimeSaveContextSnapshot(Id("battle")));
        RuntimeSavePolicyAssessment pending = service.AssessSave(
            RuntimeSaveKind.Suspend,
            new RuntimeSaveContextSnapshot(Id("field_menu"), hasPendingHostAction: true));

        Assert.True(manual.IsAllowed);
        Assert.True(suspend.IsAllowed);
        Assert.False(battle.IsAllowed);
        Assert.Contains(battle.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.ContextNotAllowed &&
            diagnostic.ContextId == Id("battle"));
        Assert.False(pending.IsAllowed);
        Assert.Contains(pending.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.PendingHostAction);
    }

    [Fact]
    public void RuntimeSavePolicy_AssessesLoadRecordsAndSuspendConsumption()
    {
        var service = new RuntimeSavePolicyService(new RuntimeSavePolicyOptions(
            manualAllowedContextIds: [Id("field_menu")],
            suspendAllowedContextIds: [Id("field_menu")]));
        var context = new RuntimeSaveContextSnapshot(Id("field_menu"));
        RuntimeSaveRecord manual = new(RuntimeSaveKind.Manual, CreateSaveSnapshot(), context, sequence: 3);
        RuntimeSaveRecord suspend = new(RuntimeSaveKind.Suspend, CreateSaveSnapshot(), context, sequence: 4);

        RuntimeSavePolicyAssessment missing = service.AssessLoad(null, RuntimeSaveKind.Manual, context);
        RuntimeSavePolicyAssessment mismatch = service.AssessLoad(manual, RuntimeSaveKind.Suspend, context);
        RuntimeSavePolicyAssessment suspendLoad = service.AssessLoad(suspend, RuntimeSaveKind.Suspend, context);
        RuntimeSavePolicyAssessment manualLoad = service.AssessLoad(manual, RuntimeSaveKind.Manual, context);
        RuntimeSavePolicyAssessment savedContextMismatch = service.AssessLoad(
            new RuntimeSaveRecord(
                RuntimeSaveKind.Manual,
                CreateSaveSnapshot(),
                new RuntimeSaveContextSnapshot(Id("battle"))),
            RuntimeSaveKind.Manual,
            context);
        RuntimeSavePolicyAssessment savedPending = service.AssessLoad(
            new RuntimeSaveRecord(
                RuntimeSaveKind.Manual,
                CreateSaveSnapshot(),
                new RuntimeSaveContextSnapshot(Id("field_menu"), hasPendingHostAction: true)),
            RuntimeSaveKind.Manual,
            context);

        Assert.False(missing.IsAllowed);
        Assert.Contains(missing.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.MissingSaveRecord);
        Assert.False(mismatch.IsAllowed);
        Assert.Contains(mismatch.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.SaveKindMismatch);
        Assert.True(suspendLoad.IsAllowed);
        Assert.True(suspendLoad.ConsumeAfterSuccessfulRestore);
        Assert.True(manualLoad.IsAllowed);
        Assert.False(manualLoad.ConsumeAfterSuccessfulRestore);
        Assert.False(savedContextMismatch.IsAllowed);
        Assert.Contains(savedContextMismatch.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.SavedContextNotAllowed &&
            diagnostic.ContextId == Id("battle"));
        Assert.False(savedPending.IsAllowed);
        Assert.Contains(savedPending.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.SavedContextPendingHostAction);
    }

    [Fact]
    public void RuntimeSavePolicy_DefensivelyCopiesOptionsAndRecordsMetadata()
    {
        List<ContentId> manualContexts = [Id("field_menu")];
        List<ContentId> suspendContexts = [Id("dungeon_menu")];
        RuntimeSavePolicyOptions options = new(manualContexts, suspendContexts);
        RuntimeSaveRecord record = new(
            RuntimeSaveKind.Manual,
            CreateSaveSnapshot(),
            new RuntimeSaveContextSnapshot(Id("field_menu")),
            sequence: 7);
        manualContexts.Add(Id("battle"));
        suspendContexts.Clear();

        Assert.Equal([Id("field_menu")], options.ManualAllowedContextIds);
        Assert.Equal([Id("dungeon_menu")], options.SuspendAllowedContextIds);
        Assert.Equal(RuntimeSaveKind.Manual, record.Kind);
        Assert.Equal(Id("field_menu"), record.Context.ContextId);
        Assert.Equal(7, record.Sequence);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuntimeSaveRecord(
                RuntimeSaveKind.Manual,
                CreateSaveSnapshot(),
                new RuntimeSaveContextSnapshot(Id("field_menu")),
                sequence: -1));
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
        int contractVersion = RuntimeSaveGameSnapshot.CurrentContractVersion,
        bool includeDefaultField = true)
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
            [
                new ContentPackIdentity("convergence.skill_system_redesign_sample", SemanticVersion.Parse("0.1.0")),
                new ContentPackIdentity("convergence.clean_battle_demo", SemanticVersion.Parse("0.1.0")),
                new ContentPackIdentity("convergence.shared_effects_demo", SemanticVersion.Parse("0.1.0")),
                new ContentPackIdentity("convergence.catalog_surface_sample", SemanticVersion.Parse("0.1.0"))
            ],
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
            field ?? (includeDefaultField
                ? new RuntimeFieldSnapshot(
                    new RuntimeNavigationSnapshot(Id("convergence.catalog_surface_sample:tartarus_floor_5")),
                    new RuntimeDungeonTraversalSnapshot(
                        Id("convergence.catalog_surface_sample:tartarus_sample"),
                        Id("convergence.catalog_surface_sample:floor_5"),
                        visitedNodeIds:
                        [
                            Id("convergence.catalog_surface_sample:floor_1"),
                            Id("convergence.catalog_surface_sample:floor_5")
                        ],
                        unlockedCheckpointIds:
                        [
                            Id("convergence.catalog_surface_sample:terminal_1"),
                            Id("convergence.catalog_surface_sample:terminal_5")
                        ],
                        defeatedBossIds: [Id("convergence.catalog_surface_sample:thebel_training_sample")]))
                : null),
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

    private static RuntimeSaveGameSnapshot Copy(
        RuntimeSaveGameSnapshot snapshot,
        IEnumerable<ContentPackIdentity>? contentPacks = null) =>
        new(
            snapshot.FrameworkVersion,
            contentPacks ?? snapshot.ContentPacks,
            snapshot.Actors,
            snapshot.PartyStock,
            snapshot.Inventory,
            snapshot.Equipment,
            snapshot.Wallet,
            snapshot.Field,
            snapshot.Compendium,
            snapshot.Knowledge,
            snapshot.Session,
            snapshot.Checkpoints,
            snapshot.HostContext,
            snapshot.ContractVersion);

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
            [new KeyValuePair<ContentId, decimal>(Id("hp"), 40)],
            Id("hp"));

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

    private sealed class FixedStockCapacityPolicy(int capacity) : IStockCapacityPolicy
    {
        public int GetCapacity(int ownerLevel) => capacity;
    }
}
