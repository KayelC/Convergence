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
        StatusLifetimeDefinition duration = StandardStatusLifetimes.Persistent;

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
    }

    [Fact]
    public void PersistedSnapshotConstructors_RejectUndefinedEnumValues()
    {
        ContentId entityId = Id("test.pack:entity");
        ContentId ailmentId = Id("test.pack:ailment");

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
        StatusLifetimeDefinition duration = StandardStatusLifetimes.Persistent;
        AssertUndefined("chargeKind", () => new ChargeApplicationRequest(
            actor,
            Undefined<ChargeKind>(),
            2m,
            duration));
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
        RuntimeBattleStatusSnapshot status = actor.ToSnapshot().BattleStatus;
        Assert.True(actor.IsDeployed);
        Assert.Empty(status.Charges);
        Assert.Empty(status.Shields);
        Assert.Empty(status.AffinityBreaks);
        Assert.Empty(status.AffinityOverrides);
    }

    [Fact]
    public void ActorRestoreAndSaveValidation_RejectEveryMalformedActorEnumPath()
    {
        RuntimeSaveGameSnapshot baseline = RuntimePersistenceSnapshotTests.CreateSaveSnapshot();
        RuntimeActorSnapshot source = baseline.Actors[0];
        StatusLifetimeDefinition duration = StandardStatusLifetimes.Persistent;
        RuntimeBattleStatusSnapshot malformedStatus = new(
            chargeState: new RuntimeChargeStateSnapshot(
                StandardChargePolicyIds.Split,
                [
                    CloneWithProperty(
                        new RuntimeChargeSnapshot(ChargeKind.Physical, 2m, duration),
                        nameof(RuntimeChargeSnapshot.Kind),
                        Undefined<ChargeKind>())
                ]),
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
            ]);
        RuntimeActorSnapshot malformed = CopyActor(source, source.Equipment, malformedStatus);
        RuntimeActorSnapshot[] actors = baseline.Actors.ToArray();
        actors[0] = malformed;
        RuntimeSaveGameSnapshot save = CopySave(baseline, actors: actors);

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            save,
            RuntimePersistenceSnapshotTests.LoadCatalog());

        AssertUndefinedDiagnostic(validation, "$.actors[0].battleStatus.charges[0].kind");
        AssertUndefinedDiagnostic(validation, "$.actors[0].battleStatus.shields[0].kind");
        AssertUndefinedDiagnostic(validation, "$.actors[0].battleStatus.affinityBreaks[0].element");
        AssertUndefinedDiagnostic(validation, "$.actors[0].battleStatus.affinityOverrides[0].element");
        AssertUndefinedDiagnostic(validation, "$.actors[0].battleStatus.affinityOverrides[1].affinity");

        ArgumentException restore = Assert.Throws<ArgumentException>(() => RuntimeActorState.Restore(
            malformed,
            CombatDefenseProfile.Empty));
        Assert.Contains("$.battleStatus", restore.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveValidation_RejectsEveryMalformedAggregateEnumPath()
    {
        RuntimeSaveGameSnapshot baseline = RuntimePersistenceSnapshotTests.CreateSaveSnapshot();
        ContentId entityId = Id("convergence.clean_battle_demo:ember_duelist_demo");
        ContentId ailmentId = Id("convergence.shared_effects_demo:poison_demo");
        RuntimeInventorySnapshot malformedInventory = baseline.Inventory;
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
        RuntimeAnalyzedDefenseKnowledgeSnapshot malformedAnalyzedDefense = CloneWithProperty(
            new RuntimeAnalyzedDefenseKnowledgeSnapshot(
                entityId,
                [BattleAnalysisField.ElementalAffinities]),
            nameof(RuntimeAnalyzedDefenseKnowledgeSnapshot.DisclosedFields),
            (IReadOnlyList<BattleAnalysisField>)Array.AsReadOnly(
                [Undefined<BattleAnalysisField>(), BattleAnalysisField.CurrentHp]));
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
            ],
            analyzedDefenses: [malformedAnalyzedDefense]);
        RuntimeCheckpointEntrySnapshot malformedCheckpoint = CloneWithProperty(
            new RuntimeCheckpointEntrySnapshot(0, RuntimeCheckpointKind.HostAction, "Malformed."),
            nameof(RuntimeCheckpointEntrySnapshot.Kind),
            Undefined<RuntimeCheckpointKind>());
        RuntimeSaveGameSnapshot save = CopySave(
            baseline,
            inventory: malformedInventory,
            knowledge: knowledge,
            checkpoints: new RuntimeCheckpointLogSnapshot([malformedCheckpoint]));

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            save,
            RuntimePersistenceSnapshotTests.LoadCatalog());

        AssertUndefinedDiagnostic(validation, "$.knowledge.elementalAffinities[0].element");
        AssertUndefinedDiagnostic(validation, "$.knowledge.elementalAffinities[1].affinity");
        AssertUndefinedDiagnostic(validation, "$.knowledge.ailmentResistances[0].resistance");
        AssertUndefinedDiagnostic(validation, "$.knowledge.instantDeathResistances[0].channel");
        AssertUndefinedDiagnostic(validation, "$.knowledge.instantDeathResistances[1].resistance");
        AssertUndefinedDiagnostic(validation, "$.knowledge.analyzedDefenses[0].disclosedFields[0]");
        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.InvalidAnalyzedDefenseField &&
            diagnostic.Path == "$.knowledge.analyzedDefenses[0].disclosedFields[1]");
        AssertUndefinedDiagnostic(validation, "$.checkpoints.entries[0].kind");
        Assert.Throws<RuntimeSaveValidationException>(() => validation.RequireValidSnapshot());
    }

    private static RuntimeActorSnapshot CopyActor(
        RuntimeActorSnapshot source,
        RuntimeEquipmentSnapshot equipment,
        RuntimeBattleStatusSnapshot battleStatus) =>
        new(
            source.Identity,
            source.Affiliation,
            source.EncounterPresence,
            source.Progression,
            source.Resources,
            source.Stats,
            source.Skills,
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
        RuntimeKnowledgeSnapshot? knowledge = null,
        RuntimeCheckpointLogSnapshot? checkpoints = null) =>
        new(
            source.FrameworkVersion,
            source.ContentPacks,
            actors ?? source.Actors,
            source.PartyRoster,
            inventory ?? source.Inventory,
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
