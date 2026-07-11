using System.Text.Json;
using Convergence.Tests.Runtime;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Host;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Host;

public sealed class CleanSaveDemoHostTests
{
    [Fact]
    public void HostOwnedJsonRoundTrip_PreservesRuntimeSaveFamilies()
    {
        RuntimeSaveGameSnapshot snapshot = RuntimePersistenceSnapshotTests.CreateSaveSnapshot();

        string json = CleanSaveJsonCodec.Serialize(snapshot);
        RuntimeSaveGameSnapshot restored = CleanSaveJsonCodec.Deserialize(json);

        Assert.Contains("\"contractVersion\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(snapshot.ContractVersion, restored.ContractVersion);
        Assert.Equal(snapshot.FrameworkVersion, restored.FrameworkVersion);
        Assert.Equal(snapshot.Actors.Select(actor => actor.Identity.InstanceId), restored.Actors.Select(actor => actor.Identity.InstanceId));
        Assert.Equal(snapshot.Actors[0].CapabilityIds, restored.Actors[0].CapabilityIds);
        Assert.Equal(
            snapshot.Actors[0].BattleActivations.PassiveSkillStates,
            restored.Actors[0].BattleActivations.PassiveSkillStates);
        Assert.Equal(snapshot.PartyStock.ActiveParty.Select(actor => actor.InstanceId), restored.PartyStock.ActiveParty.Select(actor => actor.InstanceId));
        Assert.Equal(
            snapshot.Inventory.ItemQuantities.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)),
            restored.Inventory.ItemQuantities.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)));
        Assert.Equal(snapshot.Wallet.Macca, restored.Wallet.Macca);
        Assert.Equal(
            snapshot.Field!.DungeonTraversal!.CurrentNodeId,
            restored.Field!.DungeonTraversal!.CurrentNodeId);
        Assert.Equal(snapshot.Compendium.Entries.Select(entry => entry.SpeciesId), restored.Compendium.Entries.Select(entry => entry.SpeciesId));
        Assert.Equal(
            snapshot.Compendium.Entries.Select(entry => entry.UnspentStatPoints),
            restored.Compendium.Entries.Select(entry => entry.UnspentStatPoints));
        Assert.Equal(
            snapshot.Compendium.Entries.SelectMany(entry => entry.EquippedSkillIds),
            restored.Compendium.Entries.SelectMany(entry => entry.EquippedSkillIds));
        Assert.Equal(snapshot.Knowledge.ElementalAffinities.Select(entry => entry.EntityId), restored.Knowledge.ElementalAffinities.Select(entry => entry.EntityId));
        Assert.Equal(
            snapshot.Session.Counters.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)),
            restored.Session.Counters.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)));
        Assert.Equal(snapshot.Checkpoints.Entries.Select(entry => entry.Sequence), restored.Checkpoints.Entries.Select(entry => entry.Sequence));
    }

    [Fact]
    public void HostOwnedJsonRoundTrip_PreservesCanonicalActorBattleStateAndDurationKinds()
    {
        RuntimeActorSnapshot original = RuntimePersistenceSnapshotTests.CreateActor(
            RuntimeInstanceId.Parse("frost"),
            ContentId.Parse("convergence.clean_battle_demo:frost_duelist_demo"));
        var actor = new RuntimeActorSnapshot(
            original.Identity,
            original.Ownership,
            original.Deployment,
            original.Progression,
            original.Resources,
            original.Stats,
            original.Skills,
            original.Forms,
            original.Equipment,
            new RuntimeBattleStatusSnapshot(
                statuses:
                [
                    new RuntimeTimedStateSnapshot(
                        ContentId.Parse("focused"),
                        new PermanentDurationDefinition())
                ],
                statStages:
                [
                    new RuntimeStatStageSnapshot(
                        ContentId.Parse("attack"),
                        2,
                        new PhaseDurationDefinition(ContentId.Parse("phase_end")))
                ],
                affinityOverrides:
                [
                    new RuntimeAffinityOverrideSnapshot(
                        DamageElement.Ice,
                        ElementalAffinity.Resist,
                        new BattleDurationDefinition())
                ],
                isGuarding: true,
                analysis:
                [
                    new RuntimeAnalysisSnapshot(
                        RuntimeInstanceId.Parse("enemy_1"),
                        [AnalysisLayer.Affinities])
                ]),
            new RuntimeBattleActivationSnapshot(
                passiveSkillStates:
                [
                    new RuntimePassiveSkillStateSnapshot(
                        ContentId.Parse("convergence.skill_system_redesign_sample:ice_boost_sample"),
                        IsEnabled: false)
                ]),
            original.BaseResourceValues,
            original.VitalResourceId,
            [ContentId.Parse("analyze"), ContentId.Parse("switch_form")]);
        RuntimeSaveGameSnapshot snapshot = RuntimePersistenceSnapshotTests.CreateSaveSnapshot(actors: [actor]);

        RuntimeSaveGameSnapshot restored = CleanSaveJsonCodec.Deserialize(CleanSaveJsonCodec.Serialize(snapshot));
        RuntimeActorSnapshot restoredActor = Assert.Single(restored.Actors);

        Assert.Equal(original.VitalResourceId, restoredActor.VitalResourceId);
        Assert.IsType<PermanentDurationDefinition>(Assert.Single(restoredActor.BattleStatus.Statuses).Duration);
        Assert.IsType<PhaseDurationDefinition>(Assert.Single(restoredActor.BattleStatus.StatStages).Duration);
        Assert.IsType<BattleDurationDefinition>(Assert.Single(restoredActor.BattleStatus.AffinityOverrides).Duration);
        Assert.True(restoredActor.BattleStatus.IsGuarding);
        Assert.Equal(RuntimeInstanceId.Parse("enemy_1"), Assert.Single(restoredActor.BattleStatus.Analysis).TargetInstanceId);
        Assert.Equal(
            [ContentId.Parse("analyze"), ContentId.Parse("switch_form")],
            restoredActor.CapabilityIds);
        Assert.False(Assert.Single(restoredActor.BattleActivations.PassiveSkillStates).IsEnabled);
    }

    [Fact]
    public void CleanSaveDemo_ValidatesSerializesRestoresAndExitsWithoutInput()
    {
        using var output = new StringWriter();
        int exitCode = new CleanSaveDemoHost(output, Path.Combine(FindRepositoryRoot(), "Data", "Jsons")).Run();

        string text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("[save] Created runtime save snapshot v5", text, StringComparison.Ordinal);
        Assert.Contains("[serialize] Host-owned JSON round-trip completed", text, StringComparison.Ordinal);
        Assert.Contains("[validate] Restored snapshot validated with 0 diagnostic(s).", text, StringComparison.Ordinal);
        Assert.Contains("[restore] Restored 2 actor(s), 1 item stack(s), dungeon node convergence.catalog_surface_sample:floor_5.", text, StringComparison.Ordinal);
        Assert.Contains("[outcome] Clean save demo completed successfully.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanSaveJsonCodec_RejectsMalformedHostOwnedJson()
    {
        Assert.Throws<JsonException>(() => CleanSaveJsonCodec.Deserialize("{"));
    }

    [Fact]
    public void HostOwnedJsonRoundTrip_PreservesSaveRecordMetadata()
    {
        RuntimeSaveRecord record = new(
            RuntimeSaveKind.Suspend,
            RuntimePersistenceSnapshotTests.CreateSaveSnapshot(),
            new RuntimeSaveContextSnapshot(ContentId.Parse("dungeon_menu")),
            sequence: 42);

        string json = CleanSaveJsonCodec.SerializeRecord(record);
        RuntimeSaveRecord restored = CleanSaveJsonCodec.DeserializeRecord(json);

        Assert.Contains("\"kind\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"contentPacks\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RuntimeSaveKind.Suspend, restored.Kind);
        Assert.Equal(ContentId.Parse("dungeon_menu"), restored.Context.ContextId);
        Assert.False(restored.Context.HasPendingHostAction);
        Assert.Equal(42, restored.Sequence);
        Assert.Equal(record.Snapshot.ContractVersion, restored.Snapshot.ContractVersion);
        Assert.Equal(record.Snapshot.ContentPacks, restored.Snapshot.ContentPacks);
        Assert.Equal(record.Snapshot.Wallet.Macca, restored.Snapshot.Wallet.Macca);
    }

    [Fact]
    public void CleanSaveJsonCodec_RejectsMalformedHostOwnedSaveRecordJson()
    {
        Assert.Throws<JsonException>(() => CleanSaveJsonCodec.DeserializeRecord("{"));
    }

    [Fact]
    public void HostOwnedJsonRoundTrip_PreservesOptionalNavigationAndDungeonState()
    {
        RuntimeSaveGameSnapshot noField = RuntimePersistenceSnapshotTests.CreateSaveSnapshot(
            includeDefaultField: false);
        RuntimeSaveGameSnapshot navigationOnly = RuntimePersistenceSnapshotTests.CreateSaveSnapshot(
            field: new RuntimeFieldSnapshot(
                new RuntimeNavigationSnapshot(ContentId.Parse("host_owned_location"))));

        RuntimeSaveGameSnapshot restoredNoField = CleanSaveJsonCodec.Deserialize(
            CleanSaveJsonCodec.Serialize(noField));
        RuntimeSaveGameSnapshot restoredNavigationOnly = CleanSaveJsonCodec.Deserialize(
            CleanSaveJsonCodec.Serialize(navigationOnly));

        Assert.Null(restoredNoField.Field);
        Assert.Equal(
            ContentId.Parse("host_owned_location"),
            restoredNavigationOnly.Field!.Navigation.CurrentLocationId);
        Assert.Null(restoredNavigationOnly.Field.DungeonTraversal);
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
