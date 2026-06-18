using System.Text.Json;
using Convergence.Tests.Runtime;
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
        Assert.Equal(snapshot.PartyStock.ActiveParty.Select(actor => actor.InstanceId), restored.PartyStock.ActiveParty.Select(actor => actor.InstanceId));
        Assert.Equal(
            snapshot.Inventory.ItemQuantities.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)),
            restored.Inventory.ItemQuantities.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)));
        Assert.Equal(snapshot.Wallet.Macca, restored.Wallet.Macca);
        Assert.Equal(snapshot.Field.DungeonProgress.CurrentFloor, restored.Field.DungeonProgress.CurrentFloor);
        Assert.Equal(snapshot.Compendium.Entries.Select(entry => entry.SpeciesId), restored.Compendium.Entries.Select(entry => entry.SpeciesId));
        Assert.Equal(snapshot.Knowledge.ElementalAffinities.Select(entry => entry.EntityId), restored.Knowledge.ElementalAffinities.Select(entry => entry.EntityId));
        Assert.Equal(
            snapshot.Session.Counters.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)),
            restored.Session.Counters.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)));
        Assert.Equal(snapshot.Checkpoints.Entries.Select(entry => entry.Sequence), restored.Checkpoints.Entries.Select(entry => entry.Sequence));
    }

    [Fact]
    public void CleanSaveDemo_ValidatesSerializesRestoresAndExitsWithoutInput()
    {
        using var output = new StringWriter();
        int exitCode = new CleanSaveDemoHost(output, Path.Combine(FindRepositoryRoot(), "Data", "Jsons")).Run();

        string text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("[save] Created runtime save snapshot v1", text, StringComparison.Ordinal);
        Assert.Contains("[serialize] Host-owned JSON round-trip completed", text, StringComparison.Ordinal);
        Assert.Contains("[validate] Restored snapshot validated with 0 diagnostic(s).", text, StringComparison.Ordinal);
        Assert.Contains("[restore] Restored 2 actor(s), 1 item stack(s), floor 5.", text, StringComparison.Ordinal);
        Assert.Contains("[outcome] Clean save demo completed successfully.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanSaveJsonCodec_RejectsMalformedHostOwnedJson()
    {
        Assert.Throws<JsonException>(() => CleanSaveJsonCodec.Deserialize("{"));
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
