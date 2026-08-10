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
                new KeyValuePair<EquipmentSlot, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    EquipmentSlot.Weapon,
                    [first, second])
            ]);

        Assert.Equal(2, inventory.GetEquipmentInstances(EquipmentSlot.Weapon).Count);
        Assert.True(inventory.TryGetEquipmentInstance(
            FirstSword,
            out RuntimeEquipmentInstanceSnapshot? resolved,
            out EquipmentSlot slot));
        Assert.Equal(Shortsword, resolved!.DefinitionId);
        Assert.Equal(EquipmentSlot.Weapon, slot);
        Assert.Throws<ArgumentException>(() => new RuntimeInventorySnapshot(
            ownedEquipmentInstances:
            [
                new KeyValuePair<EquipmentSlot, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    EquipmentSlot.Weapon,
                    [first]),
                new KeyValuePair<EquipmentSlot, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    EquipmentSlot.Accessory,
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
            new KeyValuePair<EquipmentSlot, RuntimeInstanceId>(
                EquipmentSlot.Weapon,
                FirstSword)
        ]);
        var service = new EquipmentTransitionService();

        EquipmentTransitionResult missing = service.Equip(
            inventory,
            empty,
            Instance("missing-001"),
            EquipmentSlot.Weapon,
            EquipmentSlot.Weapon,
            [firstActor]);
        EquipmentTransitionResult multiplyEquipped = service.Equip(
            inventory,
            empty,
            FirstSword,
            EquipmentSlot.Weapon,
            EquipmentSlot.Weapon,
            [firstActor]);

        Assert.Equal(ResourceTransactionCode.EquipmentNotOwned, missing.Code);
        Assert.Same(empty, missing.Before);
        Assert.Same(empty, missing.After);
        Assert.Equal(ResourceTransactionCode.EquipmentAlreadyEquipped, multiplyEquipped.Code);
        Assert.Same(empty, multiplyEquipped.Before);
        Assert.Same(empty, multiplyEquipped.After);
    }

    [Fact]
    public void InventoryTransition_RejectsDuplicateAndEquippedRemovalAtomically()
    {
        RuntimeInventorySnapshot inventory = Inventory(FirstSword);
        var service = new InventoryTransitionService();
        var duplicate = new RuntimeEquipmentInstanceSnapshot(FirstSword, Shortsword);
        var equipped = new RuntimeEquipmentSnapshot(
        [
            new KeyValuePair<EquipmentSlot, RuntimeInstanceId>(
                EquipmentSlot.Weapon,
                FirstSword)
        ]);

        InventoryTransitionResult duplicateResult = service.AddEquipment(
            inventory,
            duplicate,
            EquipmentSlot.Weapon);
        InventoryTransitionResult removeResult = service.RemoveEquipment(
            inventory,
            FirstSword,
            EquipmentSlot.Weapon,
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
        Assert.Equal(16, RuntimeSaveGameSnapshot.CurrentContractVersion);
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
                new KeyValuePair<EquipmentSlot, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    EquipmentSlot.Weapon,
                    instanceIds.Select(instanceId =>
                        new RuntimeEquipmentInstanceSnapshot(instanceId, Shortsword)))
            ]);

    private static RuntimeActorSnapshot WithEquipment(
        RuntimeActorSnapshot actor,
        RuntimeInstanceId equipmentInstanceId) =>
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
                new KeyValuePair<EquipmentSlot, RuntimeInstanceId>(
                    EquipmentSlot.Weapon,
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
            source.Wallet,
            source.Field,
            source.Compendium,
            source.Knowledge,
            source.Session,
            source.Checkpoints,
            source.HostContext,
            source.ContractVersion);

    private static ContentId Id(string value) => ContentId.Parse(value);
    private static RuntimeInstanceId Instance(string value) => RuntimeInstanceId.Parse(value);
}
