using Convergence.Catalog;
using Convergence.Content;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class EquipmentInstanceOwnershipTests
{
    private static readonly ContentId Shortsword =
        Id("convergence.catalog_surface_sample:shortsword_sample");
    private static readonly RuntimeInstanceId FirstSword = Instance("shortsword-001");
    private static readonly RuntimeInstanceId SecondSword = Instance("shortsword-002");

    [Fact]
    public void Inventory_UsesUniqueInstanceIdsWhileAllowingDuplicateDefinitions()
    {
        var first = new RuntimeEquipmentInstanceSnapshot(FirstSword, Shortsword);
        var second = new RuntimeEquipmentInstanceSnapshot(SecondSword, Shortsword);
        var inventory = new RuntimeInventorySnapshot(
            ownedEquipmentInstances:
            [
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    StandardEquipmentSlotIds.Weapon,
                    [first, second])
            ]);

        Assert.Equal(2, inventory.GetEquipmentInstances(StandardEquipmentSlotIds.Weapon).Count);
        Assert.True(inventory.TryGetEquipmentInstance(
            FirstSword,
            out RuntimeEquipmentInstanceSnapshot? resolved,
            out ContentId slot));
        Assert.Equal(Shortsword, resolved!.DefinitionId);
        Assert.Equal(StandardEquipmentSlotIds.Weapon, slot);
        Assert.Throws<ArgumentException>(() => new RuntimeInventorySnapshot(
            ownedEquipmentInstances:
            [
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    StandardEquipmentSlotIds.Weapon,
                    [first]),
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    StandardEquipmentSlotIds.Accessory,
                    [first])
            ]));
    }

    [Fact]
    public void EquipmentTransition_RejectsMissingAndMultiplyEquippedInstancesAtomically()
    {
        RuntimeInventorySnapshot inventory = Inventory(FirstSword, SecondSword);
        var empty = new RuntimeEquipmentSnapshot();
        var firstActor = new RuntimeEquipmentSnapshot(
        [
            new KeyValuePair<ContentId, RuntimeInstanceId>(
                StandardEquipmentSlotIds.Weapon,
                FirstSword)
        ]);
        var service = new EquipmentTransitionService();

        EquipmentTransitionResult missing = service.Equip(
            inventory,
            empty,
            Instance("missing-001"),
            StandardEquipmentSlotIds.Weapon,
            StandardEquipmentSlotIds.Weapon,
            [firstActor]);
        EquipmentTransitionResult multiplyEquipped = service.Equip(
            inventory,
            empty,
            FirstSword,
            StandardEquipmentSlotIds.Weapon,
            StandardEquipmentSlotIds.Weapon,
            [firstActor]);

        Assert.Equal(ResourceTransactionCode.EquipmentNotOwned, missing.Code);
        Assert.Same(empty, missing.Before);
        Assert.Same(empty, missing.After);
        Assert.Equal(ResourceTransactionCode.EquipmentAlreadyEquipped, multiplyEquipped.Code);
        Assert.Same(empty, multiplyEquipped.Before);
        Assert.Same(empty, multiplyEquipped.After);
    }

    [Fact]
    public void CustomSlotLayout_PreservesCrossActorOwnershipAndDrivesSaveCompatibility()
    {
        ContentId mainHand = Id("main_hand");
        RuntimeInventorySnapshot inventory = Inventory(FirstSword);
        var empty = new RuntimeEquipmentSnapshot();
        var occupiedByOtherActor = new RuntimeEquipmentSnapshot(
        [
            new KeyValuePair<ContentId, RuntimeInstanceId>(mainHand, FirstSword)
        ]);
        var layout = new MainHandEquipmentSlotLayoutPolicy(mainHand);
        var service = new EquipmentTransitionService(layout);

        EquipmentTransitionResult collision = service.Equip(
            inventory,
            empty,
            FirstSword,
            StandardEquipmentSlotIds.Weapon,
            mainHand,
            [occupiedByOtherActor]);
        EquipmentTransitionResult applied = service.Equip(
            inventory,
            empty,
            FirstSword,
            StandardEquipmentSlotIds.Weapon,
            mainHand,
            []);

        Assert.Equal(ResourceTransactionCode.EquipmentAlreadyEquipped, collision.Code);
        Assert.Same(empty, collision.Before);
        Assert.Same(empty, collision.After);
        Assert.True(applied.Applied);
        Assert.Equal(FirstSword, applied.After.EquippedInstanceIds[mainHand]);

        RuntimeSaveGameSnapshot baseline = RuntimePersistenceSnapshotTests.CreateSaveSnapshot();
        RuntimeActorSnapshot customActor = WithEquipment(baseline.Actors[0], FirstSword, mainHand);
        RuntimeSaveGameSnapshot snapshot = CopySave(
            baseline,
            [customActor, baseline.Actors[1]],
            inventory);
        GameDataCatalog catalog = RuntimePersistenceSnapshotTests.LoadCatalog();

        RuntimeSaveValidationResult standardValidation =
            new RuntimeSaveValidator().Validate(snapshot, catalog);
        RuntimeSaveValidationResult customValidation =
            new RuntimeSaveValidator(equipmentSlotLayout: layout).Validate(snapshot, catalog);

        Assert.Contains(standardValidation.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.EquipmentSlotMismatch &&
            diagnostic.Path == "$.actors[0].equipment.equippedInstanceIds.main_hand");
        Assert.True(
            customValidation.IsValid,
            string.Join(Environment.NewLine, customValidation.Diagnostics.Select(item => item.Message)));
    }

    [Fact]
    public void InventoryTransition_RejectsDuplicateAndEquippedRemovalAtomically()
    {
        RuntimeInventorySnapshot inventory = Inventory(FirstSword);
        var service = new InventoryTransitionService();
        var duplicate = new RuntimeEquipmentInstanceSnapshot(FirstSword, Shortsword);
        var equipped = new RuntimeEquipmentSnapshot(
        [
            new KeyValuePair<ContentId, RuntimeInstanceId>(
                StandardEquipmentSlotIds.Weapon,
                FirstSword)
        ]);

        InventoryTransitionResult duplicateResult = service.AddEquipment(
            inventory,
            duplicate,
            StandardEquipmentSlotIds.Weapon);
        InventoryTransitionResult removeResult = service.RemoveEquipment(
            inventory,
            FirstSword,
            StandardEquipmentSlotIds.Weapon,
            [equipped]);

        Assert.Equal(ResourceTransactionCode.EquipmentDuplicate, duplicateResult.Code);
        Assert.Same(inventory, duplicateResult.Before);
        Assert.Same(inventory, duplicateResult.After);
        Assert.Equal(ResourceTransactionCode.EquippedItemCannotBeRemoved, removeResult.Code);
        Assert.Same(inventory, removeResult.Before);
        Assert.Same(inventory, removeResult.After);
    }

    [Fact]
    public void SaveValidation_AllowsSeparateCopiesOfOneDefinitionOnDifferentActors()
    {
        RuntimeSaveGameSnapshot baseline =
            RuntimePersistenceSnapshotTests.CreateSaveSnapshot();
        RuntimeActorSnapshot frost = WithEquipment(
            baseline.Actors[0],
            FirstSword);
        RuntimeActorSnapshot ember = WithEquipment(
            baseline.Actors[1],
            SecondSword);
        RuntimeSaveGameSnapshot snapshot = CopySave(
            baseline,
            [frost, ember],
            Inventory(FirstSword, SecondSword));

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            snapshot,
            RuntimePersistenceSnapshotTests.LoadCatalog());

        Assert.True(
            validation.IsValid,
            string.Join(Environment.NewLine, validation.Diagnostics.Select(item => item.Message)));
    }

    [Fact]
    public void SaveValidation_RejectsMissingAndMultiplyEquippedInstances()
    {
        RuntimeSaveGameSnapshot baseline =
            RuntimePersistenceSnapshotTests.CreateSaveSnapshot();
        RuntimeActorSnapshot frost = WithEquipment(
            baseline.Actors[0],
            FirstSword);
        RuntimeActorSnapshot ember = WithEquipment(
            baseline.Actors[1],
            FirstSword);
        RuntimeSaveGameSnapshot multiplyEquipped = CopySave(
            baseline,
            [frost, ember],
            Inventory(FirstSword));
        RuntimeActorSnapshot missingActor = WithEquipment(
            baseline.Actors[0],
            Instance("missing-001"));
        RuntimeSaveGameSnapshot missing = CopySave(
            baseline,
            [missingActor, baseline.Actors[1]],
            Inventory(FirstSword));
        GameDataCatalog catalog = RuntimePersistenceSnapshotTests.LoadCatalog();

        RuntimeSaveValidationResult multiplyValidation =
            new RuntimeSaveValidator().Validate(multiplyEquipped, catalog);
        RuntimeSaveValidationResult missingValidation =
            new RuntimeSaveValidator().Validate(missing, catalog);

        Assert.Contains(multiplyValidation.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.EquipmentAssignedToMultipleActors &&
            diagnostic.Path == "$.actors[1].equipment.equippedInstanceIds.weapon");
        Assert.Contains(missingValidation.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.EquippedEquipmentNotOwned &&
            diagnostic.Path == "$.actors[0].equipment.equippedInstanceIds.weapon");
    }

    [Fact]
    public void SaveValidation_RejectsEquipmentInstanceIdThatCollidesWithActorIdentity()
    {
        RuntimeSaveGameSnapshot baseline =
            RuntimePersistenceSnapshotTests.CreateSaveSnapshot();
        RuntimeInstanceId actorInstanceId = baseline.Actors[0].Identity.InstanceId;
        RuntimeSaveGameSnapshot snapshot = CopySave(
            baseline,
            baseline.Actors,
            Inventory(actorInstanceId));

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            snapshot,
            RuntimePersistenceSnapshotTests.LoadCatalog());

        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code ==
                RuntimeSaveValidationCode.EquipmentInstanceIdCollidesWithActor &&
            diagnostic.InstanceId == actorInstanceId &&
            diagnostic.Path ==
                "$.inventory.ownedEquipmentInstances.weapon[0].instanceId");
    }

    [Fact]
    public void SaveContract_HasNoSeparateRootEquipmentAuthority()
    {
        Assert.Equal(18, RuntimeSaveGameSnapshot.CurrentContractVersion);
        Assert.Null(typeof(RuntimeSaveGameSnapshot).GetProperty("Equipment"));
        Assert.DoesNotContain(
            typeof(RuntimeSaveGameSnapshot).GetConstructors()
                .SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(RuntimeEquipmentSnapshot));
        Assert.Null(typeof(RuntimeRestoredSession).GetProperty("Equipment"));
    }

    private static RuntimeInventorySnapshot Inventory(
        params RuntimeInstanceId[] instanceIds) =>
        new(
            ownedEquipmentInstances:
            [
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    StandardEquipmentSlotIds.Weapon,
                    instanceIds.Select(instanceId =>
                        new RuntimeEquipmentInstanceSnapshot(instanceId, Shortsword)))
            ]);

    private static RuntimeActorSnapshot WithEquipment(
        RuntimeActorSnapshot actor,
        RuntimeInstanceId equipmentInstanceId,
        ContentId? slotId = null) =>
        new(
            actor.Identity,
            actor.Affiliation,
            actor.EncounterPresence,
            actor.Progression,
            actor.Resources,
            actor.Stats,
            actor.Skills,
            new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<ContentId, RuntimeInstanceId>(
                    slotId ?? StandardEquipmentSlotIds.Weapon,
                    equipmentInstanceId)
            ]),
            actor.BattleStatus,
            actor.BattleActivations,
            actor.BaseResourceValues,
            actor.VitalResourceId,
            actor.CapabilityIds,
            actor.CombatProfileIdentity);

    private static RuntimeSaveGameSnapshot CopySave(
        RuntimeSaveGameSnapshot source,
        IEnumerable<RuntimeActorSnapshot> actors,
        RuntimeInventorySnapshot inventory) =>
        new(
            source.FrameworkVersion,
            source.ContentPacks,
            actors,
            source.PartyRoster,
            inventory,
            source.CurrencyLedger,
            source.Field,
            source.Compendium,
            source.Knowledge,
            source.Session,
            source.Checkpoints,
            source.HostContext,
            source.ContractVersion);

    private static ContentId Id(string value) => ContentId.Parse(value);
    private static RuntimeInstanceId Instance(string value) => RuntimeInstanceId.Parse(value);

    private sealed class MainHandEquipmentSlotLayoutPolicy(ContentId mainHand)
        : IEquipmentSlotLayoutPolicy
    {
        private readonly IReadOnlyList<ContentId> _slotIds =
            Array.AsReadOnly([StandardEquipmentSlotIds.Weapon, mainHand]);

        public IReadOnlyList<ContentId> SlotIds => _slotIds;

        public EquipmentSlotLayoutResult ValidateDefinition(EquipmentDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            return definition.SlotId == StandardEquipmentSlotIds.Weapon &&
                   definition.Weapon is not null &&
                   definition.Armor is null &&
                   definition.Boots is null &&
                   definition.Accessory is null
                ? EquipmentSlotLayoutResult.Compatible
                : new EquipmentSlotLayoutResult(
                    EquipmentSlotLayoutCode.ProfileMismatch,
                    "The main-hand layout accepts weapon-profile definitions only.");
        }

        public EquipmentSlotLayoutResult ValidateAssignment(
            ContentId authoredSlotId,
            ContentId targetSlotId) =>
            authoredSlotId == StandardEquipmentSlotIds.Weapon &&
            (targetSlotId == StandardEquipmentSlotIds.Weapon || targetSlotId == mainHand)
                ? EquipmentSlotLayoutResult.Compatible
                : new EquipmentSlotLayoutResult(
                    EquipmentSlotLayoutCode.AssignmentMismatch,
                    "The authored weapon slot maps to main_hand only.");
    }
}
