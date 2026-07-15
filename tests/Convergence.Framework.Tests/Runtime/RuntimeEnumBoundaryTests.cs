using System.Reflection;
using Convergence.Battle;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Knowledge;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimeEnumBoundaryTests
{
    private const int UndefinedValue = 999;

    [Fact]
    public void ActorSnapshotConstructors_RejectUndefinedEnumValues()
    {
        var duration = new PermanentDurationDefinition();

        AssertUndefined("Deployment", () => new RuntimeActorDeploymentSnapshot(
            Undefined<RuntimeActorDeployment>(),
            IsActive: true));
        AssertUndefined("equippedItemIds", () => new RuntimeEquipmentSnapshot(
        [
            new KeyValuePair<EquipmentSlot, ContentId>(Undefined<EquipmentSlot>(), Id("equipment"))
        ]));
        AssertUndefined("kind", () => new RuntimeChargeSnapshot(
            Undefined<ChargeKind>(),
            2m,
            duration));
        AssertUndefined("kind", () => new RuntimeShieldSnapshot(
            Undefined<ShieldKind>(),
            duration));
        AssertUndefined("element", () => new RuntimeAffinityBreakSnapshot(
            Undefined<DamageElement>(),
            duration));
        AssertUndefined("element", () => new RuntimeAffinityOverrideSnapshot(
            Undefined<DamageElement>(),
            ElementalAffinity.Normal,
            duration));
        AssertUndefined("affinity", () => new RuntimeAffinityOverrideSnapshot(
            DamageElement.Fire,
            Undefined<ElementalAffinity>(),
            duration));
        AssertUndefined("layers", () => new RuntimeAnalysisSnapshot(
            RuntimeInstanceId.Parse("target"),
            [Undefined<AnalysisLayer>()]));
    }

    [Fact]
    public void PersistedSnapshotConstructors_RejectUndefinedEnumValues()
    {
        ContentId entityId = Id("test.pack:entity");
        ContentId ailmentId = Id("test.pack:ailment");

        AssertUndefined("ownedEquipmentIds", () => new RuntimeInventorySnapshot(
            ownedEquipmentIds:
            [
                new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(
                    Undefined<EquipmentSlot>(),
                    [Id("test.pack:equipment")])
            ]));
        AssertUndefined("Element", () => new RuntimeElementalAffinityKnowledgeSnapshot(
            entityId,
            Undefined<DamageElement>(),
            ElementalAffinity.Normal));
        AssertUndefined("Affinity", () => new RuntimeElementalAffinityKnowledgeSnapshot(
            entityId,
            DamageElement.Fire,
            Undefined<ElementalAffinity>()));
        AssertUndefined("Resistance", () => new RuntimeAilmentResistanceKnowledgeSnapshot(
            entityId,
            ailmentId,
            Undefined<ResistanceLevel>()));
        AssertUndefined("Channel", () => new RuntimeInstantDeathResistanceKnowledgeSnapshot(
            entityId,
            Undefined<InstantDeathChannel>(),
            ResistanceLevel.Normal));
        AssertUndefined("Resistance", () => new RuntimeInstantDeathResistanceKnowledgeSnapshot(
            entityId,
            InstantDeathChannel.Light,
            Undefined<ResistanceLevel>()));
        AssertUndefined("kind", () => new RuntimeCheckpointEntrySnapshot(
            0,
            Undefined<RuntimeCheckpointKind>(),
            "Malformed checkpoint."));
    }

    [Fact]
    public void ActorMutations_RejectUndefinedEnumValuesBeforeMutation()
    {
        RuntimeActorState actor = RuntimeActorState.Restore(
            RuntimePersistenceSnapshotTests.CreateActor(
                RuntimeInstanceId.Parse("actor"),
                Id("convergence.clean_battle_demo:frost_duelist_demo")),
            CombatDefenseProfile.Empty);
        var duration = new PermanentDurationDefinition();
        RuntimeActorDeploymentSnapshot malformedDeployment = actor.Deployment with
        {
            Deployment = Undefined<RuntimeActorDeployment>()
        };

        AssertUndefined("deployment", () => new RuntimeActorState(
            RuntimeInstanceId.Parse("other_actor"),
            Id("test.pack:other_actor"),
            Id("team"),
            Id("hp"),
            CombatDefenseProfile.Empty,
            [new BattleResourceState(Id("hp"), 10m, 10m)],
            deployment: malformedDeployment));
        AssertUndefined("deployment", () => actor.SetDeployment(
            Undefined<RuntimeActorDeployment>(),
            isActive: false));

        AssertUndefined("kind", () => actor.GrantCharge(Undefined<ChargeKind>(), 2m, duration));
        AssertUndefined("kind", () => actor.GrantShield(Undefined<ShieldKind>(), duration));
        AssertUndefined("element", () => actor.BreakAffinity(Undefined<DamageElement>(), duration));
        AssertUndefined("element", () => actor.OverrideAffinity(
            Undefined<DamageElement>(),
            ElementalAffinity.Normal,
            duration));
        AssertUndefined("affinity", () => actor.OverrideAffinity(
            DamageElement.Fire,
            Undefined<ElementalAffinity>(),
            duration));
        AssertUndefined("layers", () => actor.Reveal(
            RuntimeInstanceId.Parse("target"),
            [Undefined<AnalysisLayer>()]));

        RuntimeBattleStatusSnapshot status = actor.ToSnapshot().BattleStatus;
        Assert.True(actor.IsActive);
        Assert.Equal(RuntimeActorDeployment.Deployed, actor.Deployment.Deployment);
        Assert.Empty(status.Charges);
        Assert.Empty(status.Shields);
        Assert.Empty(status.AffinityBreaks);
        Assert.Empty(status.AffinityOverrides);
        Assert.Empty(status.Analysis);
    }

    [Fact]
    public void KnowledgeStores_RejectUndefinedEnumValuesBeforeMutation()
    {
        ContentId entityId = Id("test.pack:entity");
        ContentId ailmentId = Id("test.pack:ailment");
        var affinities = new ElementalAffinityKnowledge();
        var ailments = new AilmentResistanceKnowledge();
        var instantDeath = new InstantDeathResistanceKnowledge();

        AssertUndefined("element", () => affinities.Learn(
            entityId,
            Undefined<DamageElement>(),
            ElementalAffinity.Normal));
        AssertUndefined("affinity", () => affinities.Learn(
            entityId,
            DamageElement.Fire,
            Undefined<ElementalAffinity>()));
        AssertUndefined("resistance", () => ailments.Learn(
            entityId,
            ailmentId,
            Undefined<ResistanceLevel>()));
        AssertUndefined("channel", () => instantDeath.Learn(
            entityId,
            Undefined<InstantDeathChannel>(),
            ResistanceLevel.Normal));
        AssertUndefined("resistance", () => instantDeath.Learn(
            entityId,
            InstantDeathChannel.Light,
            Undefined<ResistanceLevel>()));

        Assert.Empty(affinities.Snapshot());
        Assert.Empty(ailments.Snapshot());
        Assert.Empty(instantDeath.Snapshot());
    }

    [Fact]
    public void ActorRestoreAndSaveValidation_RejectEveryMalformedActorEnumPath()
    {
        RuntimeSaveGameSnapshot baseline = RuntimePersistenceSnapshotTests.CreateSaveSnapshot();
        RuntimeActorSnapshot source = baseline.Actors[0];
        var duration = new PermanentDurationDefinition();
        RuntimeAnalysisSnapshot malformedAnalysis = CloneWithProperty(
            new RuntimeAnalysisSnapshot(RuntimeInstanceId.Parse("target"), [AnalysisLayer.Stats]),
            nameof(RuntimeAnalysisSnapshot.Layers),
            (IReadOnlyList<AnalysisLayer>)Array.AsReadOnly([Undefined<AnalysisLayer>()]));
        RuntimeEquipmentSnapshot malformedEquipment = CloneWithProperty(
            new RuntimeEquipmentSnapshot(),
            nameof(RuntimeEquipmentSnapshot.EquippedItemIds),
            (IReadOnlyDictionary<EquipmentSlot, ContentId>)new Dictionary<EquipmentSlot, ContentId>
            {
                [Undefined<EquipmentSlot>()] = Id("convergence.catalog_surface_sample:shortsword_sample")
            });
        RuntimeBattleStatusSnapshot malformedStatus = new(
            charges:
            [
                CloneWithProperty(
                    new RuntimeChargeSnapshot(ChargeKind.Physical, 2m, duration),
                    nameof(RuntimeChargeSnapshot.Kind),
                    Undefined<ChargeKind>())
            ],
            shields:
            [
                CloneWithProperty(
                    new RuntimeShieldSnapshot(ShieldKind.Physical, duration),
                    nameof(RuntimeShieldSnapshot.Kind),
                    Undefined<ShieldKind>())
            ],
            affinityBreaks:
            [
                CloneWithProperty(
                    new RuntimeAffinityBreakSnapshot(DamageElement.Fire, duration),
                    nameof(RuntimeAffinityBreakSnapshot.Element),
                    Undefined<DamageElement>())
            ],
            affinityOverrides:
            [
                CloneWithProperty(
                    new RuntimeAffinityOverrideSnapshot(
                        DamageElement.Fire,
                        ElementalAffinity.Normal,
                        duration),
                    nameof(RuntimeAffinityOverrideSnapshot.Element),
                    Undefined<DamageElement>()),
                CloneWithProperty(
                    new RuntimeAffinityOverrideSnapshot(
                        DamageElement.Ice,
                        ElementalAffinity.Resist,
                        duration),
                    nameof(RuntimeAffinityOverrideSnapshot.Affinity),
                    Undefined<ElementalAffinity>())
            ],
            analysis: [malformedAnalysis]);
        RuntimeActorSnapshot malformed = CopyActor(
            source,
            source.Deployment with { Deployment = Undefined<RuntimeActorDeployment>() },
            malformedEquipment,
            malformedStatus);
        RuntimeActorSnapshot[] actors = baseline.Actors.ToArray();
        actors[0] = malformed;
        RuntimeSaveGameSnapshot save = CopySave(baseline, actors: actors);

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            save,
            RuntimePersistenceSnapshotTests.LoadCatalog());

        AssertUndefinedDiagnostic(validation, "$.actors[0].deployment.deployment");
        AssertUndefinedDiagnostic(validation, "$.actors[0].equipment.equippedItemIds.999");
        AssertUndefinedDiagnostic(validation, "$.actors[0].battleStatus.charges[0].kind");
        AssertUndefinedDiagnostic(validation, "$.actors[0].battleStatus.shields[0].kind");
        AssertUndefinedDiagnostic(validation, "$.actors[0].battleStatus.affinityBreaks[0].element");
        AssertUndefinedDiagnostic(validation, "$.actors[0].battleStatus.affinityOverrides[0].element");
        AssertUndefinedDiagnostic(validation, "$.actors[0].battleStatus.affinityOverrides[1].affinity");
        AssertUndefinedDiagnostic(validation, "$.actors[0].battleStatus.analysis[0].layers[0]");

        ArgumentException restore = Assert.Throws<ArgumentException>(() => RuntimeActorState.Restore(
            malformed,
            CombatDefenseProfile.Empty));
        Assert.Contains("$.deployment.deployment", restore.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveValidation_RejectsEveryMalformedAggregateEnumPath()
    {
        RuntimeSaveGameSnapshot baseline = RuntimePersistenceSnapshotTests.CreateSaveSnapshot();
        ContentId entityId = Id("convergence.clean_battle_demo:ember_duelist_demo");
        ContentId ailmentId = Id("convergence.shared_effects_demo:poison_demo");
        RuntimeInventorySnapshot malformedInventory = CloneWithProperty(
            baseline.Inventory,
            nameof(RuntimeInventorySnapshot.OwnedEquipmentIds),
            (IReadOnlyDictionary<EquipmentSlot, IReadOnlyList<ContentId>>)
            new Dictionary<EquipmentSlot, IReadOnlyList<ContentId>>
            {
                [Undefined<EquipmentSlot>()] = Array.AsReadOnly(
                    [Id("convergence.catalog_surface_sample:shortsword_sample")])
            });
        RuntimeEquipmentSnapshot malformedEquipment = CloneWithProperty(
            baseline.Equipment,
            nameof(RuntimeEquipmentSnapshot.EquippedItemIds),
            (IReadOnlyDictionary<EquipmentSlot, ContentId>)new Dictionary<EquipmentSlot, ContentId>
            {
                [Undefined<EquipmentSlot>()] = Id("convergence.catalog_surface_sample:shortsword_sample")
            });
        var validElemental = new RuntimeElementalAffinityKnowledgeSnapshot(
            entityId,
            DamageElement.Ice,
            ElementalAffinity.Weak);
        var validAilment = new RuntimeAilmentResistanceKnowledgeSnapshot(
            entityId,
            ailmentId,
            ResistanceLevel.Normal);
        var validInstantDeath = new RuntimeInstantDeathResistanceKnowledgeSnapshot(
            entityId,
            InstantDeathChannel.Light,
            ResistanceLevel.Normal);
        var knowledge = new RuntimeKnowledgeSnapshot(
            elementalAffinities:
            [
                validElemental with { Element = Undefined<DamageElement>() },
                validElemental with { Affinity = Undefined<ElementalAffinity>() }
            ],
            ailmentResistances:
            [
                validAilment with { Resistance = Undefined<ResistanceLevel>() }
            ],
            instantDeathResistances:
            [
                validInstantDeath with { Channel = Undefined<InstantDeathChannel>() },
                validInstantDeath with { Resistance = Undefined<ResistanceLevel>() }
            ]);
        RuntimeCheckpointEntrySnapshot malformedCheckpoint = CloneWithProperty(
            new RuntimeCheckpointEntrySnapshot(0, RuntimeCheckpointKind.HostAction, "Malformed."),
            nameof(RuntimeCheckpointEntrySnapshot.Kind),
            Undefined<RuntimeCheckpointKind>());
        RuntimeSaveGameSnapshot save = CopySave(
            baseline,
            inventory: malformedInventory,
            equipment: malformedEquipment,
            knowledge: knowledge,
            checkpoints: new RuntimeCheckpointLogSnapshot([malformedCheckpoint]));

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            save,
            RuntimePersistenceSnapshotTests.LoadCatalog());

        AssertUndefinedDiagnostic(validation, "$.inventory.ownedEquipmentIds.999");
        AssertUndefinedDiagnostic(validation, "$.equipment.equippedItemIds.999");
        AssertUndefinedDiagnostic(validation, "$.knowledge.elementalAffinities[0].element");
        AssertUndefinedDiagnostic(validation, "$.knowledge.elementalAffinities[1].affinity");
        AssertUndefinedDiagnostic(validation, "$.knowledge.ailmentResistances[0].resistance");
        AssertUndefinedDiagnostic(validation, "$.knowledge.instantDeathResistances[0].channel");
        AssertUndefinedDiagnostic(validation, "$.knowledge.instantDeathResistances[1].resistance");
        AssertUndefinedDiagnostic(validation, "$.checkpoints.entries[0].kind");
        Assert.Throws<RuntimeSaveValidationException>(() => validation.RequireValidSnapshot());
    }

    private static RuntimeActorSnapshot CopyActor(
        RuntimeActorSnapshot source,
        RuntimeActorDeploymentSnapshot deployment,
        RuntimeEquipmentSnapshot equipment,
        RuntimeBattleStatusSnapshot battleStatus) =>
        new(
            source.Identity,
            source.Ownership,
            deployment,
            source.Progression,
            source.Resources,
            source.Stats,
            source.Skills,
            source.Rosters,
            equipment,
            battleStatus,
            source.BattleActivations,
            source.BaseResourceValues,
            source.VitalResourceId,
            source.CapabilityIds);

    private static RuntimeSaveGameSnapshot CopySave(
        RuntimeSaveGameSnapshot source,
        IEnumerable<RuntimeActorSnapshot>? actors = null,
        RuntimeInventorySnapshot? inventory = null,
        RuntimeEquipmentSnapshot? equipment = null,
        RuntimeKnowledgeSnapshot? knowledge = null,
        RuntimeCheckpointLogSnapshot? checkpoints = null) =>
        new(
            source.FrameworkVersion,
            source.ContentPacks,
            actors ?? source.Actors,
            source.PartyRoster,
            inventory ?? source.Inventory,
            equipment ?? source.Equipment,
            source.Wallet,
            source.Field,
            source.Compendium,
            knowledge ?? source.Knowledge,
            source.Session,
            checkpoints ?? source.Checkpoints,
            source.HostContext,
            source.ContractVersion);

    private static TSnapshot CloneWithProperty<TSnapshot, TValue>(
        TSnapshot source,
        string propertyName,
        TValue value)
        where TSnapshot : class
    {
        MethodInfo memberwiseClone = typeof(object).GetMethod(
            "MemberwiseClone",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var clone = (TSnapshot)memberwiseClone.Invoke(source, null)!;
        FieldInfo field = typeof(TSnapshot).GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                $"Snapshot property '{typeof(TSnapshot).Name}.{propertyName}' has no backing field.");
        field.SetValue(clone, value);
        return clone;
    }

    private static void AssertUndefined(string parameterName, Action action)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal(parameterName, exception.ParamName);
    }

    private static void AssertUndefinedDiagnostic(
        RuntimeSaveValidationResult result,
        string path) =>
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.UndefinedEnumValue &&
            diagnostic.Path == path);

    private static TEnum Undefined<TEnum>()
        where TEnum : struct, Enum =>
        (TEnum)Enum.ToObject(typeof(TEnum), UndefinedValue);

    private static ContentId Id(string value) => ContentId.Parse(value);
}
